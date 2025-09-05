// <copyright file="AdfApiImporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Importers
{
    /// <summary>
    /// This class imports ADF resources using Azure Data Factory REST APIs.
    /// </summary>
    public class AdfApiImporter
    {
        private readonly FabricUpgradeProgress progress;
        private readonly string subscriptionId;
        private readonly string resourceGroupName;
        private readonly string factoryName;
        private readonly string accessToken;
        private readonly string pipelineResourceId;
        private readonly AlertCollector alerts;

        private readonly AdfSupportFileUpgradePackage upgradePackage = new AdfSupportFileUpgradePackage();

        public AdfApiImporter(
            FabricUpgradeProgress progress,
            string subscriptionId,
            string resourceGroupName,
            string factoryName,
            string accessToken,
            string pipelineResourceId,
            AlertCollector alerts)
        {
            this.progress = progress;
            this.subscriptionId = subscriptionId;
            this.resourceGroupName = resourceGroupName;
            this.factoryName = factoryName;
            this.accessToken = accessToken;
            this.pipelineResourceId = pipelineResourceId;
            this.alerts = alerts;
        }

        /// <summary>
        /// Import ADF resources using REST APIs.
        /// </summary>
        /// <param name="includeUnusedResources">Whether to include datasets and linked services that are not used by any pipelines.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A FabricUpgradeProgress containing the imported resources.</returns>
        public async Task<FabricUpgradeProgress> ImportAsync(bool includeUnusedResources = true, CancellationToken cancellationToken = default)
        {
            AdfApiClient adfClient = null;
            try
            {
                adfClient = new AdfApiClient(subscriptionId, resourceGroupName, factoryName, accessToken);

                // Get Data Factory information for the ADF name
                JObject dataFactory = await adfClient.GetDataFactoryAsync(cancellationToken).ConfigureAwait(false);
                this.upgradePackage.AdfName = dataFactory["name"]?.ToString() ?? factoryName;

                // If a specific pipeline is requested, import only that pipeline and its dependencies
                if (!string.IsNullOrEmpty(pipelineResourceId))
                {
                    await ImportSpecificPipelineAsync(adfClient, pipelineResourceId, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Import all resources
                    await ImportAllResourcesAsync(adfClient, includeUnusedResources, cancellationToken).ConfigureAwait(false);
                }

                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                    Alerts = this.alerts.ToList(),
                    Result = this.BuildResult(),
                    Resolutions = this.progress.Resolutions,
                };
            }
            catch (Exception ex)
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.alerts.ToList(),
                }
                .WithAlert(
                    new FabricUpgradeAlert()
                    {
                        Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                        Details = $"Failed to import from ADF: {ex.Message}",
                    });
            }
            finally
            {
                adfClient?.Dispose();
            }
        }

        /// <summary>
        /// Import all resources from the Data Factory.
        /// </summary>
        /// <param name="adfClient">The ADF API client.</param>
        /// <param name="includeUnusedResources">Whether to include unused resources.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task ImportAllResourcesAsync(AdfApiClient adfClient, bool includeUnusedResources, CancellationToken cancellationToken)
        {
            // Import all pipelines first
            var pipelines = await adfClient.GetPipelinesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var pipeline in pipelines)
            {
                this.upgradePackage.Pipelines[pipeline.Key] = pipeline.Value;
            }

            // Get all available datasets and linked services once
            var allDatasets = await adfClient.GetDatasetsAsync(cancellationToken).ConfigureAwait(false);
            var allLinkedServices = await adfClient.GetLinkedServicesAsync(cancellationToken).ConfigureAwait(false);

            if (includeUnusedResources)
            {
                // Import all datasets and linked services
                foreach (var dataset in allDatasets)
                {
                    this.upgradePackage.Datasets[dataset.Key] = dataset.Value;
                }

                foreach (var linkedService in allLinkedServices)
                {
                    this.upgradePackage.LinkedServices[linkedService.Key] = linkedService.Value;
                }
            }
            else
            {
                // Use the common filtering logic from DependencyAnalyzer
                var (filteredDatasets, filteredLinkedServices) = DependencyAnalyzer.FilterUnusedResources(
                    this.upgradePackage.Pipelines,
                    allDatasets,
                    allLinkedServices,
                    this.alerts);

                // Import the filtered resources into the upgrade package
                foreach (var dataset in filteredDatasets)
                {
                    this.upgradePackage.Datasets[dataset.Key] = dataset.Value;
                }

                foreach (var linkedService in filteredLinkedServices)
                {
                    this.upgradePackage.LinkedServices[linkedService.Key] = linkedService.Value;
                }
            }

            // Import all triggers (triggers are not subject to dependency filtering)
            var triggers = await adfClient.GetTriggersAsync(cancellationToken).ConfigureAwait(false);
            foreach (var trigger in triggers)
            {
                this.upgradePackage.Triggers[trigger.Key] = trigger.Value;
            }
        }

        /// <summary>
        /// Import a specific pipeline and its dependencies.
        /// </summary>
        /// <param name="adfClient">The ADF API client.</param>
        /// <param name="pipelineName">The name of the pipeline to import.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task ImportSpecificPipelineAsync(AdfApiClient adfClient, string pipelineName, CancellationToken cancellationToken)
        {
            var importedDatasets = new HashSet<string>();
            var importedLinkedServices = new HashSet<string>();
            var importedPipelines = new HashSet<string>();

            // Import the main pipeline
            await ImportPipelineWithDependenciesAsync(adfClient, pipelineName, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Recursively import a pipeline and all its dependencies.
        /// </summary>
        /// <param name="adfClient">The ADF API client.</param>
        /// <param name="pipelineName">The name of the pipeline to import.</param>
        /// <param name="importedPipelines">Set of already imported pipeline names.</param>
        /// <param name="importedDatasets">Set of already imported dataset names.</param>
        /// <param name="importedLinkedServices">Set of already imported linked service names.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task ImportPipelineWithDependenciesAsync(
            AdfApiClient adfClient,
            string pipelineName,
            HashSet<string> importedPipelines,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken)
        {
            if (importedPipelines.Contains(pipelineName))
            {
                return; // Already imported
            }

            try
            {
                // Import the pipeline
                JObject pipeline = await adfClient.GetPipelineAsync(pipelineName, cancellationToken).ConfigureAwait(false);
                this.upgradePackage.Pipelines[pipelineName] = pipeline;
                importedPipelines.Add(pipelineName);

                // Find and import dependencies from the pipeline activities
                await ImportPipelineDependenciesAsync(adfClient, pipeline, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.alerts.AddWarning($"Failed to import pipeline '{pipelineName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Import dependencies found in a pipeline's activities.
        /// </summary>
        /// <param name="adfClient">The ADF API client.</param>
        /// <param name="pipeline">The pipeline definition.</param>
        /// <param name="importedPipelines">Set of already imported pipeline names.</param>
        /// <param name="importedDatasets">Set of already imported dataset names.</param>
        /// <param name="importedLinkedServices">Set of already imported linked service names.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task ImportPipelineDependenciesAsync(
            AdfApiClient adfClient,
            JObject pipeline,
            HashSet<string> importedPipelines,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken)
        {
            JArray activities = pipeline.SelectToken("properties.activities") as JArray;
            if (activities == null) return;

            foreach (JObject activity in activities)
            {
                await ImportActivityAndSubtreeDependenciesAsync(
                    adfClient,
                    activity,
                    importedPipelines,
                    importedDatasets,
                    importedLinkedServices,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ImportActivityAndSubtreeDependenciesAsync(
            AdfApiClient adfClient,
            JObject activity,
            HashSet<string> importedPipelines,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken)
        {
            // First handle the current activity
            await ImportActivityDatasetsAsync(adfClient, activity, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
            await ImportReferencedPipelinesAsync(adfClient, activity, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
            await ImportActivityLinkedServicesAsync(adfClient, activity, importedLinkedServices, cancellationToken).ConfigureAwait(false);

            string activityType = activity.SelectToken("type")?.ToString();

            // Switch cases and defaultActivities
            if (activityType == "Switch")
            {
                var cases = activity.SelectToken("typeProperties.cases") as JArray;
                if (cases != null)
                {
                    foreach (JObject caseToken in cases.OfType<JObject>())
                    {
                        var caseActivities = caseToken.SelectToken("activities") as JArray;
                        if (caseActivities != null)
                        {
                            foreach (JObject subAct in caseActivities.OfType<JObject>())
                            {
                                await ImportActivityAndSubtreeDependenciesAsync(adfClient, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                }
                var defaultActs = activity.SelectToken("typeProperties.defaultActivities") as JArray;
                if (defaultActs != null)
                {
                    foreach (JObject subAct in defaultActs.OfType<JObject>())
                    {
                        await ImportActivityAndSubtreeDependenciesAsync(adfClient, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // IfCondition: ifTrueActivities / ifFalseActivities
            if (activityType == "IfCondition")
            {
                var trueActs = activity.SelectToken("typeProperties.ifTrueActivities") as JArray;
                if (trueActs != null)
                {
                    foreach (JObject subAct in trueActs.OfType<JObject>())
                    {
                        await ImportActivityAndSubtreeDependenciesAsync(adfClient, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                    }
                }
                var falseActs = activity.SelectToken("typeProperties.ifFalseActivities") as JArray;
                if (falseActs != null)
                {
                    foreach (JObject subAct in falseActs.OfType<JObject>())
                    {
                        await ImportActivityAndSubtreeDependenciesAsync(adfClient, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // ForEach: typeProperties.activities
            if (activityType == "ForEach")
            {
                var forEachActivities = activity.SelectToken("typeProperties.activities") as JArray;
                if (forEachActivities != null)
                {
                    foreach (JObject subAct in forEachActivities.OfType<JObject>())
                    {
                        await ImportActivityAndSubtreeDependenciesAsync(adfClient, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Import datasets referenced by an activity.
        /// </summary>
        private async Task ImportActivityDatasetsAsync(
            AdfApiClient adfClient,
            JObject activity,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken)
        {
            // Handle Lookup activity dataset reference (not in inputs/outputs)
            string activityType = activity.SelectToken("type")?.ToString();
            if (activityType == "Lookup")
            {
                string lookupDatasetName = activity.SelectToken("typeProperties.dataset.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(lookupDatasetName))
                {
                    await ImportDatasetWithLinkedServiceAsync(
                        adfClient,
                        lookupDatasetName,
                        importedDatasets,
                        importedLinkedServices,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            // Check inputs and outputs for dataset references
            var inputs = activity.SelectToken("inputs") as JArray;
            var outputs = activity.SelectToken("outputs") as JArray;

            if (inputs != null)
            {
                foreach (JObject input in inputs)
                {
                    string datasetName = input.SelectToken("referenceName")?.ToString();
                    if (!string.IsNullOrEmpty(datasetName))
                    {
                        await ImportDatasetWithLinkedServiceAsync(adfClient, datasetName, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (outputs != null)
            {
                foreach (JObject output in outputs)
                {
                    string datasetName = output.SelectToken("referenceName")?.ToString();
                    if (!string.IsNullOrEmpty(datasetName))
                    {
                        await ImportDatasetWithLinkedServiceAsync(adfClient, datasetName, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Import pipelines referenced by ExecutePipeline activities.
        /// </summary>
        private async Task ImportReferencedPipelinesAsync(
            AdfApiClient adfClient,
            JObject activity,
            HashSet<string> importedPipelines,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken)
        {
            string activityType = activity.SelectToken("type")?.ToString();
            if (activityType == "ExecutePipeline")
            {
                string referencedPipelineName = activity.SelectToken("typeProperties.pipeline.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(referencedPipelineName))
                {
                    await ImportPipelineWithDependenciesAsync(adfClient, referencedPipelineName, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Import linked services referenced directly by activities.
        /// </summary>
        private async Task ImportActivityLinkedServicesAsync(
            AdfApiClient adfClient,
            JObject activity,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken)
        {
            // Web activities might reference linked services directly
            string activityType = activity.SelectToken("type")?.ToString();
            if (activityType == "WebActivity" || activityType == "WebHook")
            {
                string linkedServiceName = activity.SelectToken("typeProperties.linkedServiceName.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(linkedServiceName))
                {
                    await ImportLinkedServiceAsync(adfClient, linkedServiceName, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Import a dataset and its linked service.
        /// </summary>
        private async Task ImportDatasetWithLinkedServiceAsync(
            AdfApiClient adfClient,
            string datasetName,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken)
        {
            if (importedDatasets.Contains(datasetName))
            {
                return; // Already imported
            }

            try
            {
                JObject dataset = await adfClient.GetDatasetAsync(datasetName, cancellationToken).ConfigureAwait(false);
                this.upgradePackage.Datasets[datasetName] = dataset;
                importedDatasets.Add(datasetName);

                // Import the linked service referenced by this dataset
                string linkedServiceName = dataset.SelectToken("properties.linkedServiceName.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(linkedServiceName))
                {
                    await ImportLinkedServiceAsync(adfClient, linkedServiceName, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                this.alerts.AddWarning($"Failed to import dataset '{datasetName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Import a linked service.
        /// </summary>
        private async Task ImportLinkedServiceAsync(
            AdfApiClient adfClient,
            string linkedServiceName,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken)
        {
            if (importedLinkedServices.Contains(linkedServiceName))
            {
                return; // Already imported
            }

            try
            {
                JObject linkedService = await adfClient.GetLinkedServiceAsync(linkedServiceName, cancellationToken).ConfigureAwait(false);
                this.upgradePackage.LinkedServices[linkedServiceName] = linkedService;
                importedLinkedServices.Add(linkedServiceName);
            }
            catch (Exception ex)
            {
                this.alerts.AddWarning($"Failed to import linked service '{linkedServiceName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Construct the JObject that can be inserted into the FabricUpgradeProgress' Result field.
        /// </summary>
        /// <returns>The Result that will be returned to the client in the FabricUpgradeProgress.</returns>
        public JObject BuildResult()
        {
            JObject built = new JObject();
            built[FabricUpgradeProgress.ImportedResourcesKey] = UpgradeSerialization.ToJToken(upgradePackage);
            return built;
        }
    }
}
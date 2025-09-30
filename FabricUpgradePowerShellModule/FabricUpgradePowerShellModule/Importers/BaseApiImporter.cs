// <copyright file="BaseApiImporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Importers
{
    /// <summary>
    /// Base class for API importers that provides common dependency import logic.
    /// </summary>
    public abstract class BaseApiImporter
    {
        protected readonly FabricUpgradeProgress progress;
        protected readonly string subscriptionId;
        protected readonly string resourceGroupName;
        protected readonly AlertCollector alerts;
        protected readonly AdfSupportFileUpgradePackage upgradePackage = new AdfSupportFileUpgradePackage();

        protected BaseApiImporter(
            FabricUpgradeProgress progress,
            string subscriptionId,
            string resourceGroupName,
            AlertCollector alerts)
        {
            this.progress = progress;
            this.subscriptionId = subscriptionId;
            this.resourceGroupName = resourceGroupName;
            this.alerts = alerts;
        }

        /// <summary>
        /// Import all resources from the service.
        /// </summary>
        /// <param name="client">The API client.</param>
        /// <param name="includeUnusedResources">Whether to include unused resources.</param>
        /// <param name="verbose">Whether to output detailed logging during the import process.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task ImportAllResourcesAsync<T>(T client, bool includeUnusedResources, bool verbose, CancellationToken cancellationToken)
            where T : IApiClient
        {
            // Import all pipelines first
            var pipelines = await client.GetPipelinesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var pipeline in pipelines)
            {
                this.upgradePackage.Pipelines[pipeline.Key] = pipeline.Value;
            }

            // Get all available datasets and linked services once
            var allDatasets = await client.GetDatasetsAsync(cancellationToken).ConfigureAwait(false);
            var allLinkedServices = await client.GetLinkedServicesAsync(cancellationToken).ConfigureAwait(false);

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
                    this.alerts,
                    verbose);

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
            var triggers = await client.GetTriggersAsync(cancellationToken).ConfigureAwait(false);
            foreach (var trigger in triggers)
            {
                this.upgradePackage.Triggers[trigger.Key] = trigger.Value;
            }
        }

        /// <summary>
        /// Import a specific pipeline and its dependencies.
        /// </summary>
        /// <param name="client">The API client.</param>
        /// <param name="pipelineResourceId">The name or resource ID of the pipeline to import.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task ImportSpecificPipelineAsync<T>(T client, string pipelineResourceId, CancellationToken cancellationToken)
            where T : IApiClient
        {
            var importedDatasets = new HashSet<string>();
            var importedLinkedServices = new HashSet<string>();
            var importedPipelines = new HashSet<string>();

            // Import the main pipeline
            await ImportPipelineWithDependenciesAsync(client, pipelineResourceId, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Recursively import a pipeline and all its dependencies.
        /// </summary>
        /// <param name="client">The API client.</param>
        /// <param name="pipelineName">The name of the pipeline to import.</param>
        /// <param name="importedPipelines">Set of already imported pipeline names.</param>
        /// <param name="importedDatasets">Set of already imported dataset names.</param>
        /// <param name="importedLinkedServices">Set of already imported linked service names.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task ImportPipelineWithDependenciesAsync<T>(
            T client,
            string pipelineName,
            HashSet<string> importedPipelines,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken) where T : IApiClient
        {
            if (importedPipelines.Contains(pipelineName))
            {
                return; // Already imported
            }

            try
            {
                // Import the pipeline
                JObject pipeline = await client.GetPipelineAsync(pipelineName, cancellationToken).ConfigureAwait(false);
                this.upgradePackage.Pipelines[pipelineName] = pipeline;
                importedPipelines.Add(pipelineName);

                // Find and import dependencies from the pipeline activities
                await ImportPipelineDependenciesAsync(client, pipeline, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.alerts.AddWarning($"Failed to import pipeline '{pipelineName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Import dependencies found in a pipeline's activities.
        /// </summary>
        /// <param name="client">The API client.</param>
        /// <param name="pipeline">The pipeline definition.</param>
        /// <param name="importedPipelines">Set of already imported pipeline names.</param>
        /// <param name="importedDatasets">Set of already imported dataset names.</param>
        /// <param name="importedLinkedServices">Set of already imported linked service names.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task ImportPipelineDependenciesAsync<T>(
            T client,
            JObject pipeline,
            HashSet<string> importedPipelines,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken) where T : IApiClient
        {
            JArray activities = pipeline.SelectToken("properties.activities") as JArray;
            if (activities == null) return;

            foreach (JObject activity in activities)
            {
                await ImportActivityAndSubtreeDependenciesAsync(
                    client,
                    activity,
                    importedPipelines,
                    importedDatasets,
                    importedLinkedServices,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Import an activity and all its nested activity dependencies.
        /// </summary>
        protected async Task ImportActivityAndSubtreeDependenciesAsync<T>(
            T client,
            JObject activity,
            HashSet<string> importedPipelines,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken) where T : IApiClient
        {
            // First handle the current activity
            await ImportActivityDatasetsAsync(client, activity, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
            await ImportReferencedPipelinesAsync(client, activity, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
            await ImportActivityLinkedServicesAsync(client, activity, importedLinkedServices, cancellationToken).ConfigureAwait(false);

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
                                await ImportActivityAndSubtreeDependenciesAsync(client, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                }
                var defaultActs = activity.SelectToken("typeProperties.defaultActivities") as JArray;
                if (defaultActs != null)
                {
                    foreach (JObject subAct in defaultActs.OfType<JObject>())
                    {
                        await ImportActivityAndSubtreeDependenciesAsync(client, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
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
                        await ImportActivityAndSubtreeDependenciesAsync(client, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                    }
                }
                var falseActs = activity.SelectToken("typeProperties.ifFalseActivities") as JArray;
                if (falseActs != null)
                {
                    foreach (JObject subAct in falseActs.OfType<JObject>())
                    {
                        await ImportActivityAndSubtreeDependenciesAsync(client, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
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
                        await ImportActivityAndSubtreeDependenciesAsync(client, subAct, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Import datasets referenced by an activity.
        /// </summary>
        protected async Task ImportActivityDatasetsAsync<T>(
            T client,
            JObject activity,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken) where T : IApiClient
        {
            // Handle Lookup activity dataset reference (not in inputs/outputs)
            string activityType = activity.SelectToken("type")?.ToString();
            if (activityType == "Lookup")
            {
                string lookupDatasetName = activity.SelectToken("typeProperties.dataset.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(lookupDatasetName))
                {
                    await ImportDatasetWithLinkedServiceAsync(
                        client,
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
                        await ImportDatasetWithLinkedServiceAsync(client, datasetName, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
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
                        await ImportDatasetWithLinkedServiceAsync(client, datasetName, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Import pipelines referenced by ExecutePipeline activities.
        /// </summary>
        protected async Task ImportReferencedPipelinesAsync<T>(
            T client,
            JObject activity,
            HashSet<string> importedPipelines,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken) where T : IApiClient
        {
            string activityType = activity.SelectToken("type")?.ToString();
            if (activityType == "ExecutePipeline")
            {
                string referencedPipelineName = activity.SelectToken("typeProperties.pipeline.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(referencedPipelineName))
                {
                    await ImportPipelineWithDependenciesAsync(client, referencedPipelineName, importedPipelines, importedDatasets, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Import linked services referenced directly by activities.
        /// </summary>
        protected async Task ImportActivityLinkedServicesAsync<T>(
            T client,
            JObject activity,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken) where T : IApiClient
        {
            // Web activities might reference linked services directly
            string activityType = activity.SelectToken("type")?.ToString();
            if (activityType == "WebActivity" || activityType == "WebHook")
            {
                string linkedServiceName = activity.SelectToken("typeProperties.linkedServiceName.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(linkedServiceName))
                {
                    await ImportLinkedServiceAsync(client, linkedServiceName, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                }
            }

            // Most of external activities reference linked services directly
            // Examples: AzureFunctionActivity, SqlServerStoredProcedure
            if (activityType == "AzureFunctionActivity"
                || activityType == "SqlServerStoredProcedure"
                || activityType == "AzureDataExplorerCommand"
                || activityType == "DataLakeAnalyticsScope")
            {
                string linkedServiceName = activity.SelectToken("linkedServiceName.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(linkedServiceName))
                {
                    await ImportLinkedServiceAsync(client, linkedServiceName, importedLinkedServices, cancellationToken).ConfigureAwait(false);

                    if (activityType == "DataLakeAnalyticsScope")
                    {
                        // DataLakeAnalyticsScope activity will have script linked service
                        string scriptLinkedServiceName = activity.SelectToken("typeProperties.scriptLinkedService.referenceName")?.ToString();
                        if (!string.IsNullOrEmpty(scriptLinkedServiceName))
                        {
                            await ImportLinkedServiceAsync(client, scriptLinkedServiceName, importedLinkedServices, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Import a dataset and its linked service.
        /// </summary>
        protected async Task ImportDatasetWithLinkedServiceAsync<T>(
            T client,
            string datasetName,
            HashSet<string> importedDatasets,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken) where T : IApiClient
        {
            if (importedDatasets.Contains(datasetName))
            {
                return; // Already imported
            }

            try
            {
                JObject dataset = await client.GetDatasetAsync(datasetName, cancellationToken).ConfigureAwait(false);
                this.upgradePackage.Datasets[datasetName] = dataset;
                importedDatasets.Add(datasetName);

                // Import the linked service referenced by this dataset
                string linkedServiceName = dataset.SelectToken("properties.linkedServiceName.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(linkedServiceName))
                {
                    await ImportLinkedServiceAsync(client, linkedServiceName, importedLinkedServices, cancellationToken).ConfigureAwait(false);
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
        protected async Task ImportLinkedServiceAsync<T>(
            T client,
            string linkedServiceName,
            HashSet<string> importedLinkedServices,
            CancellationToken cancellationToken) where T : IApiClient
        {
            if (importedLinkedServices.Contains(linkedServiceName))
            {
                return; // Already imported
            }

            try
            {
                JObject linkedService = await client.GetLinkedServiceAsync(linkedServiceName, cancellationToken).ConfigureAwait(false);
                this.upgradePackage.LinkedServices[linkedServiceName] = linkedService;
                importedLinkedServices.Add(linkedServiceName);
                Console.WriteLine($"Imported linked service: {linkedServiceName}" );
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
        protected JObject BuildResult()
        {
            JObject built = new JObject();
            built[FabricUpgradeProgress.ImportedResourcesKey] = UpgradeSerialization.ToJToken(upgradePackage);
            return built;
        }
    }
}
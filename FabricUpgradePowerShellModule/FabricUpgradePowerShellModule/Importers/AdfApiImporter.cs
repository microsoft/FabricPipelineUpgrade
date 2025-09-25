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
    public class AdfApiImporter : BaseApiImporter
    {
        private readonly string factoryName;
        private readonly string accessToken;
        private readonly string pipelineName;

        public AdfApiImporter(
            FabricUpgradeProgress progress,
            string subscriptionId,
            string resourceGroupName,
            string factoryName,
            string accessToken,
            string pipelineName,
            AlertCollector alerts)
            : base(progress, subscriptionId, resourceGroupName, alerts)
        {
            this.factoryName = factoryName;
            this.accessToken = accessToken;
            this.pipelineName = pipelineName;
        }

        /// <summary>
        /// Import ADF resources using REST APIs.
        /// </summary>
        /// <param name="includeUnusedResources">Whether to include datasets and linked services that are not used by any pipelines.</param>
        /// <param name="verbose">Whether to output detailed logging during the import process.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A FabricUpgradeProgress containing the imported resources.</returns>
        public async Task<FabricUpgradeProgress> ImportAsync(bool includeUnusedResources = true, bool verbose = false, CancellationToken cancellationToken = default)
        {
            AdfApiClient adfClient = null;
            try
            {
                adfClient = new AdfApiClient(subscriptionId, resourceGroupName, factoryName, accessToken);

                // Get Data Factory information
                JObject dataFactory = await adfClient.GetDataFactoryAsync(cancellationToken).ConfigureAwait(false);
                this.upgradePackage.AdfName = dataFactory["name"]?.ToString() ?? factoryName;
                this.upgradePackage.AdfRegion = dataFactory["location"]?.ToString();
                this.upgradePackage.SubscriptionId = this.subscriptionId;
                this.upgradePackage.ResourceGroupName = this.resourceGroupName;

                // If a specific pipeline is requested, import only that pipeline and its dependencies
                if (!string.IsNullOrEmpty(pipelineName))
                {
                    await ImportSpecificPipelineAsync(adfClient, pipelineName, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Import all resources
                    await ImportAllResourcesAsync(adfClient, includeUnusedResources, verbose, cancellationToken).ConfigureAwait(false);
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
    }
}
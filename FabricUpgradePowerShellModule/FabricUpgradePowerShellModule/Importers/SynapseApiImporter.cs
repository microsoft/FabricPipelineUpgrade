// Copyright (c) Microsoft. All rights reserved.
// SynapseApiImporter.cs
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Importers
{
    /// <summary>
    /// Imports Synapse workspace pipeline artifacts (pipelines, datasets, linked services, triggers) similar to ADF importer.
    /// </summary>
    public class SynapseApiImporter : BaseApiImporter
    {
        private readonly string workspaceName;
        private readonly string synapseToken; // Token for Synapse dev endpoint
        private readonly string armToken; // Token for ARM APIs
        private readonly string pipelineName; // Changed from pipelineResourceId to match Synapse naming

        public SynapseApiImporter(
            FabricUpgradeProgress progress,
            string subscriptionId,
            string resourceGroupName,
            string workspaceName,
            string synapseToken,
            string armToken,
            string pipelineName,
            AlertCollector alerts)
            : base(progress, subscriptionId, resourceGroupName, alerts)
        {
            this.workspaceName = workspaceName;
            this.synapseToken = synapseToken;
            this.armToken = armToken;
            this.pipelineName = pipelineName;
        }

        public async Task<FabricUpgradeProgress> ImportAsync(bool includeUnusedResources, bool verbose = false, CancellationToken cancellationToken = default)
        {
            SynapseApiClient client = null;
            try
            {
                // Pass separate tokens for ARM and Synapse endpoints
                client = new SynapseApiClient(subscriptionId, resourceGroupName, workspaceName, synapseToken, armToken);
                
                // Get workspace metadata from ARM using ARM token
                JObject workspace = await client.GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
                upgradePackage.AdfName = workspace["name"]?.ToString() ?? workspaceName; // reuse ADF property names for compatibility
                upgradePackage.AdfRegion = workspace["location"]?.ToString();
                upgradePackage.SubscriptionId = subscriptionId;
                upgradePackage.ResourceGroupName = resourceGroupName;

                if (!string.IsNullOrEmpty(pipelineName))
                {
                    await ImportSpecificPipelineAsync(client, pipelineName, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await ImportAllResourcesAsync(client, includeUnusedResources, verbose, cancellationToken).ConfigureAwait(false);
                }

                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                    Alerts = alerts.ToList(),
                    Result = BuildResult(),
                    Resolutions = progress.Resolutions
                };
            }
            catch (Exception ex)
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = alerts.ToList()
                }.WithAlert(new FabricUpgradeAlert()
                {
                    Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                    Details = $"Failed to import from Synapse workspace '{workspaceName}': {ex.Message}"
                });
            }
            finally
            {
                client?.Dispose();
            }
        }
    }
}

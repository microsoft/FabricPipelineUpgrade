// <copyright file="FabricExportMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Exceptions;
using FabricUpgradePowerShellModule.Exporters;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.ExportMachines
{
    public class FabricExportMachine : ExportMachine
    {
        private List<ResourceExporter> exporters = new List<ResourceExporter>();
        private JObject exportResults = new JObject();

        public FabricExportMachine(
            JObject toExport,
            string cluster,
            string workspaceId,
            string fabricToken,
            List<FabricUpgradeResolution> resolutions,
            AlertCollector alerts)
            : base(toExport, workspaceId, resolutions, alerts)
        {
            this.Cluster = cluster;
            this.FabricToken = fabricToken;
        }

        // The cluster (aka region) of the user's workspace.
        protected string Cluster { get; private set; }

        // The user's PowerBI AAD token.
        protected string FabricToken { get; private set; }

        /// <inheritdoc/>
        public override async Task<FabricUpgradeProgress> ExportAsync(CancellationToken cancellationToken)
        {
            try
            {
                this.BuildAllExporters();
                this.CheckAllExportersBeforeExport();
                JObject exportResult = await this.ExportAllExportersAsync(cancellationToken).ConfigureAwait(false);
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                    Alerts = this.Alerts.ToList(),
                    Result = exportResult,
                };
            }
            catch (UpgradeFailureException)
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.Alerts.ToList(),
                    Result = this.BuildResult(),
                };
            }
        }

        /// <summary>
        /// Build all of the Exporters described by the ExportObject.
        /// </summary>
        /// <exception cref="UpgradeFailureException"></exception>
        private void BuildAllExporters()
        {
            JArray toExport = (JArray)this.ExportObject.SelectToken(FabricUpgradeProgress.ExportableFabricResourcesKey);
            if (toExport == null)
            {
                this.Alerts.AddPermanentError("Cannot find fabricResources to export");
                throw new UpgradeFailureException("Construct");
            }

            foreach (var exportable in toExport)
            {
                ResourceExporter exporter = ResourceExporter.CreateResourceExporter(exportable, this);
                this.exporters.Add(exporter);
            }
        }

        /// <summary>
        /// Invoke CheckBeforeExports on all of the Exporters.
        /// </summary>
        /// <exception cref="UpgradeFailureException"></exception>
        private void CheckAllExportersBeforeExport()
        {
            foreach (ResourceExporter exporter in this.exporters)
            {
                exporter.CheckBeforeExports(this.Alerts);
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("PreCheck");
            }
        }

        /// <summary>
        /// Invoke ExportAsync() on all of the Exporters.
        /// </summary>
        /// <remarks>
        /// This method also collects the ID of each Fabric Resource, so that later
        /// Exporters can resolve those values before Creating/Updating their resources.
        /// </remarks>
        /// <param name="cancellationToken"/>
        /// <returns>A JObject that is the Result in the FabricUpgradeProgress returned to the client.</returns>
        /// <exception cref="UpgradeFailureException"></exception>
        private async Task<JObject> ExportAllExportersAsync(CancellationToken cancellationToken)
        {
            foreach (ResourceExporter exporter in this.exporters)
            {
                JObject uploadResult = await exporter.ExportAsync(
                    this.Cluster,
                    this.WorkspaceId,
                    this.FabricToken,
                    this.Alerts,
                    cancellationToken).ConfigureAwait(false);

                if (this.AlertsIndicateFailure())
                {
                    throw new UpgradeFailureException("Export");
                }

                this.exportResults[$"{exporter.Name}"] = uploadResult;

                // Keep track of the Fabric Resource ID of each Fabric Resource that we create,
                // so that later Exporters can resolve this value.
                var newResolution = new FabricUpgradeResolution()
                {
                    Type = FabricUpgradeResolution.ResolutionType.AdfResourceNameToFabricResourceId,
                    Key = $"{exporter.ResourceType}:{exporter.Name}",
                    Value = uploadResult.SelectToken("$.id")?.ToString(),
                };
                this.Resolutions.Add(newResolution);
            }

            return this.BuildResult();
        }

        /// <summary>
        /// Check the Alerts; if any are worse than a Warning, then the Export has failed.
        /// </summary>
        /// <returns>True if the Export has failed; False otherwise.</returns>
        private bool AlertsIndicateFailure()
        {
            return this.Alerts.Any(f => f.Severity != FabricUpgradeAlert.AlertSeverity.Warning);
        }

        private JObject BuildResult()
        {
            JObject result = new JObject();
            if (this.exportResults != null && this.exportResults.Count > 0)
            {
                result[FabricUpgradeProgress.ExportedFabricResourcesKey] = this.exportResults;
            }
            return result;
        }
    }
}

using FabricUpgradeCmdlet.Exporters;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.ExportMachines
{
    public class FabricExportMachine : ExportMachine
    {
        private List<ResourceExporter> exporters = new List<ResourceExporter>();
        private Dictionary<string, string> fabricResourceIds = new Dictionary<string, string>();

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

        protected string Cluster { get; private set; }

        protected string FabricToken { get; private set; }

        /// <inheritdoc/>
        public override async Task<FabricUpgradeProgress> ExportAsync()
        {
            try
            {
                this.BuildAllExporters();
                this.CheckAllExportersBeforeExport();
                return await this.ExportAllExportersAsync().ConfigureAwait(false);
            }
            catch (UpgradeFailureException)
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.Alerts.ToList(),
                };
            }
        }

        /// <inheritdoc/>
        public override JToken Link(
            string key,
            AlertCollector alerts)
        {
            if (key == "$workspace")
            {
                return this.WorkspaceId;
            }
           
            if (this.fabricResourceIds.ContainsKey(key))
            {
                return this.fabricResourceIds[key];
            }

            // The caller must add an alert.
            return null;
        }

        private void BuildAllExporters()
        {
            JArray toExport = (JArray)this.ExportObject.SelectToken("fabricResources");
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

        private async Task<FabricUpgradeProgress> ExportAllExportersAsync()
        {
            JObject results = new JObject();

            foreach (ResourceExporter exporter in this.exporters)
            {
                JObject uploadResult = await exporter.ExportAsync(
                    this.Cluster,
                    this.WorkspaceId,
                    this.FabricToken,
                    this.Alerts).ConfigureAwait(false);

                // TODO: Handle an error here! Throw an exception from ExportAsync.

                results[$"{exporter.Name}"] = uploadResult;
                this.fabricResourceIds[$"{exporter.ResourceType}:{exporter.Name}"] = uploadResult.SelectToken("$.id")?.ToString();
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("Export");
            }

            return new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                Alerts = this.Alerts.ToList(),
                Resolutions = this.Resolutions,
                Result = results,
            };
        }

        private bool AlertsIndicateFailure()
        {
            return this.Alerts.Any(f => f.Severity != FabricUpgradeAlert.FailureSeverity.Warning);
        }

    }
}

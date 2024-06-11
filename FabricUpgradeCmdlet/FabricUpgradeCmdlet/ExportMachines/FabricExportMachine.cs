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

        public override async Task<FabricUpgradeProgress> ExportAsync()
        {
            try
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

                return await this.DoExportAsync().ConfigureAwait(false);
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

        public override JToken Link(string key)
        {
            if (key == "$workspace")
            {
                return this.WorkspaceId;
            }

            if (!this.fabricResourceIds.ContainsKey(key))
            {
                // TODO!
            }

            return this.fabricResourceIds[key];
        }

        private async Task<FabricUpgradeProgress> DoExportAsync()
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

            return new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                Alerts = this.Alerts.ToList(),
                Resolutions = this.Resolutions,
                Result = results,
            };
        }
    }
}

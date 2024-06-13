using FabricUpgradeCmdlet.ExportMachines;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Exporters
{
    public class ConnectionExporter : ResourceExporter
    {
        private readonly ConnectionExportInstruction exportInstruction;
        private string connectionId;

        public ConnectionExporter(
            JToken toExport,
            FabricExportMachine machine)
            : base(toExport, FabricUpgradeResourceTypes.Connection, machine)
        {
            this.exportInstruction = ConnectionExportInstruction.FromJToken(toExport);
            this.Name = this.exportInstruction.ResourceName;
        }

        public override void CheckBeforeExports(
            AlertCollector alerts)
        {
            base.CheckBeforeExports(alerts);

            foreach (FabricExportResolve resolve in this.exportInstruction.Resolves)
            {
                var resolution = this.Machine.Resolve(resolve.Type, resolve.Key, alerts);
                if (resolution == null)
                {
                    alerts.AddMissingResolutionAlert(
                        resolve.Type,
                        resolve.Key,
                        resolve.Hint);
                }
            }
        }

        public override Task<JObject> ExportAsync(
            string cluster,
            string workspace,
            string fabricToken,
            AlertCollector alerts)
        {
            // We do not actually export a connection.
            // We use the resolutions to pretend.

            this.ResolveResolutions(alerts);

            return Task.FromResult(this.exportInstruction.Export);
        }

        private void ResolveResolutions(AlertCollector alerts)
        {
            foreach (FabricExportResolve resolve in this.exportInstruction.Resolves)
            {
                var resolution = this.Machine.Resolve(resolve.Type, resolve.Key, alerts);
                this.exportInstruction.Export.SelectToken(resolve.TargetPath).Replace(resolution);
            }
        }

    }
}

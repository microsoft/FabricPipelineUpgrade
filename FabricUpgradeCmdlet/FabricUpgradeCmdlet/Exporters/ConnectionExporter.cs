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
        private readonly PipelineExportInstruction exportInstruction;
        private string connectionId;

        public ConnectionExporter(
            JObject toExport,
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

            this.connectionId = this.Machine.Resolve(FabricUpgradeResolution.ResolutionType.LinkedServiceToConnection, this.Name, alerts);
            if (this.connectionId == null)
            {
                alerts.AddMissingResolutionAlert(
                    FabricUpgradeResolution.ResolutionType.LinkedServiceToConnection,
                    this.Name,
                    new FabricUpgradeConnectionHint()
                    {
                        // TODO: Build a connection hint
                    });
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

            JObject result = new JObject();
            result["id"] = this.connectionId;
            return Task.FromResult(result);
        }
    }
}

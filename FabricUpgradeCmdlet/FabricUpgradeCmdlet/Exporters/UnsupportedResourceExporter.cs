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
    public class UnsupportedResourceExporter : ResourceExporter
    {
        private readonly FabricExportInstruction exportInstruction;

        public UnsupportedResourceExporter(
            JToken toExport,
            FabricExportMachine machine)
            : base(toExport, FabricUpgradeResourceTypes.Connection, machine)
        {
            this.exportInstruction = FabricExportInstruction.FromJToken(toExport);
            this.Name = this.exportInstruction.ResourceName;
        }

        public override void CheckBeforeExports(AlertCollector alerts)
        {
            base.CheckBeforeExports(alerts);

            alerts.AddUnsupportedResourceAlert($"Cannot upgrade LinkedService '{this.Name}' because its Type is '{this.ResourceType}'.");
        }
    }
}

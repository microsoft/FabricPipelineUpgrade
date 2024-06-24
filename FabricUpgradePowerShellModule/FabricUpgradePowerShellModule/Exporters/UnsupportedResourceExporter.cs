// <copyright file="UnsupportedResourceExporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.ExportMachines;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Exporters
{
    /// <summary>
    /// If, somehow, the ExportMachine tries to export an unrecognized Resource,
    /// the ResourceExporter factory will make one of these.
    /// This object will throw an Error in CheckBeforeExports(), thereby terminating the
    /// export before it begins.
    /// </summary>
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

        /// <inheritdoc/>
        public override void CheckBeforeExports(AlertCollector alerts)
        {
            base.CheckBeforeExports(alerts);

            alerts.AddUnsupportedResourceAlert($"Cannot upgrade LinkedService '{this.Name}' because its Type is '{this.ResourceType}'.");
        }
    }
}

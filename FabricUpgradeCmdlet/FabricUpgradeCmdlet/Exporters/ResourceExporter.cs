// <copyright file="ResourceExporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.ExportMachines;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Exporters
{
    public class ResourceExporter
    {
        protected ResourceExporter(
            JToken exportToken,
            FabricUpgradeResourceTypes resourceType,
            FabricExportMachine machine)
        {
            this.ExportObject = (JObject)exportToken;
            this.ResourceType = resourceType;
            this.Machine = machine;

            // TODO: Compute name!
        }

        public string Name { get; protected set; }

        public FabricUpgradeResourceTypes ResourceType { get; private set; }

        protected JObject ExportObject { get; private set; }

        protected FabricExportMachine Machine { get; private set; }

        public static ResourceExporter CreateResourceExporter(JToken exportable, FabricExportMachine machine)
        {
            FabricExportInstruction instruction = FabricExportInstruction.FromJToken(exportable);

            return instruction.ResourceType switch
            {
                FabricUpgradeResourceTypes.Connection => new ConnectionExporter(exportable, machine),
                FabricUpgradeResourceTypes.DataPipeline => new PipelineExporter(exportable, machine),
                _ => new UnsupportedResourceExporter(exportable, machine),
            };
        }

        public virtual void CheckBeforeExports(AlertCollector alerts)
        {
        }

        public virtual Task<JObject> ExportAsync(
            string cluster,
            string workspace,
            string fabricToken,
            AlertCollector alerts,
            CancellationToken cancellationToken)
        {
            return null;
        }
    }
}

using FabricUpgradeCmdlet.ExportMachines;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Exporters
{
    public class ResourceExporter
    {
        protected ResourceExporter(
            JObject exportObject,
            FabricUpgradeResourceTypes resourceType,
            FabricExportMachine machine)
        {
            this.ExportObject = exportObject;
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
                FabricUpgradeResourceTypes.DataPipeline => new PipelineExporter((JObject)exportable, machine),
                _ => null,
            };
        }

        public virtual void CheckBeforeExports(AlertCollector alerts)
        {
        }

        public virtual Task<JObject> ExportAsync(
            string cluster,
            string workspace,
            string fabricToken,
            AlertCollector alerts)
        {
            return null;
        }
    }
}

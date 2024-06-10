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
    public class PipelineExporter : ResourceExporter
    {
        private readonly PipelineExportInstruction exportInstruction;
        
        public PipelineExporter(
            JObject toExport,
            FabricExportMachine machine)
            : base(toExport, FabricUpgradeResourceTypes.DataPipeline, machine)
        {
            this.exportInstruction = PipelineExportInstruction.FromJToken(toExport);
            this.Name = this.exportInstruction.Pipeline.SelectToken("$.name")?.ToString();
        }

        public override async Task<JObject> ExportAsync(
            string cluster,
            string workspace,
            string fabricToken,
            AlertCollector alerts)
        {
            this.ResolveLinks();

            string uploadResult = await new PublicApiClient().UploadPipelineAsync(
                this.exportInstruction.Pipeline,
                cluster,
                workspace,
                fabricToken).ConfigureAwait(false);

            return JObject.Parse(uploadResult);
        }

        private void ResolveLinks()
        {
            foreach (FabricExportLink link in this.exportInstruction.Links)
            {
                JToken value = this.Machine.Link(link.From);
                string k = exportInstruction.Pipeline.ToString(Newtonsoft.Json.Formatting.Indented);
                JToken target = exportInstruction.Pipeline.SelectToken(link.To);
                this.exportInstruction.Pipeline.SelectToken(link.To).Replace(value);
            }
        }
    }
}

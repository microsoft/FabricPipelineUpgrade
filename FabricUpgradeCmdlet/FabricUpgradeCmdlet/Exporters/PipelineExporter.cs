// <copyright file="PipelineExporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.ExportMachines;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Exporters
{
    public class PipelineExporter : ResourceExporter
    {
        private readonly PipelineExportInstruction exportInstruction;

        public PipelineExporter(
            JToken toExport,
            FabricExportMachine machine)
            : base(toExport, FabricUpgradeResourceTypes.DataPipeline, machine)
        {
            this.exportInstruction = PipelineExportInstruction.FromJToken(toExport);
            this.Name = this.exportInstruction.ResourceName;
        }

        public override void CheckBeforeExports(AlertCollector alerts)
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

        public override async Task<JObject> ExportAsync(
            string cluster,
            string workspace,
            string fabricToken,
            AlertCollector alerts)
        {
            this.ResolveLinks(alerts);
            this.ResolveResolutions(alerts);

            string uploadResult = await new PublicApiClient().UploadPipelineAsync(
                this.exportInstruction.Export,
                cluster,
                workspace,
                fabricToken).ConfigureAwait(false);

            return JObject.Parse(uploadResult);
        }

        private void ResolveLinks(AlertCollector alerts)
        {
            foreach (FabricExportLink link in this.exportInstruction.Links)
            {
                JToken value = this.Machine.Link(link.From, alerts);
                this.exportInstruction.Export.SelectToken(link.TargetPath).Replace(value);
            }
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

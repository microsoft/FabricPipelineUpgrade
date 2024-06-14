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
            string workspaceId,
            string fabricToken,
            AlertCollector alerts,
            CancellationToken cancellationToken)
        {
            this.ResolveLinks(alerts);
            this.ResolveResolutions(alerts);

            string exportResult = await new PublicApiClient(cluster, workspaceId, fabricToken)
                .CreateOrUpdateArtifactAsync(
                    FabricUpgradeResourceTypes.DataPipeline,
                    this.exportInstruction.ResourceName,
                    this.exportInstruction.ResourceDescription,
                    this.exportInstruction.Export,
                    cancellationToken).ConfigureAwait(false);

            return JObject.Parse(exportResult);
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

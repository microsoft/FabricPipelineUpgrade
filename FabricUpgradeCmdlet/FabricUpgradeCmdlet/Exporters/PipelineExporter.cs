// <copyright file="PipelineExporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.ExportMachines;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Exporters
{
    /// <summary>
    /// This class creates/updates a DataPipeline Item.
    /// </summary>
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

        /// <inheritdoc/>
        public override void CheckBeforeExports(AlertCollector alerts)
        {
            base.CheckBeforeExports(alerts);

            // This DataPipeline (technically, its Activities) may need to resolve certain
            // connection IDs. If it cannot, then fail!
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

        /// <inheritdoc/>
        public override async Task<JObject> ExportAsync(
            string cluster,
            string workspaceId,
            string fabricToken,
            AlertCollector alerts,
            CancellationToken cancellationToken)
        {
            this.ResolveLinks(alerts);
            this.ResolveResolutions(alerts);

            try
            {
                string exportResult = await new PublicApiClient(cluster, workspaceId, fabricToken)
                    .CreateOrUpdateArtifactAsync(
                        FabricUpgradeResourceTypes.DataPipeline,
                        this.exportInstruction.ResourceName,
                        this.exportInstruction.ResourceDescription,
                        this.exportInstruction.Export,
                        cancellationToken).ConfigureAwait(false);

                return JObject.Parse(exportResult);
            }
            catch (Exception ex)
            {
                alerts.AddPermanentError(ex.Message);
                return new JObject();
            }
        }

        /// <summary>
        /// Find the IDs of the Fabric Resources that this Pipeline references.
        /// </summary>
        /// <remarks>
        /// The Execute/InvokePipeline Activity needs the ID of another Pipeline.
        /// All sorts of Activities need the ID of a Connection.
        /// </remarks>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void ResolveLinks(AlertCollector alerts)
        {
            foreach (FabricExportLink link in this.exportInstruction.Links)
            {
                JToken value = this.Machine.Link(link.From, alerts);
                this.exportInstruction.Export.SelectToken(link.TargetPath).Replace(value);
            }
        }

        /// <summary>
        /// Find the IDs of the Connections that this Pipeline references.
        /// </summary>
        /// <remarks>
        /// The Web Activity directly references a Connection, so that reference needs to be resolved.
        /// </remarks>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
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

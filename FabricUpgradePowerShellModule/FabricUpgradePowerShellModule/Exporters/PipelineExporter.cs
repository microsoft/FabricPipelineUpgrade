// <copyright file="PipelineExporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.ExportMachines;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Exporters
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
            foreach (FabricExportResolveStep resolve in this.exportInstruction.Resolves)
            {
                if (resolve.Type == FabricUpgradeResolution.ResolutionType.AdfResourceNameToFabricResourceId)
                {
                    // We do not expect this resolution to exist yet.
                    // This resolution is created when the "from" Fabric Resource is exported.
                    continue;
                }

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
            this.ExecuteResolveSteps(alerts);

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
        /// Find the Resource IDs of other Fabric Resources, and insert them into the 
        /// export payload before Creating/Updating this Fabric Resource.
        /// </summary>
        /// <remarks>
        /// The Execute/Invoke Pipeline references another Pipeline, so that reference needs to 
        /// be resolved, _after_ that other Pipeline has been Created/Updated.
        ///
        /// A CopyActivity (e.g.) references Dataset(s), which reference LinkedServices, to the
        /// Pipeline needs to find the FabricResourceID of the corresponding Fabric Connection.
        ///
        /// The Web Activity directly references a Connection, so that reference needs to be resolved.
        /// </remarks>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void ExecuteResolveSteps(AlertCollector alerts)
        {
            foreach (FabricExportResolveStep resolve in this.exportInstruction.Resolves)
            {
                var resolution = this.Machine.Resolve(resolve.Type, resolve.Key, alerts);
                this.exportInstruction.Export.SelectToken(resolve.TargetPath).Replace(resolution);
            }
        }
    }
}

// <copyright file="ConnectionExporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;
using FabricUpgradePowerShellModule.ExportMachines;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;

namespace FabricUpgradePowerShellModule.Exporters
{
    /// <summary>
    /// This class creates/updates a Connection Item.
    /// </summary>
    /// <remarks>
    /// This is a little complicated, so bear with me for a little while.
    /// 
    /// This class does not _actually_ create or update a Fabric Connection Item.
    /// That capability did not exist when this code was written.
    /// (Or, if it did exist, this capability was very very new, and not quite ready).
    /// 
    /// Therefore, we just _pretend_ to create the Connection.
    /// What we are _really_ doing is getting a Resolution from the client and
    /// setting the Fabric Resource ID for this Item to what we find in the Resolution.
    /// This way, the Pipeline, etc., can act as though this Connection was created and
    /// has a Fabric Resource ID.
    ///
    /// Someday, when we _can_ actually create/update a Fabric Connection via the PublicAPI,
    /// the Pipeline will continue to work exactly the same way.
    /// </remarks>
    public class ConnectionExporter : ResourceExporter
    {
        private readonly ConnectionExportInstruction exportInstruction;

        public ConnectionExporter(
            JToken toExport,
            FabricExportMachine machine)
            : base(toExport, FabricUpgradeResourceTypes.Connection, machine)
        {
            this.exportInstruction = ConnectionExportInstruction.FromJToken(toExport);
            this.Name = this.exportInstruction.ResourceName;
        }

        /// <inheritdoc/>
        public override void CheckBeforeExports(
            AlertCollector alerts)
        {
            base.CheckBeforeExports(alerts);

            // Make sure that this Connection has a Resolution from the client.
            foreach (FabricExportResolveStep resolve in this.exportInstruction.Resolves)
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
        public override Task<JObject> ExportAsync(
            string cluster,
            string workspace,
            string fabricToken,
            AlertCollector alerts,
            CancellationToken cancellationToken)
        {
            // We do not actually export a connection.
            // We use the Resolutions to pretend.

            this.ResolveResolutions(alerts);

            return Task.FromResult(this.exportInstruction.Export);
        }

        /// <summary>
        /// Find the ID of the Connection that this Connection references.
        /// </summary>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void ResolveResolutions(AlertCollector alerts)
        {
            foreach (FabricExportResolveStep resolve in this.exportInstruction.Resolves)
            {
                var resolution = this.Machine.Resolve(resolve.Type, resolve.Key, alerts);
                this.exportInstruction.Export.SelectToken(resolve.TargetPath).Replace(resolution);
            }
        }
    }
}

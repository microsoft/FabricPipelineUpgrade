// <copyright file="ResourceExporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.ExportMachines;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Exporters
{
    /// <summary>
    /// A base class for all Exporters.
    /// </summary>
    public class ResourceExporter
    {
        protected ResourceExporter(
            JToken exportToken,
            FabricUpgradeResourceTypes resourceType,
            FabricExportMachine machine)
        {
            this.ExportToken = exportToken;
            this.ResourceType = resourceType;
            this.Machine = machine;
        }

        // The DisplayName of the Fabric Resource that this Exporter exports.
        public string Name { get; protected set; }

        // The original JToken that is to be exported.
        protected JToken ExportToken { get; private set; }

        // The ResourceType (DataPipeline, etc.) of the Fabric Resource that this Exporter exports.
        public FabricUpgradeResourceTypes ResourceType { get; private set; }

        // The FabricExportMachine that is performing the overall Export process.
        // This allows the individual Exporters to access some configuration, etc.
        protected FabricExportMachine Machine { get; private set; }

        /// <summary>
        /// A factory to create individual Exporters.
        /// </summary>
        /// <param name="exportable">The JToken that describes the ExportInstruction.</param>
        /// <param name="machine">The FabricExportMachine that is performing the overall Export process.</param>
        /// <returns>A ResourceExporter.</returns>
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

        /// <summary>
        /// Before beginning the Export process, verify that the Export can work.
        /// This step usually checks if the connections are all resolved; it can check other things.
        /// </summary>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        public virtual void CheckBeforeExports(AlertCollector alerts)
        {
        }

        /// <summary>
        /// Actually perform the Export!
        /// </summary>
        /// <param name="cluster">The cluster (aka region) of the user's workspace.</param>
        /// <param name="workspace">The user's workspace, into which the pipeline, etc., will be exported.</param>
        /// <param name="fabricToken">The PowerBI AAD token to authenticate/authorize access to the workspace.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <param name="cancellationToken"/>
        /// <returns>The response from the Create/UpdateItem PublicAPI call.</returns>
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

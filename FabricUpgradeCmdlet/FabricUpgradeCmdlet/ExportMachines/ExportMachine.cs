// <copyright file="ExportMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.ExportMachines
{
    public class ExportMachine
    {
        public ExportMachine(
            JObject exportObject,
            string workspaceId,
            List<FabricUpgradeResolution> resolutions,
            AlertCollector alerts)
        {
            this.ExportObject = exportObject;
            this.WorkspaceId = workspaceId;
            this.Resolutions = resolutions ?? new List<FabricUpgradeResolution>();
            this.Alerts = alerts;
        }

        // The manually constructed Resolutions, that map ADF LinkedServices to Fabric ConnectionIDs.
        protected List<FabricUpgradeResolution> Resolutions { get; private set; }

        // The Object being exported.
        protected JObject ExportObject { get; private set; } = new JObject();

        // The WorkspaceID.
        protected string WorkspaceId { get; private set; }

        /// <summary>
        /// This AlertCollector accumulates the Alerts generated during the Upgrade process.
        /// </summary>
        protected AlertCollector Alerts { get; private set; }

        /// <summary>
        /// Perform the actual Export process.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <returns>The FabricUpgradeProgress object that is returned to the client.</returns>
        public virtual Task<FabricUpgradeProgress> ExportAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new FabricUpgradeProgress());
        }

        /// <summary>
        /// Find the manually constructed Resolution that matches what an Exporter needs to
        /// populate its "connection ID" field(s).
        /// </summary>
        /// <param name="type">The Type of resolution to find.</param>
        /// <param name="key">The Key ('name') of the resolution to find.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns></returns>
        public string Resolve(
            FabricUpgradeResolution.ResolutionType type,
            string key,
            AlertCollector alerts)
        {
            FabricUpgradeResolution matchingResolution = this.Resolutions
                .Where(r => r.Type == type && r.Key == key)
                .FirstOrDefault();

            if (matchingResolution == null)
            {
                return null;
            }

            return matchingResolution.Value;
        }

        /// <summary>
        /// Find and return the ID of a previously exported Fabric Resource.
        /// If key is '$workspace', then return the workspace ID.
        /// </summary>
        /// <remarks>
        /// The Execute/InvokePipeline Activity needs the ID of a previously exported Pipeline.
        /// All sorts of Activities need the ID of a Connection (which we _pretend_ to export!).
        /// </remarks>
        /// <param name="key">The (display) name of the Fabric Resource (or $workspace).</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>The corresponding ID.</returns>
        public virtual JToken Link(
            string key,
            AlertCollector alerts)
        {
            return string.Empty;
        }
    }
}

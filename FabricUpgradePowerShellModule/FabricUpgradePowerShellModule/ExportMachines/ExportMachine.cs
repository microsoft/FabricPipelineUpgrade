// <copyright file="ExportMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.ExportMachines
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
        /// populate those field(s) that it cannot populate until the Export phase.
        /// </summary>
        /// <remarks>
        /// These fields include the Fabric Resource IDs of a Connection, which must (currently)
        /// be specified manually by the user.
        /// These fields include the Fabric Resource IDs of other Pipelines, which we do not know
        /// until we Create/Update those Pipelines.
        /// </remarks>
        /// <param name="type">The Type of resolution to find.</param>
        /// <param name="key">The Key ('name') of the resolution to find.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns></returns>
        public string Resolve(
            FabricUpgradeResolution.ResolutionType type,
            string key,
            AlertCollector alerts)
        {
            if (type == FabricUpgradeResolution.ResolutionType.WorkspaceId)
            {
                return this.WorkspaceId;
            }

            FabricUpgradeResolution matchingResolution = this.Resolutions
                .Where(r => r.Type == type && r.Key == key)
                .FirstOrDefault();

            return matchingResolution?.Value;
        }
    }
}

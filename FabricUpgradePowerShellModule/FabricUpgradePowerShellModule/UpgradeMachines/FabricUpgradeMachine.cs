// <copyright file="FabricUpgradeMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Upgraders;
using FabricUpgradePowerShellModule.Utilities;

namespace FabricUpgradePowerShellModule.UpgradeMachines
{
    /// <summary>
    /// The base class for FabricUpgradeMachines.
    /// </summary>
    public abstract class FabricUpgradeMachine : IFabricUpgradeMachine
    {
        protected FabricUpgradeMachine(
            List<FabricUpgradeResolution> resolutions,
            AlertCollector alerts)
        {
            this.Resolutions = resolutions;
            this.Alerts = alerts ?? new AlertCollector();
        }

        /// <summary>
        /// This AlertCollector accumulates the Alerts generated during the Upgrade process.
        /// </summary>
        protected AlertCollector Alerts { get; set; }

        protected List<FabricUpgradeResolution> Resolutions { get; set; } = new List<FabricUpgradeResolution>();

        /// <summary>
        /// This list contains the PipelineUpgrader(s), the Dataset Upgraders, and the LinkedService Upgraders.
        /// </summary>
        protected List<Upgrader> Upgraders { get; set; } = new List<Upgrader>();

        /// <inheritdoc/>
        public abstract FabricUpgradeProgress Upgrade();
    }
}

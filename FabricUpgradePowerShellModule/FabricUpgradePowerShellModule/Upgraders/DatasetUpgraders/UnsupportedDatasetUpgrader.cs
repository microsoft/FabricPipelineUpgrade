// <copyright file="UnsupportedDatasetUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.DatasetUpgraders
{
    /// <summary>
    /// This class handles an unsupported Dataset type by failing in the Compile step.
    /// </summary>
    public class UnsupportedDatasetUpgrader : DatasetUpgrader
    {
        public UnsupportedDatasetUpgrader(
            JToken adfDatasetToken,
            IFabricUpgradeMachine machine)
            : base(adfDatasetToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            // Here, we add an Alert that will prevent the Upgrader from continuing.
            alerts.AddUnsupportedResourceAlert($"Cannot upgrade Dataset '{this.Path}' because its Type is '{this.AdfModel.Properties.DatasetType}'.");
        }
    }
}

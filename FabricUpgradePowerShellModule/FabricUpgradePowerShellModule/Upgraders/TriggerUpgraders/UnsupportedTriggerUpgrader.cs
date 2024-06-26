// <copyright file="UnsupportedLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.TriggerUpgraders
{

    /// <summary>
    /// This class handles an unsupported Trigger type by generating a Warning in the Compile step.
    /// </summary>
    public class UnsupportedTriggerUpgrader : TriggerUpgrader
    {
        public UnsupportedTriggerUpgrader(
            JToken adfTriggerToken,
            IFabricUpgradeMachine machine)
            : base(adfTriggerToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            // Here, we add an Alert that will prevent the Upgrader from continuing.
            alerts.AddWarning($"Cannot upgrade the {this.TriggerType} '{this.Name}'; Fabric Upgrade does not yet support upgrading Triggers");
        }
    }
}

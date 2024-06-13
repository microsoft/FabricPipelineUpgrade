// <copyright file="UnsupportedLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders
{
    public class UnsupportedLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        public UnsupportedLinkedServiceUpgrader(
            JToken adfLinkedServiceToken,
            IFabricUpgradeMachine machine)
            : base(adfLinkedServiceToken, machine)
        {
        }

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            // Here, we add an Alert that will prevent the Upgrader from continuing.
            alerts.AddUnsupportedResourceAlert($"Cannot upgrade LinkedService '{this.Path}' because its Type is '{this.LinkedServiceType}'");
        }
    }
}

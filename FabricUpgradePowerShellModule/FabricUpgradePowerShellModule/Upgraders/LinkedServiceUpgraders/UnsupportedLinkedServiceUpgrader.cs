// <copyright file="UnsupportedLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{

    /// <summary>
    /// This class handles an unsupported LinkedService type by failing in the Compile step.
    /// </summary>
    public class UnsupportedLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        public UnsupportedLinkedServiceUpgrader(
            JToken adfLinkedServiceToken,
            IFabricUpgradeMachine machine)
            : base(adfLinkedServiceToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            // By pass the Azure Key Vault Linked Service as it is not used directly in any activity or dataset
            // only used in other Linked Services to retrieve secrets.
            if (this.LinkedServiceType == LinkedServiceTypes.AzureKeyVault)
            {
                return;
            }

            // Here, we add an Alert that will prevent the Upgrader from continuing.
            alerts.AddUnsupportedResourceAlert($"Cannot upgrade LinkedService '{this.Name}' because its Type is '{this.LinkedServiceType}'.");
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
           string symbolName,
           Dictionary<string, JToken> parameterAssignments,
           AlertCollector alerts)
        {
            return Symbol.ReadySymbol(null);
        }
    }
}

// <copyright file="AzureBlobStorageLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{
    /// <summary>
    /// This class handles the Upgrade for an AzureBlob LinkedService.
    /// </summary>
    public class AzureBlobStorageLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        private string AccountNameKey = "AccountName";
        private string EndpointSuffixKey = "EndpointSuffix";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
        };

        // A dictionary parsed from the connectionSettings property.
        private Dictionary<string, JToken> connectionSettings;

        public AzureBlobStorageLinkedServiceUpgrader(
            JToken adfLinkedServiceToken,
            IFabricUpgradeMachine machine)
            : base(adfLinkedServiceToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            JToken connectionStringToken = this.AdfResourceToken.SelectToken(AdfConnectionStringPath);

            if (connectionStringToken == null)
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Path}' because its ConnectionString is missing.");
                return;
            }
            else if (connectionStringToken.Type != JTokenType.String)
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Path}' because its ConnectionString is not a string.");
                return;
            }
            else
            {
                this.connectionSettings = this.BuildConnectionSettings(connectionStringToken.ToString());
            }

            this.CheckForExpressionInConnectionSettings(this.connectionSettings, AccountNameKey, alerts);
            this.CheckForExpressionInConnectionSettings(this.connectionSettings, EndpointSuffixKey, alerts);
        }

        /// <inheritdoc/>
        public override void PreSort(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreSort(allUpgraders, alerts);
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <inheritdoc/>
        protected override FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            this.connectionSettings.TryGetValue(AccountNameKey, out JToken accountName);

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(accountName?.ToString() ?? "unknown");
        }
    }
}

// <copyright file="AzureDataLakeStorageGen2LinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{
    /// <summary>
    /// This class handles the Upgrade for an Azure Data Lake Storage Gen2.
    /// </summary>
    public class AzureDataLakeStorageGen2LinkedServiceUpgrader : LinkedServiceUpgrader
    {
        private string AccountUrlKey = "AccountUrl";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
        };

        // A dictionary parsed from the connectionSettings property.
        private Dictionary<string, JToken> connectionSettings;

        public AzureDataLakeStorageGen2LinkedServiceUpgrader(
            JToken adfLinkedServiceToken,
            IFabricUpgradeMachine machine)
            : base(adfLinkedServiceToken, machine)
        {
        }

        private const string AdfUrlPath = "properties.typeProperties.url";

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            JToken accountUrl = this.AdfResourceToken.SelectToken(AdfUrlPath);
            if (accountUrl == null)
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Path}' because its Url is missing.");
            }
            this.connectionSettings = new Dictionary<string, JToken> { };
            this.connectionSettings[AccountUrlKey] = accountUrl;
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
            this.connectionSettings.TryGetValue(AccountUrlKey, out JToken accountUrl);

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(accountUrl?.ToString() ?? "unknown");
        }
    }
}

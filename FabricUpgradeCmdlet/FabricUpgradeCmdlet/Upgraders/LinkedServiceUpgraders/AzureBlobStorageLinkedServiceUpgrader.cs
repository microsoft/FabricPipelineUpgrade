// <copyright file="AzureBlobStorageLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders
{
    /// <summary>
    /// This class handles the Upgrade for an AzureBlob LinkedService.
    /// </summary>
    public class AzureBlobStorageLinkedServiceUpgrader : LinkedServiceUpgrader
    {
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
            }
            else if (connectionStringToken.Type != JTokenType.String)
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Path}' because its ConnectionString is not a string.");
            }
            else
            {
                this.connectionSettings = this.BuildConnectionSettings(connectionStringToken.ToString());
            }

            // TODO: Verify that AccountName and EndpointSuffix are not expressions.
        }

        /// <inheritdoc/>
        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreLink(allUpgraders, alerts);
        }

        /// <inheritdoc/>
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            return base.ResolveExportedSymbol(symbolName, parameters, alerts);
        }

        /// <inheritdoc/>
        protected override FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            this.connectionSettings.TryGetValue("AccountName", out JToken accountName);

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(accountName?.ToString() ?? "unknown");
        }
    }
}

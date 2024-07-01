// <copyright file="TemplateConnectionStringLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{
    /// <summary>
    /// This class is a template for a LinkedService Upgrader
    /// that uses a ConnectionString to describe its data source.
    /// </summary>
    public class TemplateConnectionStringLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        // These strings appear in the "connectionString" for this LinkedService.
        // We check these values to ensure that they are not expressions, and
        // to build a connection hint.
        // Please replace this list with the property names you need.
        private string AccountNameKey = "AccountName";
        private string EndpointSuffixKey = "EndpointSuffix";

        // If you support upgrading only those LinkedServices that contain these properties.
        private readonly List<string> requiredAdfProperties = new List<string>
        {
        };

        // A dictionary parsed from the connectionSettings property.
        private Dictionary<string, JToken> connectionSettings;

        public TemplateConnectionStringLinkedServiceUpgrader(
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
            // TODO: Adjust this to produce a connectionHint for your LinkedService!
            this.connectionSettings.TryGetValue(AccountNameKey, out JToken accountName);

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(accountName?.ToString() ?? "unknown");
        }
    }
}

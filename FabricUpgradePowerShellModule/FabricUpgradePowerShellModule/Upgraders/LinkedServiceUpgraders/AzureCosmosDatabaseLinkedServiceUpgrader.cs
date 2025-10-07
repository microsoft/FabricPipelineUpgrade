// <copyright file="AzureCosmosDatabaseLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{
    /// <summary>
    /// This class is for a AzureCosmosDatabaseLinkedServiceUpgrader Upgrader
    /// that uses a ConnectionString to describe its data source.
    /// </summary>
    public class AzureCosmosDatabaseLinkedServiceUpgrader : LinkedServiceUpgrader
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

        public AzureCosmosDatabaseLinkedServiceUpgrader(
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
            // Database for datasets should be taken from the connection string.
            // The dataset JSON does not include the database; linked-service connectionString contains it.
            if (symbolName == Symbol.CommonNames.LinkedServiceDatabaseName)
            {
                if (this.connectionSettings != null &&
                    this.connectionSettings.TryGetValue("Database", out JToken connDb) &&
                    connDb != null)
                {
                    return Symbol.ReadySymbol(connDb);
                }

                // Not found — return Missing so callers (datasets/export) can surface a clear resolution/alert.
                return Symbol.MissingSymbol();
            }

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <inheritdoc/>
        protected override FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            // Safely obtain accountName from connectionSettings — avoid unassigned variable warning.
            JToken accountName = null;
            if (this.connectionSettings != null)
            {
                this.connectionSettings.TryGetValue(AccountNameKey, out accountName);
            }

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(accountName?.ToString() ?? "unknown");
        }
    }
}

// <copyright file="AzureSqlDatabaseLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders
{
    public class AzureSqlDatabaseLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        // The key in the connection string for the database value.
        // Datasets may use this during the construction of datasetSettings.
        public const string DatabaseKey = "initial catalog";

        private const string DatasourceKey = "data source";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
        };

        public AzureSqlDatabaseLinkedServiceUpgrader(
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

            if (!this.ConnectionSettings.TryGetValue(DatabaseKey, out JToken databaseToken))
            {
                alerts.AddPermanentError($"LinkedService property {this.Path}.connectionSettings.'{DatabaseKey}' must not be null.");
            }
            else if (databaseToken.Type !=  JTokenType.String)
            {
                alerts.AddPermanentError($"LinkedService property {this.Path}.connectionSettings.'{DatabaseKey}' must be a string.");
            }
            else if (databaseToken.ToString().StartsWith('@'))
            {
                alerts.AddPermanentError($"LinkedService property {this.Path}.connectionSettings.'{DatabaseKey}' must not be an expression.");
            }
        }

        /// <inheritdoc/>
        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreLink(allUpgraders, alerts);
        }

        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                return base.ResolveExportedSymbol(Symbol.CommonNames.ExportLinks, alerts);
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
        }

        protected override FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            this.ConnectionSettings.TryGetValue(DatasourceKey, out JToken accountName);

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(accountName?.ToString() ?? "unknown");
        }
    }
}

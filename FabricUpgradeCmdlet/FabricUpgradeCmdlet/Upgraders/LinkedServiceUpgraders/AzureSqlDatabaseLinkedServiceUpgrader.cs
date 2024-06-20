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
        public const string DatabaseKey = "initial catalog";
        private const string DatasourceKey = "data source";
        private const string ServerNamePath = "properties.typeProperties.server";
        private const string DatabaseNamePath = "properties.typeProperties.database";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            ServerNamePath,
            DatabaseNamePath,
        };

        protected ResourceParameters LinkedServiceParameters { get; set; }

        public AzureSqlDatabaseLinkedServiceUpgrader(
            JToken adfLinkedServiceToken,
            IFabricUpgradeMachine machine)
            : base(adfLinkedServiceToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            this.ForceToCanonicalForm(alerts);

            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            Dictionary<string, JToken> parameters = this.AdfResourceToken.SelectToken(AdfParametersPath)?.ToObject<Dictionary<string, JToken>>();
            this.LinkedServiceParameters = ResourceParameters.FromResourceDeclaration(
                parameters,
                "linkedService()");

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
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                return base.ResolveExportedSymbol(Symbol.CommonNames.ExportLinks, parametersFromCaller, alerts);
            }

            if (symbolName == Symbol.CommonNames.LinkedServiceDatabaseName)
            {
                // We need to do a bit of manipulation here.

                // In a LinkedService, the expressions look like "@{...}", so we need to convert that to 
                // a form like "{ 'type': 'Expression', 'value': '@...' }.
                string databaseNameSource = this.AdfResourceToken.SelectToken(DatabaseNamePath).ToString();
                this.FixupLinkedServiceExpression(databaseNameSource, out JToken databaseNameSourceToken);

                // Next, we need to stick this into an object from which the PropertyCopier can copy this value.
                JObject source = new JObject();
                source["database"] = databaseNameSourceToken;


                // And here's an object into which we can copy the value.
                JObject target = new JObject();

                JObject databaseNameTarget = new JObject();
                PropertyCopier copier = new PropertyCopier(
                    this.Name,
                    source,
                    target,
                    this.BuildActiveParameters(parametersFromCaller),
                    alerts);

                copier.Copy("database");

                // Finally, pull that "database" property from the target object, and return it!
                return Symbol.ReadySymbol(target["database"]);
            }

            return base.ResolveExportedSymbol(symbolName, parametersFromCaller, alerts);
        }

        /// <summary>
        /// If this AzureSqlDatabase LinkedService is in the "Legacy" format,
        /// then convert it to the "Recommended" format.
        /// </summary>
        /// <remarks>
        /// Sometime around June 2024, this LinkedService acquired a new, "Recommended" format.
        /// The "Recommended" format uses individual typeProperties instead of the connectionString.
        /// This LinkedService may have been built _before_ then, or intentionally built in the "Legacy" format.
        /// To simplify downstream operations, convert this to the "Recommended" format.
        /// </remarks>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void ForceToCanonicalForm(
            AlertCollector alerts)
        {
            JToken connectionStringToken = this.AdfResourceToken.SelectToken(AdfConnectionStringPath);

            if (connectionStringToken == null)
            {
                this.VerifyCanonicalProperties(alerts);
            }
            else
            {
                this.ConvertToCanonicalForm(connectionStringToken, alerts);
            }
        }

        private void VerifyCanonicalProperties(
            AlertCollector alerts)
        {
            // TODO?
        }

        private void ConvertToCanonicalForm(
            JToken connectionStringToken,
            AlertCollector alerts)
        {
            if (connectionStringToken.Type != JTokenType.String)
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Name}' because its ConnectionString is not a string.");
                return;
            }

            string connectionString = connectionStringToken.ToString();

            this.BuildConnectionSettings(connectionString);

            /* This:
            "typeProperties": {
                "connectionString": "integrated security=False;encrypt=True;connection timeout=30;data source=rodenkew-sql-server;initial catalog=@{linkedService().dbName};user id=rodenkew",
                ...
            }

             becomes

            "typeProperties":
            {
                "server": "rodenkew-sql-server",
                "database": "@{linkedService().dbName}",
                "encrypt": "mandatory",
                "trustServerCertificate": false,
                "authenticationType": "SQL",
                "userName": "rodenkew",
                ...
            }

            The only properties that we need are the server (for the connection hint) and the database (for the Dataset).

            We will leave the "connectionString" in place, just in case we need it for anything else.
            */

            JObject typeProperties = (JObject)this.AdfResourceToken.SelectToken("properties.typeProperties");

            if (this.ConnectionSettings.TryGetValue(DatasourceKey, out JToken serverName))
            {
                typeProperties["server"] = serverName;
            }

            if (this.ConnectionSettings.TryGetValue(DatabaseKey, out JToken databaseName))
            {
                typeProperties["database"] = databaseName;
            }
        }

        protected ResourceParameters BuildActiveParameters(
                    Dictionary<string, JToken> callerValues)
        {
            return this.LinkedServiceParameters.BuildResolutionContext(callerValues);
        }

        protected override FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            string serverName = this.AdfResourceToken.SelectToken(ServerNamePath)?.ToString();

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(serverName ?? "unknown");
        }
    }
}

// <copyright file="AzureSqlDatabaseLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders
{
    /// <summary>
    /// This class handles the Upgrade for an AzureSqlDatabase LinkedService.
    /// </summary>
    public class AzureSqlDatabaseLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        public const string DatabaseKey = "initial catalog";
        private const string DatasourceKey = "data source";
        private const string UserNameKey = "user id";
        private const string PasswordKey = "password";

        private const string ServerNamePath = "properties.typeProperties.server";
        private const string DatabaseNamePath = "properties.typeProperties.database";
        private const string UserNamePath = "properties.typeProperties.userName";
        private const string PasswordPath = "properties.typeProperties.password";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            ServerNamePath,
            DatabaseNamePath,
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
            this.ForceToCanonicalForm(alerts);

            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            this.CheckForExpressionInProperty(ServerNamePath, alerts);
            this.CheckForExpressionInProperty(UserNamePath, alerts);
            this.CheckForExpressionInProperty(PasswordPath, alerts);
        }

        /// <inheritdoc/>
        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreLink(allUpgraders, alerts);
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.LinkedServiceDatabaseName)
            {
                return this.BuildLinkedServiceDatabaseNameSymbol(parametersFromCaller, alerts);
            }

            return base.EvaluateSymbol(symbolName, parametersFromCaller, alerts);
        }

        /// <summary>
        /// Build and return a Symbol that contains the name of the database.
        /// This handles the possibility that the database is a LinkedService Expression.
        /// </summary>
        /// <param name="parametersFromCaller">The parameters from the caller.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>A Symbol whose value is the database name.</returns>
        private Symbol BuildLinkedServiceDatabaseNameSymbol(
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            return this.BuildLinkedServiceExportableSymbol(
                DatabaseNamePath,
                this.BuildActiveParameters(parametersFromCaller),
                alerts);
        }

        /// <summary>
        /// If this AzureSqlDatabase LinkedService is in the "Legacy" format,
        /// then convert it to the "Recommended" format.
        /// </summary>
        /// <remarks>
        /// Sometime around June 2024, this LinkedService acquired a new "Recommended" format.
        /// The "Recommended" format uses individual typeProperties instead of the connectionString.
        /// This LinkedService may have been built before then, or intentionally built in the "Legacy" format.
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

            var connectionSettings = this.BuildConnectionSettings(connectionString);

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

            if (connectionSettings.TryGetValue(DatasourceKey, out JToken serverName))
            {
                typeProperties["server"] = serverName;
            }

            if (connectionSettings.TryGetValue(DatabaseKey, out JToken databaseName))
            {
                typeProperties["database"] = databaseName;
            }

            if (connectionSettings.TryGetValue(UserNameKey, out JToken userName))
            {
                typeProperties["userName"] = userName;
            }

            if (connectionSettings.TryGetValue(PasswordKey, out JToken password))
            {
                typeProperties["password"] = password;
            }
        }

        /// <inheritdoc/>
        protected override FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            string serverName = this.AdfResourceToken.SelectToken(ServerNamePath)?.ToString();

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(serverName ?? "unknown");
        }
    }
}

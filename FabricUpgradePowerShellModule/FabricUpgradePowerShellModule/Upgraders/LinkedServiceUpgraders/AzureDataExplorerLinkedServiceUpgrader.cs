// <copyright file="AzureDataExplorerLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{
    public class AzureDataExplorerLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        private const string kustoEndpointPath = "properties.typeProperties.endpoint";
        private const string DatabaseNamePath = "properties.typeProperties.database";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            kustoEndpointPath
        };

        public AzureDataExplorerLinkedServiceUpgrader(
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
            if (symbolName == Symbol.CommonNames.LinkedServiceDatabaseName)
            {
                return this.BuildLinkedServiceDatabaseSymbol(parameterAssignments, alerts);
            }

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <inheritdoc/>
        protected override FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            string kustoEndpoint = "<kustoendpoint-dynamic-expression>";
            JToken kustoAppEndpointToken = this.AdfResourceToken.SelectToken(kustoEndpointPath);

            if (kustoAppEndpointToken?.Type == JTokenType.String)
            {
                kustoEndpoint = kustoAppEndpointToken.ToString();
                (kustoEndpoint, _) = UrlHelper.ProcessUrl(kustoEndpoint);
            }

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(kustoEndpoint ?? this.Name);
        }

        /// <summary>
        /// Build and return a Symbol that contains the name of the database.
        /// This handles the possibility that the database is a LinkedService Expression.
        /// </summary>
        /// <param name="parameterAssignments">The parameters from the caller.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>A Symbol whose value is the database name.</returns>
        private Symbol BuildLinkedServiceDatabaseSymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            return this.BuildLinkedServiceExportableSymbol(
                DatabaseNamePath,
                this.BuildActiveParameters(parameterAssignments),
                alerts);
        }
    }
}

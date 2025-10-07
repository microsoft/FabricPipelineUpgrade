// <copyright file="AzureDataLakeStoreLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{
    public class AzureDataLakeStoreLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        private const string dataLakeStoreUriPath = "properties.typeProperties.dataLakeStoreUri";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            dataLakeStoreUriPath
        };

        public AzureDataLakeStoreLinkedServiceUpgrader(
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
            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <inheritdoc/>
        protected override FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            string dataLakeEndpoint = "<dataLakeStoreUri-dynamic-expression>";
            JToken dataLakeAccountUriToken = this.AdfResourceToken.SelectToken(dataLakeStoreUriPath);

            if (dataLakeAccountUriToken?.Type == JTokenType.String)
            {
                dataLakeEndpoint = dataLakeAccountUriToken.ToString();
                (dataLakeEndpoint, _) = UrlHelper.ProcessUrl(dataLakeEndpoint);
            }

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(dataLakeEndpoint ?? this.Name);
        }
    }
}

// <copyright file="AzureDataLakeAnalyticsLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{
    public class AzureDataLakeAnalyticsLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        private const string accountNamePath = "properties.typeProperties.accountName";
        private const string subscriptionIdPath = "properties.typeProperties.subscriptionId";
        private const string resourceGroupPath = "properties.typeProperties.resourceGroupName";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            accountNamePath,
            subscriptionIdPath,
            resourceGroupPath
        };
        public AzureDataLakeAnalyticsLinkedServiceUpgrader(
            JToken adfLinkedServiceToken,
            IFabricUpgradeMachine machine)
            : base(adfLinkedServiceToken, machine)
        {
        }

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
            JToken adlaAccountToken = this.AdfResourceToken.SelectToken(accountNamePath);
            JToken adlaSubscriptionToken = this.AdfResourceToken.SelectToken(subscriptionIdPath);
            JToken adlaResourceGroupToken = this.AdfResourceToken.SelectToken(resourceGroupPath); 

            string datasource = $"{adlaAccountToken}--{adlaSubscriptionToken}--{adlaResourceGroupToken}";

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(datasource ?? this.Name);
        }
    }
}

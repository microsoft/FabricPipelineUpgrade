// <copyright file="AzureFunctionLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{
    /// <summary>
    /// This class handles the Upgrade for an Azure Function LinkedService
    /// </summary>
    public class AzureFunctionLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        private const string functionAppUrlPath = "properties.typeProperties.functionAppUrl";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            functionAppUrlPath
        };

        public AzureFunctionLinkedServiceUpgrader(
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
            string functionAppHostName = null;
            JToken functionAppUrlToken = this.AdfResourceToken.SelectToken(functionAppUrlPath);

            if (functionAppUrlToken?.Type == JTokenType.String)
            {
                (functionAppHostName, _) = UrlHelper.ProcessUrl(functionAppUrlToken.ToString());
            }
            
            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(functionAppHostName ?? this.Name);
        }
    }
}

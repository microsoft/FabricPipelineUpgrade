// <copyright file="AzureSynapseArtifactsLinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders
{
    public class AzureSynapseArtifactsLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        private const string endpointPath = "properties.typeProperties.endpoint";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            endpointPath
        };
        public AzureSynapseArtifactsLinkedServiceUpgrader(
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
            string synapseWorkspaceUrl = null;
            JToken synapseWorkspaceEndpointToken = this.AdfResourceToken.SelectToken(endpointPath);

            if (synapseWorkspaceEndpointToken?.Type == JTokenType.String)
            {
                (synapseWorkspaceUrl, _) = UrlHelper.ProcessUrl(synapseWorkspaceEndpointToken.ToString());
            }

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(synapseWorkspaceUrl ?? "<unresolved-synapse-workspace-url>");
        }
    }
}

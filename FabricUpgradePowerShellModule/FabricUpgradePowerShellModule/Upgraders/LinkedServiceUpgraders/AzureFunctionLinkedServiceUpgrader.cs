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
                (functionAppHostName, _) = this.ProcessUrl(functionAppUrlToken.ToString());
            }
            
            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(functionAppHostName ?? this.Name);
        }

        /// <summary>
        /// Break the URL into its components.
        /// </summary>
        /// <returns>The host name and the relative URL.</returns>
        private (string HostName, string RelativeUrl) ProcessUrl(string url)
        {
            url = this.EnsureHttpSchemeIsPresent(url, "http");

            // Note: We might want to support Connections that have a HostName and a path "prefix".
            // For example, the Connection has the URL "http://abc.com/orders" and we convert
            // "http://abc.com/orders/1234" into <connectionId>, "/1234".
            // This is a little bit complicated, so, for now, we'll just support Connections that
            // point at the Host.
            Uri uri = new Uri(url);

            string hostname = uri.Authority;
            string pathAndQuery = uri.PathAndQuery;

            return (hostname, pathAndQuery);
        }

        // The constructor for System.Uri will fail if the URL does
        // not include a schema. Make sure that there is one.
        private string EnsureHttpSchemeIsPresent(
            string url,
            string defaultHttpScheme)
        {
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                return url;
            }

            return defaultHttpScheme + "://" + url;
        }
    }
}

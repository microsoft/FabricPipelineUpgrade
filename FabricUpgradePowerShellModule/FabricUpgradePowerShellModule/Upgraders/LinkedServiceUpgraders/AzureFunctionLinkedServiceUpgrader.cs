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
    /// This class handles the Upgrade for an Azure Function LinkedService, allowing anonymous access when applicable.
    /// </summary>
    public class AzureFunctionLinkedServiceUpgrader : LinkedServiceUpgrader
    {
        private const string FunctionAppUrlPath = "properties.typeProperties.functionAppUrl";
        private const string AuthenticationKeyPath = "properties.typeProperties.authenticationKey";
        private const string FunctionKeyPath = "properties.typeProperties.functionKey";
        private const string MethodPath = "properties.typeProperties.method";
        private const string HeadersPath = "properties.typeProperties.headers";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            FunctionAppUrlPath,
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
            this.ForceToCanonicalForm(alerts);
            base.Compile(alerts);
            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);
            this.CheckForExpressionInProperty(FunctionAppUrlPath, alerts);

            // Make authenticationKey optional
            JToken authKeyToken = this.AdfResourceToken.SelectToken(AuthenticationKeyPath);
            if (authKeyToken == null || string.IsNullOrWhiteSpace(authKeyToken.ToString()))
            {
                alerts.AddWarning($"LinkedService '{this.Name}' is using anonymous access as authenticationKey is missing.");
            }
            else
            {
                this.CheckForExpressionInProperty(AuthenticationKeyPath, alerts);
            }
        }

        /// <inheritdoc/>
        public override void PreSort(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreSort(allUpgraders, alerts);
        }

        /// <summary>
        /// If this AzureFunction LinkedService is in the "Legacy" format,
        /// then convert it to the "Recommended" format.
        /// </summary>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void ForceToCanonicalForm(AlertCollector alerts)
        {
            JToken functionUrlToken = this.AdfResourceToken.SelectToken(FunctionAppUrlPath);

            if (functionUrlToken == null)
            {
                this.VerifyCanonicalProperties(alerts);
            }
            else
            {
                this.ConvertToCanonicalForm(functionUrlToken, alerts);
            }
        }

        private void VerifyCanonicalProperties(AlertCollector alerts)
        {
            // TODO: Add specific validation checks if required
        }

        private void ConvertToCanonicalForm(JToken functionUrlToken, AlertCollector alerts)
        {
            if (functionUrlToken.Type != JTokenType.String)
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Name}' because FunctionAppUrl is not a string.");
                return;
            }

            JObject typeProperties = (JObject)this.AdfResourceToken.SelectToken("properties.typeProperties");

            if (typeProperties != null)
            {
                typeProperties["functionAppUrl"] = functionUrlToken;

                if (this.AdfResourceToken.SelectToken(FunctionKeyPath) != null)
                {
                    typeProperties["functionKey"] = this.AdfResourceToken.SelectToken(FunctionKeyPath);
                }

                if (this.AdfResourceToken.SelectToken(MethodPath) != null)
                {
                    typeProperties["method"] = this.AdfResourceToken.SelectToken(MethodPath);
                }

                if (this.AdfResourceToken.SelectToken(HeadersPath) != null)
                {
                    typeProperties["headers"] = this.AdfResourceToken.SelectToken(HeadersPath);
                }
            }
        }

        /// <inheritdoc/>
        protected override FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            string functionAppUrl = this.AdfResourceToken.SelectToken(FunctionAppUrlPath)?.ToString();

            return base.BuildFabricConnectionHint()
                .WithConnectionType(this.LinkedServiceType)
                .WithDatasource(functionAppUrl ?? "unknown");
        }
    }
}

// <copyright file="WebActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    public class WebActivityUpgrader : ActivityUpgrader
    {
        private const string adfUrlPath = "typeProperties.url";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            adfUrlPath
        };

        public WebActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.Web, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            this.ValidateUrl(alerts);

            if (this.CountArrayElements("typeProperties.linkedServices") > 0)
            {
                alerts.AddPermanentError($"Cannot upgrade Web Activity '{this.Path}' because it includes LinkedServices");
            }

            if (this.CountArrayElements("typeProperties.datasets") > 0)
            {
                alerts.AddPermanentError($"Cannot upgrade Web Activity '{this.Path}' because it includes Datasets");
            }

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
            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
            {
                return this.BuildExportResolveStepsSymbol(parameterAssignments, alerts);
            }
            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameterAssignments, alerts);
            }

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildExportResolveStepsSymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            List<FabricExportResolveStep> resolves = new List<FabricExportResolveStep>();

            // The ADF WebActivity directly declares its URL, but the Fabric WebActivity uses a Connection instead.
            // Therefore, the client must provide a UrlHostToConnectionId Resolution to help us out.
            string url = this.AdfResourceToken.SelectToken(adfUrlPath).ToString();
            (string hostName, _) = UrlHelper.ProcessUrl(url);

            FabricUpgradeResolution.ResolutionType resolutionType = FabricUpgradeResolution.ResolutionType.UrlHostToConnectionId;
            FabricExportResolveStep userCredentialConnectionResolve = new FabricExportResolveStep(
                resolutionType,
                hostName,
                "externalReferences.connection")
                .WithHint(new FabricUpgradeConnectionHint()
                {
                    LinkedServiceName = null,
                    ConnectionType = "Web v2",
                    Datasource = hostName
                }
                    .WithTemplate(new FabricUpgradeResolution()
                    {
                        Type = resolutionType,
                        Key = hostName,
                        Value = "<Fabric Connection ID>"
                    }
               ));

            resolves.Add(userCredentialConnectionResolve);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            Symbol activitySymbol = base.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;

            string url = this.AdfResourceToken.SelectToken(adfUrlPath).ToString();
            (_, string relativeUrl) = UrlHelper.ProcessUrl(url);

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);
            copier.Copy("description");
            copier.Copy("typeProperties.method", allowNull: false);
            copier.Copy("typeProperties.headers");
            copier.Set("typeProperties.relativeUrl", relativeUrl);
            copier.Copy("typeProperties.body");
            copier.Copy("typeProperties.disableCertValidation");
            copier.Copy("typeProperties.httpRequestTimeout");
            copier.Copy("typeProperties.turnOffAsync");

            // This property cannot be set until the Export operation phase.
            // We include this property in the "exportResolve" symbol.
            copier.Set("externalReferences.connection", Guid.Empty.ToString());

            return Symbol.ReadySymbol(fabricActivityObject);
        }


        private int CountArrayElements(
            string path)
        {
            JToken token = this.AdfResourceToken.SelectToken(path);
            if (token == null) return 0;
            if (token.Type != JTokenType.Array) return 0;
            return ((JArray)token).Count;
        }

        private void ValidateUrl(AlertCollector alerts)
        {
            // We cannot upgrade a WebActivity that has an Expression for its URL.
            // The ADF WebActivity directly declares its URL, but the Fabric WebActivity uses a Connection instead.
            // If the ADF WebActivity's URL is a String, then we can make this work with a special UrlHostToConnection Resolution.
            JToken urlToken = this.AdfResourceToken.SelectToken("typeProperties.url");

            if (urlToken == null)
            {
                // We have already generated an alert for this.
                return;
            }

            // Validate that the URL is a string.
            if (urlToken.Type != JTokenType.String)
            {
                alerts.AddPermanentError(
                    $"{this.Path}.typeProperties.url is not a string and the FabricUpgrader cannot upgrade a WebActivity whose URL is not a string.");
                return;
            }

            // Validate that the URL is properly formatted.
            (string hostName, _) = UrlHelper.ProcessUrl(urlToken.ToString());
            if (string.IsNullOrWhiteSpace(hostName))
            {
                alerts.AddPermanentError($"Cannot upgrade Web Activity '{this.Path}' because its URL is improperly formatted.");
            }
        }
    }
}

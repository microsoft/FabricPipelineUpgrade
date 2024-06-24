// <copyright file="JsonDatasetUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.DatasetUpgraders
{
    /// <summary>
    /// This class handles the Upgrade for an Json Dataset.
    /// </summary>
    public class JsonDatasetUpgrader : DatasetUpgrader
    {
        private const string adfLocationPath = "properties.typeProperties.location";
        private const string fabricLocationPath = "typeProperties.location";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            adfLocationPath,
        };

        public JsonDatasetUpgrader(
            JToken adfDatasetToken,
            IFabricUpgradeMachine machine)
            : base(adfDatasetToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);
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
            if (symbolName == Symbol.CommonNames.DatasetSettings)
            {
                return this.BuildDatasetSettings(parametersFromCaller, alerts);
            }

            return base.EvaluateSymbol(symbolName, parametersFromCaller, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildDatasetSettings(
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            Symbol datasetSettingsSymbol = base.EvaluateSymbol(Symbol.CommonNames.DatasetSettings, parametersFromCaller, alerts);

            if (datasetSettingsSymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            JObject fabricActivityObject = (JObject)datasetSettingsSymbol.Value;
            PropertyCopier copier = new PropertyCopier(
                this.Path,
                this.AdfResourceToken,
                fabricActivityObject,
                this.BuildActiveParameters(parametersFromCaller),
                alerts);

            copier.Copy(adfLocationPath, fabricLocationPath);

            // Fabric UX always makes an empty schema for JSON datasets.
            // Therefore, we will duplicate that behavior here.
            copier.Set("schema", new JObject());

            return Symbol.ReadySymbol(fabricActivityObject);
        }
    }
}

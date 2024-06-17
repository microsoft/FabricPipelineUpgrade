// <copyright file="JsonDatasetUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.DatasetUpgraders
{
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

            string q = this.AdfResourceToken.ToString(Newtonsoft.Json.Formatting.Indented);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);
        }

        /// <inheritdoc/>
        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreLink(allUpgraders, alerts);
        }

        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                return base.ResolveExportedSymbol(Symbol.CommonNames.ExportLinks, alerts);
            }

            if (symbolName == Symbol.CommonNames.DatasetSettings)
            {
                Symbol datasetSettingsSymbol = base.ResolveExportedSymbol(Symbol.CommonNames.DatasetSettings, alerts);

                if (datasetSettingsSymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                JObject fabricActivityObject = (JObject)datasetSettingsSymbol.Value;
                PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);

                copier.Copy(adfLocationPath, fabricLocationPath);

                // Fabric UX always makes an empty schema for JSON datasets.
                // Therefore, we will duplicate that behavior here.
                copier.Set("schema", new JObject());

                return Symbol.ReadySymbol(fabricActivityObject);
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
        }
    }
}

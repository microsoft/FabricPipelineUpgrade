// <copyright file="AzureSqlTableDatasetUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.DatasetUpgraders
{
    public class AzureSqlTableDatasetUpgrader : DatasetUpgrader
    {
        private readonly List<string> requiredAdfProperties = new List<string>
        {
        };

        public AzureSqlTableDatasetUpgrader(
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

        public override Symbol ResolveExportedSymbol(
            string symbolName,
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                return base.ResolveExportedSymbol(Symbol.CommonNames.ExportLinks, parameters, alerts);
            }

            if (symbolName == Symbol.CommonNames.DatasetSettings)
            {
                Symbol datasetSettingsSymbol = base.ResolveExportedSymbol(Symbol.CommonNames.DatasetSettings, parameters, alerts);

                if (datasetSettingsSymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                JObject fabricActivityObject = (JObject)datasetSettingsSymbol.Value;
                PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);

                copier.Copy("properties.typeProperties", "typeProperties");

                if (this.LinkedServiceUpgrader.ConnectionSettings.TryGetValue(
                    AzureSqlDatabaseLinkedServiceUpgrader.DatabaseKey,
                    out JToken databaseName))
                {
                    copier.Set("typeProperties.database", databaseName);
                }
                else
                {
                    // The LinkedService already alerted!
                    copier.Set("typeProperties.database", "UNKNOWN");
                }

                // The datasetSettings should include at least an _empty_ Schema array.
                //
                // ADF uses the up-to-date "mappings" field in its translator, and Fabric will accept
                // that field if (and only if?) the datasetSettings fields include a non-null Schema.
                //
                // If we ensure that there is at least an _empty_ Schema field,
                // then the "Mappings" tab in the Fabric UX will create the supported "mappings" translator,
                // rather than one of the deprecated translator forms.
                JToken schema = this.AdfResourceToken.SelectToken("properties.schema") ?? new JArray();
                copier.Set("schema", schema);

                return Symbol.ReadySymbol(fabricActivityObject);
            }

            return base.ResolveExportedSymbol(symbolName, parameters, alerts);
        }
    }
}

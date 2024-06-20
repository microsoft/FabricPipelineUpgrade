// <copyright file="AzureSqlTableDatasetUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

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
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                return base.ResolveExportedSymbol(Symbol.CommonNames.ExportLinks, parametersFromCaller, alerts);
            }

            if (symbolName == Symbol.CommonNames.DatasetSettings)
            {
                Symbol datasetSettingsSymbol = base.ResolveExportedSymbol(Symbol.CommonNames.DatasetSettings, parametersFromCaller, alerts);

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

                copier.Copy("properties.typeProperties", "typeProperties");

                Dictionary<string, JToken> parametersToLinkedService = this.BuildParametersToPassToLinkedService(parametersFromCaller, alerts);

                Symbol databaseNameSymbol = this.LinkedServiceUpgrader.ResolveExportedSymbol(
                    Symbol.CommonNames.LinkedServiceDatabaseName,
                    parametersToLinkedService,
                    alerts);

                if (databaseNameSymbol.State == Symbol.SymbolState.Ready)
                {
                    copier.Set("typeProperties.database", databaseNameSymbol.Value);
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

            return base.ResolveExportedSymbol(symbolName, parametersFromCaller, alerts);
        }

        /// <summary>
        /// Combine this dataset's default parameter values with the values passed in from the 
        /// caller to produce a set of values to send to the LinkedService.
        /// </summary>
        /// <param name="parametersFromCaller">The values passed in from the caller (like Copy Activity).</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns></returns>
        private Dictionary<string, JToken> BuildParametersToPassToLinkedService(
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            // TODO: This will stop working when the Dataset needs to both accept and forward parameters.

            JObject linkedServiceParametersObject = (JObject)this.AdfResourceToken.SelectToken($"properties.linkedServiceName.parameters") ?? new JObject();
            Dictionary<string, JToken> linkedServiceParameters = linkedServiceParametersObject.ToObject<Dictionary<string, JToken>>();

            var localParameters = this.BuildActiveParameters(parametersFromCaller);

            JObject parametersToSend = new JObject();

            PropertyCopier copier = new PropertyCopier("", linkedServiceParametersObject, parametersToSend, localParameters, alerts);

            foreach (var p in linkedServiceParametersObject)
            {
                copier.Copy(p.Key);
            }

            return parametersToSend.ToObject<Dictionary<string, JToken>>();

            /*
            JObject targetParams = new JObject();
            PropertyCopier paramCopier = new PropertyCopier(
                "",
                parametersFromCaller,
                targetParams,
                this.DatasetParameters,
                alerts);

            return null;
            */

        }
    }
}

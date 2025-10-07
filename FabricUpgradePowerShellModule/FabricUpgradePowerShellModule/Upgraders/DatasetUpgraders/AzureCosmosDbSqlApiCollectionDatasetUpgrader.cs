// <copyright file="AzureCosmosDbSqlApiCollectionDatasetUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.DatasetUpgraders
{
    /// <summary>
    /// This class handles the Upgrade for an AzureCosmosDbSqlApiCollectio Dataset.
    /// </summary>
    public class AzureCosmosDbSqlApiCollectionDatasetUpgrader : DatasetUpgrader
    {
        private readonly List<string> requiredAdfProperties = new List<string>
        {
        };

        public AzureCosmosDbSqlApiCollectionDatasetUpgrader(
            JToken adfDatasetToken,
            IFabricUpgradeMachine machine)
            : base(adfDatasetToken, machine)
        {
        }

        /// <inheritdoc/>AzureCosmosDbSqlApiCollectionDatasetUpgrader
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
            if (symbolName == Symbol.CommonNames.DatasetSettings)
            {
                return this.BuildDatasetSettings(parameterAssignments, alerts);
            }

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildDatasetSettings(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            Symbol datasetSettingsSymbol = base.EvaluateSymbol(Symbol.CommonNames.DatasetSettings, parameterAssignments, alerts);

            if (datasetSettingsSymbol.State != Symbol.SymbolState.Ready)
            {
                // Propagate non-ready state to caller.
                return datasetSettingsSymbol;
            }

            JObject fabricActivityObject = (JObject)datasetSettingsSymbol.Value;
            PropertyCopier copier = new PropertyCopier(
                this.Path,
                this.AdfResourceToken,
                fabricActivityObject,
                this.BuildActiveParameters(parameterAssignments),
                alerts);

            // Copy Cosmos-specific typeProperties (collectionName, etc.)
            copier.Copy("properties.typeProperties", "typeProperties", copyIfNull: false);

            // Some dataset settings (database name) may come from the LinkedService.
            Dictionary<string, JToken> parameterAssignmentsToLinkedService = this.BuildParameterAssignmentsToPassToLinkedService(parameterAssignments, alerts);

            Symbol databaseNameSymbol = Symbol.MissingSymbol();
            if (this.LinkedServiceUpgrader != null)
            {
                databaseNameSymbol = this.LinkedServiceUpgrader.EvaluateSymbol(
                    Symbol.CommonNames.LinkedServiceDatabaseName,
                    parameterAssignmentsToLinkedService,
                    alerts);
            }

            if (databaseNameSymbol.State == Symbol.SymbolState.Ready && databaseNameSymbol.Value != null)
            {
                copier.Set("typeProperties.database", databaseNameSymbol.Value);
            }
            else
            {
                // The LinkedService already alerted, or no DB could be determined.
                copier.Set("typeProperties.database", "UNKNOWN");
            }

            // Ensure schema exists and is an object (Fabric expects an object for Cosmos dataset schema).
            JToken schema = this.AdfResourceToken.SelectToken("properties.schema") ?? new JObject();
            if (schema.Type == JTokenType.Object)
            {
                copier.Set("schema", schema);
            }
            else if (schema.Type == JTokenType.Array)
            {
                // Preserve array if present in ADF, otherwise convert to empty object.
                copier.Set("schema", schema);
            }
            else
            {
                copier.Set("schema", new JObject());
            }

            // externalReferences.connection is provided by BuildCommonDatasetSettings (defaults to Guid.Empty).
            return Symbol.ReadySymbol(fabricActivityObject);
        }

        /// <summary>
        /// Combine this Dataset's default parameter values with the values passed in from the 
        /// caller to produce a set of values to send to the LinkedService.
        /// </summary>
        /// <param name="parameterAssignments">The values passed in from the caller (like Copy Activity).</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>A dictionary describing the values to be sent when resolving a LinkedService Symbol.</returns>
        private Dictionary<string, JToken> BuildParameterAssignmentsToPassToLinkedService(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            JObject linkedServiceParametersObject = (JObject)this.AdfResourceToken.SelectToken($"properties.linkedServiceName.parameters") ?? new JObject();
            Dictionary<string, JToken> linkedServiceParameters = linkedServiceParametersObject.ToObject<Dictionary<string, JToken>>();

            var localParameters = this.BuildActiveParameters(parameterAssignments);

            JObject parameterAssignmentsToSend = new JObject();

            PropertyCopier copier = new PropertyCopier("", linkedServiceParametersObject, parameterAssignmentsToSend, localParameters, alerts);

            foreach (var p in linkedServiceParametersObject)
            {
                copier.Copy(p.Key);
            }

            return parameterAssignmentsToSend.ToObject<Dictionary<string, JToken>>();
        }
    }
}

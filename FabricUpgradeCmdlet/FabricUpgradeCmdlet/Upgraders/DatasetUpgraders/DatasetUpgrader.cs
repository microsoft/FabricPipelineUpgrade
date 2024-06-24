// <copyright file="DatasetUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders;
using FabricUpgradeCmdlet.Utilities;
using Microsoft.PowerShell.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.DatasetUpgraders
{
    public class DatasetUpgrader : Upgrader
    {
        public class DatasetTypes
        {
            public const string AzureSqlTable = "AzureSqlTable";
            public const string Json = "Json";
            public const string Binary = "Binary";
        }

        protected const string AdfDatasetTypePath = "properties.type";
        protected const string FabricDatasetTypePath = "type";
        protected const string AdfLinkedServiceNamePath = "properties.linkedServiceName.referenceName";
        protected const string FabricLinkedServiceNamePath = "linkedServiceName.referenceName";

        protected ResourceParameters DatasetParameters { get; set; }

        protected const string FabricConnectionIdPath = "externalReferences.connection";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            AdfLinkedServiceNamePath
        };


        protected DatasetUpgrader(
            JToken adfDatasetToken,
            IFabricUpgradeMachine machine)
            : base(adfDatasetToken, machine)
        {
            this.AdfModel = AdfDatasetModel.Build(adfDatasetToken);
            this.DatasetType = this.AdfModel.Properties.DatasetType;
            this.Name = AdfModel.Name;
            this.UpgraderType = FabricUpgradeResourceTypes.Dataset;
            this.Path = "Dataset " + this.Name;
        }

        protected AdfDatasetModel AdfModel { get; set; }

        protected string DatasetType { get; set; }

        protected LinkedServiceUpgrader LinkedServiceUpgrader { get; private set; }

        /// <summary>
        /// A "factory" function that creates the appropriate Upgrader from the ADF Dataset's Type.
        /// </summary>
        /// <param name="parentPath">The 'path' to the parent object.</param>
        /// <param name="adfDatasetToken">The JObject that describes the ADF Dataset.</param>
        /// <param name="machine">The FabricUpgradeMachine that provides utilities to Upgraders.</param>
        /// <returns>A new Upgrader for that Dataset type.</returns>
        public static DatasetUpgrader CreateDatasetUpgrader(
            JToken adfDatasetToken,
            IFabricUpgradeMachine machine)
        {
            string datasetType = AdfDatasetModel.Build(adfDatasetToken).Properties.DatasetType;
            return datasetType switch
            {
                DatasetTypes.AzureSqlTable => new AzureSqlTableDatasetUpgrader(adfDatasetToken, machine),
                DatasetTypes.Binary => new BinaryDatasetUpgrader(adfDatasetToken, machine),
                DatasetTypes.Json => new JsonDatasetUpgrader(adfDatasetToken, machine),
                _ => new UnsupportedDatasetUpgrader(adfDatasetToken, machine),
            };
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            this.DatasetParameters = ResourceParameters.FromResourceDeclaration(
                this.AdfModel.Properties.Parameters,
                "dataset()");
        }

        /// <inheritdoc/>
        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreLink(allUpgraders, alerts);

            // All datasets need to find a LinkedService, and that LinkedService is always in the same place!
            Upgrader linkedServiceUpgrader = this.FindOtherUpgrader(
                allUpgraders,
                FabricUpgradeResourceTypes.LinkedService,
                AdfLinkedServiceNamePath,
                alerts);

            this.LinkedServiceUpgrader = (LinkedServiceUpgrader)linkedServiceUpgrader;

            if (this.LinkedServiceUpgrader != null)
            {
                this.DependsOn.Add(this.LinkedServiceUpgrader);
            }
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
            {
                return this.BuildExportResolveSteps(parametersFromCaller, alerts);
            }

            if (symbolName == Symbol.CommonNames.DatasetSettings)
            {
                return this.BuildCommonDatasetSettings(parametersFromCaller, alerts);
            }

            return base.EvaluateSymbol(symbolName, parametersFromCaller, alerts);
        }

        /// <summary>
        /// Build the ExportLinks Symbol that will be included in
        /// the Pipeline of the Activity that references this Dataset.
        /// </summary>
        /// <remarks>
        /// A Dataset links to a LinkedService.
        /// In the Export phase, we will acquire the Fabric Resource ID for the Connection
        /// that corresponds to the LinkedService, and _then_ we will insert it into this JSON.
        /// </remarks>
        /// <param name="parametersFromCaller">The parameters from the caller.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>A Symbol whose value describes the other Fabric Resources upon which this Dataset depends.</returns>
        protected virtual Symbol BuildExportResolveSteps(
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            // Datasets may override this method.

            List<FabricExportResolveStep> resolveSteps = new List<FabricExportResolveStep>();

            string linkedServiceName = this.AdfResourceToken.SelectToken(AdfLinkedServiceNamePath)?.ToString();

            FabricExportResolveStep linkedServiceLink = FabricExportResolveStep.ForResourceId(
                $"{FabricUpgradeResourceTypes.Connection}:{linkedServiceName}",
                FabricConnectionIdPath);

            resolveSteps.Add(linkedServiceLink);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolveSteps)));
        }

        /// <summary>
        /// Create a Symbol whose Value is a JObject that contains all of the 
        /// common datasetSetting properties.
        /// </summary>
        /// <param name="parametersFromCaller">The parameters from the caller.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>A Symbol whose value describes the common Fabric datasetSettings.</returns>
        private Symbol BuildCommonDatasetSettings(
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            // Populate the common dataset settings.

            JObject datasetSettings = new JObject();
            PropertyCopier copier = new PropertyCopier(
                this.Path,
                this.AdfResourceToken,
                datasetSettings,
                this.BuildActiveParameters(parametersFromCaller),
                alerts);

            copier.Copy("properties.annotations", "annotations");
            copier.Copy(AdfDatasetTypePath, FabricDatasetTypePath);
            copier.Set(FabricConnectionIdPath, Guid.Empty.ToString());

            return Symbol.ReadySymbol(datasetSettings);
        }

        /// <summary>
        /// Build the datasetSettings Symbol whose value will be included in an Activity.
        /// </summary>
        /// <param name="parameters">The parameters from the caller.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>The datasetSettings Symbol whose value will be included in an Activity.</returns>
        protected virtual Symbol BuildDatasetSettings(
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            // Datasets should override this method!
            return Symbol.ReadySymbol(null);
        }

        protected ResourceParameters BuildActiveParameters(
            Dictionary<string, JToken> callerValues)
        {
            return this.DatasetParameters.BuildResolutionContext(callerValues);
        }

        /// <summary>
        /// The ADF Model for a Pipeline Activity.
        /// </summary>
        protected class AdfDatasetModel
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "properties")]
            public AdfDatasetPropertiesModel Properties { get; set; }

            public static AdfDatasetModel Build(JToken datasetToken)
            {
                return UpgradeSerialization.FromJToken<AdfDatasetModel>(datasetToken);
            }
        }

        protected class AdfDatasetPropertiesModel
        {
            [JsonProperty(PropertyName = "annotations")]
            public JToken Annotations { get; set; }

            [JsonProperty(PropertyName = "type")]
            public string DatasetType { get; set; }

            [JsonProperty(PropertyName = "schema")]
            public JToken Schema { get; set; }

            [JsonProperty(PropertyName = "linkedService")]
            public AdfDatasetLinkedServiceModel LinkedService { get; set; }

            [JsonProperty(PropertyName = "parameters")]
            public Dictionary<string, JToken> Parameters { get; set; } = new Dictionary<string, JToken>();
        }

        protected class AdfDatasetLinkedServiceModel
        {
            [JsonProperty(PropertyName = "referenceName")]
            public string ReferenceName { get; set; }

            [JsonProperty(PropertyName = "parameters")]
            public Dictionary<string, JToken> Parameters { get; set; } = new Dictionary<string, JToken>();
        }
    }
}

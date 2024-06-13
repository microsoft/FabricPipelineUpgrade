using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Upgraders.ActivityUpgraders;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FabricUpgradeCmdlet.Upgraders.ActivityUpgraders.ActivityUpgrader;

namespace FabricUpgradeCmdlet.Upgraders.DatasetUpgraders
{
    public class DatasetUpgrader : Upgrader
    {
        public class DatasetTypes
        {
            public const string Json = "Json";
            public const string Binary = "Binary";
        }

        protected const string AdfDatasetTypePath = "properties.type";
        protected const string AdfLinkedServiceNamePath = "properties.linkedServiceName.referenceName";

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
            this.Path = this.Name;
        }

        protected AdfDatasetModel AdfModel { get; set; }

        protected string DatasetType { get; set; }

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
                DatasetTypes.Binary => new BinaryDatasetUpgrader(adfDatasetToken, machine),
                DatasetTypes.Json => new JsonDatasetUpgrader(adfDatasetToken, machine),
                _ => new UnsupportedDatasetUpgrader(adfDatasetToken, machine),
            };
        }

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);
        }

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

            if (linkedServiceUpgrader != null)
            {
                this.DependsOn.Add(linkedServiceUpgrader);
            }
        }

        /// <inheritdoc/>
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                // A Dataset links to a LinkedService.
                // In the Export phase, we will acquire the Fabric Resource ID for the Connection
                // that corresponds to the LinkedService, and _then_ we will insert it into this JSON.

                List<FabricExportLink> links = new List<FabricExportLink>();

                string linkedServiceName = this.AdfResourceToken.SelectToken(AdfLinkedServiceNamePath)?.ToString();

                FabricExportLink linkedServiceLink = new FabricExportLink(
                    $"{FabricUpgradeResourceTypes.Connection}:{linkedServiceName}",
                    FabricConnectionIdPath);

                links.Add(linkedServiceLink);

                return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(links)));
            }

            if (symbolName == Symbol.CommonNames.DatasetSettings)
            {
                // Populate the common dataset settings.

                JObject datasetSettings = new JObject();
                PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, datasetSettings, alerts);

                copier.Copy("properties.annotations");
                copier.Copy(AdfDatasetTypePath);
                copier.Set(FabricConnectionIdPath, Guid.Empty.ToString());

                return Symbol.ReadySymbol(datasetSettings);
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
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
        }

        protected class AdfDatasetLinkedServiceModel
        {
            [JsonProperty(PropertyName = "referenceName")]
            public string ReferenceName { get; set; }
        }
    }
}

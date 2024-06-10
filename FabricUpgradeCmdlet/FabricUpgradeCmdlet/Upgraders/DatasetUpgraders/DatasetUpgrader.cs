using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Upgraders.ActivityUpgraders;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FabricUpgradeCmdlet.Upgraders.ActivityUpgraders.ActivityUpgrader;

namespace FabricUpgradeCmdlet.Upgraders.DatasetUpgraders
{
    public class DatasetTypes
    {
        // public const string WaitActivity = "Wait";
    }

    public class DatasetUpgrader : Upgrader
    {
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
                _ => new UnsupportedDatasetUpgrader(adfDatasetToken, machine),
            };
        }

        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            // TODO: (I think) all datasets need to find a LinkedService, and that LinkedService is always in the same place!
            base.PreLink(allUpgraders, alerts);
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

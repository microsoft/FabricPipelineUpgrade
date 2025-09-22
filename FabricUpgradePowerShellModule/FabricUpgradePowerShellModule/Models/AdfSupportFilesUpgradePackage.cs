// <copyright file="AdfSupportFileUpgradePackage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Models
{
    /// <summary>
    /// This class is part of the communication between Import-AdfSupportFile and ConvertTo-FabricResources.
    /// It contains the unpacked contents of the ADF Support File.
    /// </summary>

    public class AdfSupportFileUpgradePackage : UpgradePackage
    {
        // The name of the Azure Data Factory that made the ADF Support File.
        [JsonProperty(PropertyName = "adfName", Order = 10)]
        public string AdfName { get; set; }

        // The subscription ID where the Azure Data Factory is located.
        [JsonProperty(PropertyName = "subscriptionId", Order = 11)]
        public string SubscriptionId { get; set; }

        // The resource group name where the Azure Data Factory is located.
        [JsonProperty(PropertyName = "resourceGroupName", Order = 12)]
        public string ResourceGroupName { get; set; }

        // The Azure region where the Azure Data Factory is located.
        [JsonProperty(PropertyName = "adfRegion", Order = 13)]
        public string AdfRegion { get; set; }

        [JsonProperty(PropertyName = "pipelines", Order = 100)]
        public Dictionary<string, JObject> Pipelines { get; set; } = new Dictionary<string, JObject>();

        [JsonProperty(PropertyName = "datasets", Order = 101)]
        public Dictionary<string, JObject> Datasets { get; set; } = new Dictionary<string, JObject>();

        [JsonProperty(PropertyName = "linkedServices", Order = 102)]
        public Dictionary<string, JObject> LinkedServices { get; set; } = new Dictionary<string, JObject>();

        [JsonProperty(PropertyName = "triggers", Order = 103)]
        public Dictionary<string, JObject> Triggers { get; set; } = new Dictionary<string, JObject>();

        public AdfSupportFileUpgradePackage()
            : base(UpgradePackage.UpgradePackageType.AdfSupportFile)
        {
        }

        public static AdfSupportFileUpgradePackage FromString(string json)
        {
            return JsonConvert.DeserializeObject<AdfSupportFileUpgradePackage>(json);
        }

        public static new AdfSupportFileUpgradePackage FromJToken(JToken jToken)
        {
            return UpgradeSerialization.FromJToken<AdfSupportFileUpgradePackage>(jToken);
        }


    }
}

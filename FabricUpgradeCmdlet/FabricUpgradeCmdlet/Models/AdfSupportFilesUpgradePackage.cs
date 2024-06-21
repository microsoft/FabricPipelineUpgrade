// <copyright file="AdfSupportFileUpgradePackage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Models
{
    /// <summary>
    /// This class is part of the communication between Import-AdfSupportFile and ConvertTo-FabricPipeline.
    /// It contains the unpacked contents of the ADF Support File.
    /// </summary>

    public class AdfSupportFileUpgradePackage : UpgradePackage
    {
        // The name of the Azure Data Factory that made the ADF Support File.
        [JsonProperty(PropertyName = "adfName", Order = 10)]
        public string AdfName { get; set; }

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

// <copyright file="AdfSupportFileUpgradePackage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Utilities
{
    public class AdfSupportFileUpgradePackage : AdfUpgradePackage
    {
        [JsonProperty(PropertyName = "pipelines", Order = 100)]
        public Dictionary<string, JObject> Pipelines { get; set; } = new Dictionary<string, JObject>();

        [JsonProperty(PropertyName = "datasets", Order = 101)]
        public Dictionary<string, JObject> Datasets { get; set; } = new Dictionary<string, JObject>();

        [JsonProperty(PropertyName = "linkedServices", Order = 102)]
        public Dictionary<string, JObject> LinkedServices { get; set; } = new Dictionary<string, JObject>();

        [JsonProperty(PropertyName = "triggers", Order = 103)]
        public Dictionary<string, JObject> Triggers { get; set; } = new Dictionary<string, JObject>();

        public static new AdfSupportFileUpgradePackage FromString(string json)
        {
            return JsonConvert.DeserializeObject<AdfSupportFileUpgradePackage>(json);
        }

        public static new AdfSupportFileUpgradePackage FromJToken(JToken jToken)
        {
            return UpgradeSerialization.FromJToken<AdfSupportFileUpgradePackage>(jToken);
        }


    }
}

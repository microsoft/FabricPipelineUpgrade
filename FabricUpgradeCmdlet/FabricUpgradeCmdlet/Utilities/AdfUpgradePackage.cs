// <copyright file="AdfUpgradePackage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Utilities
{
    public class AdfUpgradePackage : UpgradePackage
    {

        [JsonProperty(PropertyName = "adfName", Order = 10)]
        public string AdfName { get; set; }

        public static AdfUpgradePackage FromString(string json)
        {
            return JsonConvert.DeserializeObject<AdfUpgradePackage>(json);
        }

        public static new AdfUpgradePackage FromJToken(JToken jToken)
        {
            return UpgradeSerialization.FromJToken<AdfUpgradePackage>(jToken);
        }
    }
}

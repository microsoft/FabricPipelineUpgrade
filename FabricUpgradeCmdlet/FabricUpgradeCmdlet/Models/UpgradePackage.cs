// <copyright file="UpgradePackage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Models
{
    public class UpgradePackage
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum UpgradePackageType
        {
            Unknown = 0,
            AdfSupportFile = 1,
        }

        [JsonProperty(PropertyName = "type", Order = 1)]
        public UpgradePackageType Type { get; set; } = UpgradePackageType.Unknown;

        public static UpgradePackage FromJToken(JToken jToken)
        {
            return UpgradeSerialization.FromJToken<UpgradePackage>(jToken);
        }

    }
}

// <copyright file="UpgradePackage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Models
{
    /// <summary>
    /// This class is the base class of UpgradePackages.
    /// It contains only the "type" property, so that all UpgradePackages will have a common property.
    /// </summary>
    /// <remarks>
    /// We may extend this module to include Import-ArmTemplate or Import-FabricPipeline.
    /// </remarks>
    public class UpgradePackage
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum UpgradePackageType
        {
            Unknown = 0,
            AdfSupportFile = 1,
        }

        [JsonProperty(PropertyName = "type", Order = 1)]
        public UpgradePackageType Type { get; private set; } = UpgradePackageType.Unknown;

        public UpgradePackage()
        {
            this.Type = UpgradePackageType.Unknown;
        }

        protected UpgradePackage(
            UpgradePackageType type)
        {
            this.Type = type;
        }

        public static UpgradePackage FromJToken(JToken jToken)
        {
            return UpgradeSerialization.FromJToken<UpgradePackage>(jToken);
        }

    }
}

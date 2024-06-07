using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Utilities
{
    public class AdfUpgradePackage
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum UpgradePackageType
        {
            Unknown = 0,
            AdfSupportFile = 1,
        }

        [JsonProperty(PropertyName = "type", Order = 1)]
        public UpgradePackageType Type { get; set; } = UpgradePackageType.Unknown;

        [JsonProperty(PropertyName = "adfName", Order = 2)]
        public string AdfName { get; set; }

        public static AdfUpgradePackage FromString(string json)
        {
            return JsonConvert.DeserializeObject<AdfUpgradePackage>(json);
        }

        public static AdfUpgradePackage FromJToken(JToken jToken)
        {
            return UpgradeSerialization.FromJToken<AdfUpgradePackage>(jToken);
        }
    }
}

using FabricUpgradeCmdlet.Models;
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
    public class AdfUpgradePackage : UpgradePackage
    {

        [JsonProperty(PropertyName = "adfName", Order = 100)]
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

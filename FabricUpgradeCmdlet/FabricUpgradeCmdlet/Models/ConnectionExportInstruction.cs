using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Models
{
    // Currently, the FabricUpgrader does not actually export Connections.
    // When it does, then we can "back-fill" this functionality.
    public class ConnectionExportInstruction : FabricExportInstruction
    {
        [JsonProperty(PropertyName = "link", Order = 100)]
        public List<FabricExportLink> Links { get; set; } = new List<FabricExportLink>();

        [JsonProperty(PropertyName = "export", Order = 101)]
        public JObject Export { get; set; }

        public ConnectionExportInstruction(string name)
            : base(name, FabricUpgradeResourceTypes.Connection)
        {
        }

        static public new PipelineExportInstruction FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<PipelineExportInstruction>(token);
        }

        public JObject ToJObject()
        {
            return (JObject)UpgradeSerialization.ToJToken(this);
        }
    }
}

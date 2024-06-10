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
    public class PipelineExportInstruction : FabricExportInstruction
    {
        [JsonProperty(PropertyName = "link", Order = 100)]
        public List<FabricExportLink> Links { get; set; } = new List<FabricExportLink>();

        [JsonProperty(PropertyName = "export", Order = 101)]
        public JObject Pipeline { get; set; }

        public PipelineExportInstruction(string name)
            : base(name, FabricUpgradeResourceTypes.DataPipeline)
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

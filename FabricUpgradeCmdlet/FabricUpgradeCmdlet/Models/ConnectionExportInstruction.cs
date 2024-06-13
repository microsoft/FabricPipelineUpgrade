using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace FabricUpgradeCmdlet.Models
{
    // Currently, the FabricUpgrader does not actually export Connections.
    // When it does, then we can "back-fill" this functionality.
    public class ConnectionExportInstruction : FabricExportInstruction
    {
        [JsonProperty(PropertyName = "resolve", Order = 100)]
        public List<FabricExportResolve> Resolves { get; set; } = new List<FabricExportResolve>();

        [JsonProperty(PropertyName = "export", Order = 101)]
        public JObject Export { get; set; } = new JObject();

        public ConnectionExportInstruction(string name)
            : base(name, FabricUpgradeResourceTypes.Connection)
        {
        }

        static public new ConnectionExportInstruction FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<ConnectionExportInstruction>(token);
        }

        public JObject ToJObject()
        {
            return (JObject)UpgradeSerialization.ToJToken(this);
        }
    }
}

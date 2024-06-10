using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Models
{
    /// <summary>
    /// Part of the communication between an Upgrader and an Exporter.
    /// This class tells the Exporter how to "finish" the item that it is exporting.
    /// Each of these typically tell the Exporter to copy the Fabric resource ID of another item.
    /// We cannot know the Fabric resource ID of another item until the Export operation.
    /// </summary>
    public class FabricExportLink
    {
        // Typically, the name of the other ADF resource.
        // May also the "$workspace", which means the workspaceId.
        [JsonProperty(PropertyName = "from")]
        public string From { get; set; }

        // Where in the Fabric resource JSON to place the value.
        [JsonProperty(PropertyName = "to")]
        public string To { get; set; }

        public FabricExportLink(
            string from,
            string to)
        {
            this.From = from;
            this.To = to;
        }

        public static FabricExportLink FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<FabricExportLink>(token);
        }
    }
}

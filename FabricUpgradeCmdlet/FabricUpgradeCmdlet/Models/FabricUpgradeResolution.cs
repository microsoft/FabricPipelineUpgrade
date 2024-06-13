using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using FabricUpgradeCmdlet.Utilities;
using System.Security.Policy;

namespace FabricUpgradeCmdlet.Models
{
    /// <summary>
    /// This class describes information that the client can send to assist the FabricUpgrade process.
    /// </summary>
    /// <remarks>
    /// For example, an Upgrade may need to "convert" an ADF LinkedService into a Fabric Connection.
    /// A Resolution like
    /// { type = "LinkedServiceToConnection", name = [LinkedService's name], value = [Connection's GUID] }
    /// will allow the Upgrader to perform this translation.
    ///
    /// These resolutions must come from the client; this Workload has no way of computing them.
    /// </remarks>
    public class FabricUpgradeResolution
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum ResolutionType
        {
            /// <summary>Every enum deserves an invalid value.</summary>
            Unknown = 0,

            /// <summary>This resolution converts a LinkedServiceName to a Connection ID.</summary>
            LinkedServiceToConnectionId = 1,

            /// <summary>This resolution converts an ADF Web(Hook)Activity's URL to a Connection ID and a relative URL.</summary>
            UrlHostToConnectionId = 2,

            /// <summary>This resolution holds the Credential Connection for a user.</summary>
            /// <remarks>
            /// This resolution is used in InvokePipeline.
            /// The key should be "user".
            /// </remarks>
            CredentialConnectionId = 3,
        }

        [DataMember(Name = "type")]
        [JsonProperty("type")]
        public ResolutionType Type { get; set; }

        // The content of this key depends on the ResolutionType.
        // In a LinkedServiceToConnection, this is the name of the ADF LinkedService.
        // In a UrlHostToConnection, this is the hostname of a Url.
        [DataMember(Name = "key")]
        [JsonProperty("key")]
        public string Key { get; set; }

        // The content of this value depends on the ResolutionType.
        // In a LinkedServiceToConnection, this is a Fabric ConnectionId.
        // For a UrlHostToConnection, this is a FabricConnectionId.
        [DataMember(Name = "value")]
        [JsonProperty("value")]
        public string Value { get; set; }

        public JToken ToJToken()
        {
            return UpgradeSerialization.ToJToken(this);
        }

        public override string ToString()
        {
            return UpgradeSerialization.Serialize(this);
        }
    }
}


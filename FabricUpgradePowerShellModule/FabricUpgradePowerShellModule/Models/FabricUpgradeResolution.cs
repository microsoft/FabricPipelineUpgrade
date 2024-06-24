// <copyright file="FabricUpgradeResolution.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using FabricUpgradePowerShellModule.Utilities;

namespace FabricUpgradePowerShellModule.Models
{
    /// <summary>
    /// This class describes information that the client can send to assist the FabricUpgrade process,
    /// or that the ExportMachine collects from each Fabric Resources as it Creates/Updates it.
    /// </summary>
    /// <remarks>
    /// For example, an Upgrade may need to "convert" an ADF LinkedService into a Fabric Connection.
    /// A Resolution like
    /// { type = "LinkedServiceToConnectionId", name = [LinkedService's name], value = [Connection's GUID] }
    /// will allow the Upgrader to perform this translation.
    /// These resolutions must come from the client; the Upgrader/Exporter has no way of computing them.
    ///
    /// For another example, an Export may need to include the ID of a previously exported Resource in the
    /// JSON for its own resource: InvokePipeline needs the ID of the other Pipeline.
    /// These resolutions cannot be computed until the Export phase.
    /// </remarks>
    public class FabricUpgradeResolution
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum ResolutionType
        {
            /// <summary>Every enum deserves an invalid value.</summary>
            Unknown = 0,

            /// <summary>The ID of the workspace into which this Resource is being Created/Updated.</summary>
            WorkspaceId = 1, 

            /// <summary>The ID of a previously Created/Updated Fabric Resource.</summary>
            AdfResourceNameToFabricResourceId = 2,

            /// <summary>This resolution converts a LinkedServiceName to a Connection ID.</summary>
            LinkedServiceToConnectionId = 3,

            /// <summary>This resolution converts an ADF Web(Hook)Activity's URL to a Connection ID and a relative URL.</summary>
            UrlHostToConnectionId = 4,

            /// <summary>This resolution holds the Credential Connection for a user.</summary>
            /// <remarks>
            /// This resolution is used in InvokePipeline.
            /// The key should be "user".
            /// </remarks>
            CredentialConnectionId = 5,
        }

        [DataMember(Name = "type")]
        [JsonProperty("type")]
        public ResolutionType Type { get; set; }

        // The content of this key depends on the ResolutionType.
        // In a LinkedServiceToConnectionId Resolution, this is the name of the ADF LinkedService.
        // In a UrlHostToConnectionId Resolution, this is the hostname of a Url.
        // In a CredentialConnectionId Resolution, this should be "user."
        [DataMember(Name = "key")]
        [JsonProperty("key")]
        public string Key { get; set; }

        // The content of this value could depend on the ResolutionType.
        // For all the current Resolutions, this is a Fabric ConnectionId.
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


// <copyright file="FabricUpgradeAlert.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using FabricUpgradeCmdlet.Utilities;

namespace FabricUpgradeCmdlet.Models
{
    /// <summary>
    /// An "alert" is a Warning, an Error, or some other information we need to send to the client.
    /// </summary>
    public class FabricUpgradeAlert
    {
        /// <summary>
        /// The severity of the failure.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public enum FailureSeverity
        {
            /// <summary>
            /// Invalid enumeration value.
            /// </summary>
            [EnumMember(Value = "unknown")]
            Unknown = 0,

            /// <summary>
            /// The Upgrade process completed, but some resources may need attention.
            /// </summary>
            /// <remarks>
            /// An unsupported Activity type should generate this in Compile.
            /// A Trigger should generate this in Compile.
            /// </remarks>
            [EnumMember(Value = "Warning")]
            Warning = 1,

            /// <summary>
            /// FabricUpgrade requires additional information about the resource described in Details.
            /// Typically, the caller should retry with this resource included in the next request's Resolutions.
            /// </summary>
            [EnumMember(Value = "RequiresUserAction")]
            RequiresUserAction = 2,

            /// <summary>
            /// Caller may retry the request without change.
            /// </summary>
            [EnumMember(Value = "Retryable")]
            Retryable = 3,

            /// <summary>
            /// FabricUpgrade does not (yet!) support the resource type named in Details.
            /// </summary>
            [EnumMember(Value = "UnsupportedResource")]
            UnsupportedResource = 4,

            /// <summary>
            /// The FabricUpgrade failed and no remedy is available.
            /// </summary>
            [EnumMember(Value = "Permanent")]
            Permanent = 5,
        }

        /// <summary>
        /// Gets or sets the severity of the failure.
        /// </summary>
        [JsonProperty(PropertyName = "severity", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
        public FailureSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the details of the failure.
        /// </summary>
        [JsonProperty(PropertyName = "details", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
        public string Details { get; set; }

        /// <summary>
        /// Gets or sets hints about the nature of an unresolved connection.
        /// A client/user can use this to find/create a Fabric Connection to
        /// associate with an ADF LinkedService.
        /// </summary>
        [JsonProperty(PropertyName = "connectionHint", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
        public FabricUpgradeConnectionHint ConnectionHint { get; set; }

        /// <summary>
        /// Gets or sets the 'template' for an unresolved connection.
        /// A client/user can use this to add a new FabricUpgradeResolution to the Resolutions.
        /// </summary>
        [JsonProperty(PropertyName = "resolutionTemplate", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
        public FabricUpgradeResolution ResolutionTemplate { get; set; }

        public JToken ToJToken()
        {
            return UpgradeSerialization.ToJToken(this);
        }
    }
}

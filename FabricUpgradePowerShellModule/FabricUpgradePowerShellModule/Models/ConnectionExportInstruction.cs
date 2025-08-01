﻿// <copyright file="ConnectionExportInstruction.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace FabricUpgradePowerShellModule.Models
{
    /// <summary>
    /// This class contains the instructions required to finish and then create/update a Connection Item.
    /// </summary>
    /// <remarks>
    /// See the ConnectionExporter for details of this operation.
    /// </remarks>
    public class ConnectionExportInstruction : FabricExportInstruction
    {
        // Each ResolveStep describes a property in the generated Resource should be set to the GUID
        // of a Fabric Connection that was manually created.
        [JsonProperty(PropertyName = "resolve", Order = 100)]
        public List<FabricExportResolveStep> Resolves { get; set; } = new List<FabricExportResolveStep>();

        // This object describes the connection that we will "pretend" to export.
        // It also contains connection "hints" just in case the Resolutions do not contain this connection.
        [JsonProperty(PropertyName = "export", Order = 101)]
        public JObject Export { get; set; } = new JObject();

        public ConnectionExportInstruction(string name)
            : base(FabricUpgradeResourceTypes.Connection, name, null)
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

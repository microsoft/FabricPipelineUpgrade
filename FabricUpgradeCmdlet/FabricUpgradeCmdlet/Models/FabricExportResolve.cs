// <copyright file="FabricExportResolve.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Models
{
    /// <summary>
    /// This class is part of the communication between an Upgrader and an Exporter.
    /// This class tells the Exporter how to "finish" the item that it is exporting.
    /// Each of these typically tell the Exporter which Connection ID needs to be found
    /// in the Resolutions from the client, and where in the Resource to insert that ID.
    /// We don't need Resolutions until Export, so we allow the user to postpone setting
    /// the Resolutions until then.
    /// Therefore, we need to finish this in the Export phase.
    /// </summary>
    public class FabricExportResolve
    {
        // Look for a resolution of this type.
        [JsonProperty(PropertyName = "type", Order = 1)]
        public FabricUpgradeResolution.ResolutionType Type { get; set; }

        // Look for a resolution with this 'key' (e.g., the LinkedService name).
        [JsonProperty(PropertyName = "key", Order = 2)]
        public string Key { get; set; }

        // Where in the Fabric resource JSON to place the value.
        [JsonProperty(PropertyName = "targetPath", Order = 3)]
        public string TargetPath { get; set; }

        // If we don't find this resolution during Export,
        // then include this hint in the Alert.
        [JsonProperty(PropertyName = "hint", Order = 4)]
        public FabricUpgradeConnectionHint Hint { get; set; }

        public FabricExportResolve(
            FabricUpgradeResolution.ResolutionType type,
            string key,
            string targetPath)
        {
            this.Type = type;
            this.Key = key;
            this.TargetPath = targetPath;
        }

        public FabricExportResolve WithHint(FabricUpgradeConnectionHint hint)
        {
            this.Hint = hint;
            return this;
        }

        public static FabricExportResolve FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<FabricExportResolve>(token);
        }
    }
}

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
    /// </summary>
    /// <remarks>
    /// Each ResolveStep does one of the following:
    /// 1) Tell the Exporter which Connection ID needs to be found in the Resolutions from the client,
    ///    and where in the Resource to insert that ID.
    /// 2) Tell the Exporter the name of a previously exported Resource whose ID needs to be included in
    ///    this Resource, and where in this Resource to insert that ID.
    /// We don't need Resolutions until Export, so we allow the user to postpone setting
    /// the Resolutions until then.
    /// Therefore, we need to finish this in the Export phase.
    /// </remarks>
    public class FabricExportResolveStep
    {
        // Look for a resolution of this type.
        [JsonProperty(PropertyName = "type", Order = 1)]
        public FabricUpgradeResolution.ResolutionType Type { get; set; }

        // Look for a resolution with this 'key' (e.g., the LinkedService name or the Pipeline name).
        [JsonProperty(PropertyName = "key", Order = 2)]
        public string Key { get; set; }

        // Where in the Fabric resource JSON to place the value.
        [JsonProperty(PropertyName = "targetPath", Order = 3)]
        public string TargetPath { get; set; }

        // If we don't find this resolution during Export,
        // then include this hint in the Alert.
        [JsonProperty(PropertyName = "hint", Order = 4)]
        public FabricUpgradeConnectionHint Hint { get; set; }

        public FabricExportResolveStep(
            FabricUpgradeResolution.ResolutionType type,
            string key,
            string targetPath)
        {
            this.Type = type;
            this.Key = key;
            this.TargetPath = targetPath;
        }

        public FabricExportResolveStep WithHint(FabricUpgradeConnectionHint hint)
        {
            this.Hint = hint;
            return this;
        }

        public static FabricExportResolveStep ForResourceId(
            string resourceAdfName,
            string targetPath)
        {
            return new FabricExportResolveStep(
                FabricUpgradeResolution.ResolutionType.AdfResourceNameToFabricResourceId,
                resourceAdfName,
                targetPath);
        }

        public static FabricExportResolveStep FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<FabricExportResolveStep>(token);
        }
    }
}

// <copyright file="FabricExportLink.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        [JsonProperty(PropertyName = "from", Order = 1)]
        public string From { get; set; }

        // Where in the Fabric resource JSON to place the value.
        [JsonProperty(PropertyName = "targetPath", Order = 2)]
        public string TargetPath { get; set; }

        public FabricExportLink(
            string from,
            string targetPath)
        {
            this.From = from;
            this.TargetPath = targetPath;
        }

        public static FabricExportLink FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<FabricExportLink>(token);
        }
    }
}

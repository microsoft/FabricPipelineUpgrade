// <copyright file="FabricExportInstruction.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Models
{
    public class FabricExportInstruction
    {
        [JsonProperty(PropertyName = "resourceName", Order = 1)]
        public string ResourceName { get; set; }

        [JsonProperty(PropertyName = "resourceType", Order = 2)]
        public FabricUpgradeResourceTypes ResourceType { get; set; }

        public FabricExportInstruction() { }

        protected FabricExportInstruction(
            string resourceName,
            FabricUpgradeResourceTypes resourceType)
        {
            this.ResourceName = resourceName;
            this.ResourceType = resourceType;
        }

        static public FabricExportInstruction FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<FabricExportInstruction>(token);
        }
    }
}

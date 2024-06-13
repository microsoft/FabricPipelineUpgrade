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
        [JsonProperty(PropertyName = "resourceType", Order = 1)]
        public FabricUpgradeResourceTypes ResourceType { get; set; }

        [JsonProperty(PropertyName = "resourceName", Order = 2)]
        public string ResourceName { get; set; }

        public FabricExportInstruction() { }

        protected FabricExportInstruction(
            FabricUpgradeResourceTypes resourceType,
            string resourceName
            )
        {
            this.ResourceType = resourceType;
            this.ResourceName = resourceName;
        }

        static public FabricExportInstruction FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<FabricExportInstruction>(token);
        }
    }
}

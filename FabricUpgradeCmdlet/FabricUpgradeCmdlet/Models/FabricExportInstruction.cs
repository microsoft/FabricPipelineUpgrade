// <copyright file="FabricExportInstruction.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Models
{
    /// <summary>
    /// The base class for ExportInstructions.
    /// </summary>
    /// <remarks>
    /// An ExportInstruction is an object that tells the ExportMachine and
    /// individual Exporters how to finish and create/update a Fabric Resource.
    /// </remarks>
    public class FabricExportInstruction
    {
        /// <summary>
        /// This is the type of resource that this instruction exports (DataPipeline or Connection).
        /// </summary>
        [JsonProperty(PropertyName = "resourceType", Order = 1)]
        public FabricUpgradeResourceTypes ResourceType { get; set; }

        /// <summary>
        /// The export process requires the display name of the resource.
        /// Also, we use this in the response to the user.
        /// Also, we use this during the "Link" phase during export to find the ID of previously exported resources.
        /// </summary>
        [JsonProperty(PropertyName = "resourceName", Order = 2)]
        public string ResourceName { get; set; }

        /// <summary>
        /// The export process requires the description of the resource.
        /// Since this is easily available during ConvertTo-FabricResources, we set it then.
        /// </summary>
        [JsonProperty(PropertyName = "resourceDescription", Order = 3)]
        public string ResourceDescription { get; set; }

        public FabricExportInstruction() { }

        protected FabricExportInstruction(
            FabricUpgradeResourceTypes resourceType,
            string resourceName,
            string resourceDescription)
        {
            this.ResourceType = resourceType;
            this.ResourceName = resourceName;
            this.ResourceDescription = resourceDescription;
        }

        static public FabricExportInstruction FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<FabricExportInstruction>(token);
        }
    }
}

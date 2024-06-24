// <copyright file="PipelineExportInstruction.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace FabricUpgradeCmdlet.Models
{
    /// <summary>
    /// This class contains the instructions required to finish and then create/update a DataPipeline Item.
    /// </summary>
    /// <remarks>
    /// Before a DataPipeline can be created/updated, we need to populate
    /// the IDs of other Fabric Resources and Connections.
    /// The instructions for doing so is found in the "resolve" step.
    /// </remarks>
    public class PipelineExportInstruction : FabricExportInstruction
    {
        // These Resolves describe which properties in the generated code should be set to the GUID
        // of a Fabric Resource that was previously Created/Updated.
        [JsonProperty(PropertyName = "resolve", Order = 100)]
        public List<FabricExportResolveStep> Resolves { get; set; } = new List<FabricExportResolveStep>();

        // This object describes the DataPipeline Resource that the PipelineExporter will create/update.
        [JsonProperty(PropertyName = "export", Order = 101)]
        public JObject Export { get; set; } = new JObject();

        public PipelineExportInstruction(
            string name,
            string description)
            : base(FabricUpgradeResourceTypes.DataPipeline, name, description)
        {
        }

        static public new PipelineExportInstruction FromJToken(JToken token)
        {
            return UpgradeSerialization.FromJToken<PipelineExportInstruction>(token);
        }

        public JObject ToJObject()
        {
            return (JObject)UpgradeSerialization.ToJToken(this);
        }
    }
}

// <copyright file="PipelineExportInstruction.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace FabricUpgradeCmdlet.Models
{
    public class PipelineExportInstruction : FabricExportInstruction
    {
        // These Links describe which properties in the generated code should be set to the
        // value of a Fabric Resource ID of a previously exported Resource.
        [JsonProperty(PropertyName = "link", Order = 100)]
        public List<FabricExportLink> Links { get; set; } = new List<FabricExportLink>();

        // These Resolves describe which properties in the generated code should be set to the GUID
        // of a Fabric Connection that was manually created.
        [JsonProperty(PropertyName = "resolve", Order = 101)]
        public List<FabricExportResolve> Resolves { get; set; } = new List<FabricExportResolve>();

        [JsonProperty(PropertyName = "export", Order = 102)]
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

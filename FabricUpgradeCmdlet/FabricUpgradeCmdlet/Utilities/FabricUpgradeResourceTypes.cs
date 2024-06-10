using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FabricUpgradeCmdlet.Utilities
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FabricUpgradeResourceTypes
    {
        Unknown = 0,

        DataPipeline = 1,

        Dataset = 2,

        LinkedService = 3,

        Trigger = 4,

        PipelineActivity = 5,
    }
}

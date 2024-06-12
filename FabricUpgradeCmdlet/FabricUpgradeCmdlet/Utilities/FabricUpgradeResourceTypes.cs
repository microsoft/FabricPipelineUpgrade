using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FabricUpgradeCmdlet.Utilities
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FabricUpgradeResourceTypes
    {
        Unknown = 0,

        // An ADF or Fabric Pipeline
        DataPipeline = 1,

        // An ADF Dataset
        Dataset = 2,

        // An ADF LinkedService
        LinkedService = 3,

        // An ADF Trigger (not currently supported)
        Trigger = 4,

        // An ADF or Fabric PipelineActivity
        PipelineActivity = 5,

        // A Fabric Connection
        Connection = 6,
    }
}

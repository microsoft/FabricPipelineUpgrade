// <copyright file="FabricUpgradeResourceTypes.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FabricUpgradePowerShellModule.Utilities
{
    /// <summary>
    /// The different kinds of ADF and Fabric resources handled by this module.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FabricUpgradeResourceTypes
    {
        Unknown = 0,

        // An ADF or Fabric Pipeline
        DataPipeline = 1,

        // An ADF or Fabric PipelineActivity
        PipelineActivity = 2,

        // An ADF Dataset
        Dataset = 3,

        // An ADF LinkedService
        LinkedService = 4,

        // A Fabric Connection
        Connection = 5,

        // An ADF Trigger (not currently supported)
        Trigger = 6,

        //A Fabric SQL Stored Procedure resource
        StoredProcedure = 7,
    }
}

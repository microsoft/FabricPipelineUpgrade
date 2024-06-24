// <copyright file="FabricUpgradeProgress.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using FabricUpgradePowerShellModule.Utilities;

namespace FabricUpgradePowerShellModule.Models
{
    public class FabricUpgradeProgress
    {
        public const string ImportedResourcesKey = "importedResources";
        public const string ExportableFabricResourcesKey = "exportableFabricResources";
        public const string ExportedFabricResourcesKey = "exportedFabricResources";

        /// <summary>
        /// The result of the FabricUpgrade operation.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public enum FabricUpgradeState
        {
            /// <summary>
            /// Invalid enumeration value.
            /// </summary>
            [EnumMember(Value = "Unknown")]
            Unknown = 0,

            /// <summary>
            /// FabricUpgrade is not (yet) implemented.
            /// </summary>
            [EnumMember(Value = "NotImplemented")]
            NotImplemented = 1,

            /// <summary>
            /// FabricUpgrade does not support the PackageFormat.
            /// </summary>
            [EnumMember(Value = "UnsupportedFormat")]
            UnsupportedFormat = 2,

            /// <summary>
            /// The FabricUpgrade is in progress.
            /// </summary>
            [EnumMember(Value = "InProgress")]
            InProgress = 3,

            /// <summary>
            /// The FabricUpgrade failed. See the Alerts field for more information.
            /// </summary>
            [EnumMember(Value = "Failed")]
            Failed = 4,

            /// <summary>
            /// The FabricUpgrade has completed successfully.
            /// There may be Warnings in the Alerts field.
            /// </summary>
            [EnumMember(Value = "Succeeded")]
            Succeeded = 5,
        }

        /// <summary>
        /// Gets or sets the state of the FabricUpgrade.
        /// </summary>
        [JsonProperty(PropertyName = "state", Order = 10)]
        public FabricUpgradeState State { get; set; }

        /// <summary>
        /// Gets or sets the details of the FabricUpgrade.
        /// </summary>
        [JsonProperty(PropertyName = "alerts", Order = 20)]
        public List<FabricUpgradeAlert> Alerts { get; set; } = new List<FabricUpgradeAlert>();

        /// <summary>
        /// A "result" object, whose contents vary by which method returns it.
        /// </summary>
        [JsonProperty(PropertyName = "result", Order = 30)]
        public JObject Result { get; set; } = new JObject();

        /// <summary>
        /// The Resolutions of LinkedServices to Connection IDs.
        /// </summary>
        /// <remarks>
        /// The Resolutions may be set at any time during the Import/Upgrade/Export process,
        /// so they are carried along in the Progress.
        /// </remarks>
        [JsonProperty(PropertyName = "resolutions", Order = 40)]
        public List<FabricUpgradeResolution> Resolutions { get; set; } = new List<FabricUpgradeResolution>();

        /// <summary>
        /// Add an Alert to the Progress.
        /// </summary>
        /// <param name="alert">The Alert to add.</param>
        /// <returns>this, for chaining.</returns>
        public FabricUpgradeProgress WithAlert(FabricUpgradeAlert alert)
        {
            this.Alerts.Add(alert);
            return this;
        }

        public override string ToString()
        {
            return UpgradeSerialization.Serialize(this);
        }

        public JObject ToJObject()
        {
            return JObject.Parse(this.ToString());
        }

        public static FabricUpgradeProgress FromString(string fup)
        {
            if (string.IsNullOrEmpty(fup))
            {
                return new FabricUpgradeProgress() 
                {
                    State = FabricUpgradeState.Succeeded,
                    Alerts = new List<FabricUpgradeAlert>(),
                };
            }

            return JsonConvert.DeserializeObject<FabricUpgradeProgress>(fup);
        }

        public static FabricUpgradeProgress FromJToken(JToken jToken)
        {
            return UpgradeSerialization.FromJToken<FabricUpgradeProgress>(jToken);
        }

    }
}

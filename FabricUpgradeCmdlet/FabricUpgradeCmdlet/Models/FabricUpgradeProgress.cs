using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using FabricUpgradeCmdlet.Utilities;

namespace FabricUpgradeCmdlet.Models
{
    public class FabricUpgradeProgress
    {
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
            /// The FabricUpgrade failed. See the Failures field for more information.
            /// </summary>
            [EnumMember(Value = "Failed")]
            Failed = 4,

            /// <summary>
            /// The FabricUpgrade has completed successfully.
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

        [JsonProperty(PropertyName = "result", Order = 40)]
        public JObject Result { get; set; } = new JObject();

        public FabricUpgradeProgress WithAlert(FabricUpgradeAlert alert)
        {
            this.Alerts.Add(alert);
            return this;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public JObject ToJObject()
        {
            return JObject.Parse(this.ToString());
        }

        public static FabricUpgradeProgress FromString(string fur)
        {
            if (string.IsNullOrEmpty(fur))
            {
                return new FabricUpgradeProgress() { State = FabricUpgradeState.Succeeded };
            }

            return JsonConvert.DeserializeObject<FabricUpgradeProgress>(fur);
        }

        public static FabricUpgradeProgress FromJToken(JToken jToken)
        {
            return UpgradeSerialization.FromJToken<FabricUpgradeProgress>(jToken);
        }

    }
}

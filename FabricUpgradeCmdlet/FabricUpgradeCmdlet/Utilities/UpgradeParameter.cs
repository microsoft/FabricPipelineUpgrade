// <copyright file="UpgradeExpressions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace FabricUpgradeCmdlet.Utilities
{
    /// <summary>
    /// An UpgradeParameter describes a parameter from an ADF Resource.
    /// UpgradeParameters are built from (e.g.) a Dataset's 'parameters' property.
    /// </summary>
    public class UpgradeParameter
    {
        private JToken parameterValue;
        private string parameterType;

        public UpgradeParameter Clone()
        {
            return new UpgradeParameter()
            {
                parameterValue = this.parameterValue?.DeepClone(),
                parameterType = this.parameterType,
            };
        }

        public static UpgradeParameter FromDefaultValueToken(
            JToken parameterToken)
        {
            ParameterElement element = UpgradeSerialization.FromJToken<ParameterElement>(parameterToken);
            return new UpgradeParameter()
            {
                parameterType = element.Type,
                parameterValue = element.DefaultValue,
            };
        }

        public UpgradeParameter WithValue(JToken newValue)
        {
            this.parameterValue = newValue.DeepClone();
            return this;
        }

        // If the parameter is "all by itself,"
        // like "@dataset().fileName",
        // then use this value.
        public JToken StandaloneValue
        {
            get => this.parameterValue;
        }


        // If the parameter is part of an expression,
        // like "@concat(dataset().fileName, '.json')",
        // then use this value.
        // This way, the result will be "@concat('otter', '.json')
        public JToken IntegratedValue
        {
            get
            {
                if (this.parameterValue == null) return null;

                if (this.parameterType.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    return "'" + this.parameterValue.ToString() + "'";
                }

                return this.parameterValue;
            }
        }

        private class ParameterElement
        {
            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; }

            [JsonProperty(PropertyName = "defaultValue")]
            public JToken DefaultValue { get; set; }

            public ParameterElement Clone()
            {
                return new ParameterElement()
                {
                    Type = this.Type,
                    DefaultValue = this.DefaultValue.DeepClone(),
                };
            }
        }

    }
}

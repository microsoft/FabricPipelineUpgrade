// <copyright file="UpgradeExpressions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Utilities
{
    public class UpgradeParameter
    {
        public string ParameterName { get; set; }
        
        // If the parameter is "all by itself,"
        // like "@dataset().fileName",
        // then use this value.
        public JToken StandaloneValue { get; set; }

        // If the parameter is part of an expression,
        // like "@concat(dataet().fileName, '.json')",
        // then use this value.
        public JToken IntegratedValue { get; set; }

        public static UpgradeParameter FromJToken(
            JToken parameterToken)
        {
            ParameterElement element = UpgradeSerialization.FromJToken<ParameterElement>(parameterToken);
            if (element.DefaultValue == null)
            {
                return new UpgradeParameter()
                {
                    StandaloneValue = null,
                    IntegratedValue = null, //element.Type.Equals("string", StringComparison.OrdinalIgnoreCase) ? "''" : null,
                };
            }
            else
            {
                JToken integratedValue = element.DefaultValue;
                if (element.Type.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    integratedValue = "'" + element.DefaultValue.ToString() + "'";
                }

                return new UpgradeParameter()
                {
                    StandaloneValue = element.DefaultValue,
                    IntegratedValue = integratedValue,
                };
            }
        }

        private class ParameterElement
        {
            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; }

            [JsonProperty(PropertyName = "defaultValue")]
            public JToken DefaultValue { get; set; }
        }

    }
}

// <copyright file="ResourceParameters.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Utilities
{
    /// <summary>
    /// This class manages the Parameters for a resource, like Dataset.
    /// It is initialized with the default values.
    /// When a resource (Dataset or LinkedService) is asked to resolve a Symbol, this class
    /// produces a "context" for the resolution of the Symbol.
    /// This context is a merge of the default values and the overrides from the resolve requester.
    /// </summary>
    public class ResourceParameters
    {
        private string prefix;
        private readonly Dictionary<string, Parameter> parameters = new Dictionary<string, Parameter>();

        public ResourceParameters()
            :this(null, null)
        {
        }

        private ResourceParameters(
            string prefix,
            Dictionary<string, Parameter> parameters)
        {
            this.prefix = (prefix == null) ? string.Empty : prefix + ".";
            this.parameters = parameters ?? new Dictionary<string, Parameter>();
        }

        /// <summary>
        /// Initialize a ResourceParameters object from the contents of the ADF Resource.
        /// </summary>
        /// <param name="parameterDeclaration">The Dictionary from the ADF Resource.</param>
        /// <param name="prefix">Prepend this to each symbol name, to facilitate lookups.</param>
        /// <returns>A ResourceParameters object.</returns>
        public static ResourceParameters FromResourceDeclaration(
            Dictionary<string, JToken> parameterDeclaration,
            string prefix)
        {
            Dictionary<string, Parameter> declaredParameters = new Dictionary<string, Parameter>();
            foreach (var declaration in parameterDeclaration ?? new Dictionary<string, JToken>())
            {
                declaredParameters[prefix + "." + declaration.Key] = Parameter.FromDefaultValueToken(declaration.Value);
            }

            return new ResourceParameters(prefix, declaredParameters);
        }


        /// <summary>
        /// Initialize a ResourceParameters object from the contents of the ADF Resource.
        /// </summary>
        /// <remarks>
        /// This method performs the transformation from JToken to Dictionary and calls the "other" FromResourceDeclaration().
        /// </remarks>
        /// <param name="parameterDeclaration">The JObject from the ADF Resource.</param>
        /// <param name="prefix">Prepend this to each symbol name, to facilitate lookups.</param>
        /// <returns>A ResourceParameters object.</returns>
        public static ResourceParameters FromResourceDeclaration(
            JToken parameterDeclaration,
            string prefix)
        {
            return FromResourceDeclaration(((JObject)parameterDeclaration)?.ToDictionary<string, JToken>(), prefix);
        }


        /// <summary>
        /// Combine this ResourceParameters object with the override values to produce a new ResourceParameters object.
        /// </summary>
        /// <remarks>
        /// When a Copy Activity asks a Dataset to build its datasetSettings Symbol, the Copy Activity passes
        /// the 'override' values that overwrite the Dataset's default values.
        /// </remarks>
        /// <param name="parameterAssignments">Override the current values with the contents of this dictionary.</param>
        /// <returns>A new ResourceParameters with the updated values.</returns>
        public ResourceParameters BuildResolutionContext(
            Dictionary<string, JToken> parameterAssignments)
        {
            Dictionary<string, Parameter> activeParameters = new Dictionary<string, Parameter>();

            foreach (var myParam in this.parameters)
            {
                activeParameters[myParam.Key] = myParam.Value?.Clone();
            }

            foreach (var incomingParam in parameterAssignments ?? new Dictionary<string, JToken>())
            {
                string key = this.prefix + incomingParam.Key;
                activeParameters[key] = activeParameters[key].WithValue(incomingParam.Value);
            }

            return new ResourceParameters(this.prefix, activeParameters);
        }

        /// <summary>
        /// Does this object have a parameter with this name?
        /// </summary>
        /// <param name="parameterName">The name to check.</param>
        /// <returns>True if and only if this object has a parameter with this name.</returns>
        public bool ContainsParameterName(string parameterName)
        {
            return this.parameters.ContainsKey(parameterName);
        }

        // If the parameter is "all by itself,"
        // like "@dataset().fileName",
        // then use this value.
        public JToken StandaloneValue(
            string parameterName)
        {
            return this.parameters[parameterName].StandaloneValue;
        }


        // If the parameter is part of an expression,
        // like "@concat(dataset().fileName, '.json')",
        // then use this value.
        // This way, the result will be something like "@concat('otter', '.json')
        public JToken IntegratedValue(
            string parameterName)
        {
            return this.parameters[parameterName].IntegratedValue;
        }

        /// <summary>
        /// An UpgradeParameter describes a parameter declared in an ADF Resource.
        /// UpgradeParameters are built from (e.g.) a Dataset's 'parameters' property.
        /// </summary>
        private class Parameter
        {
            private JToken parameterValue;
            private string parameterType;

            public Parameter Clone()
            {
                return new Parameter()
                {
                    parameterValue = this.parameterValue?.DeepClone(),
                    parameterType = this.parameterType,
                };
            }

            public static Parameter FromDefaultValueToken(
                JToken parameterToken)
            {
                AdfParameterModel element = UpgradeSerialization.FromJToken<AdfParameterModel>(parameterToken);
                return new Parameter()
                {
                    parameterType = element.Type,
                    parameterValue = element.DefaultValue,
                };
            }

            public Parameter WithValue(JToken newValue)
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
            // This way, the result will be something like "@concat('otter', '.json')
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

            private class AdfParameterModel
            {
                [JsonProperty(PropertyName = "type")]
                public string Type { get; set; }

                [JsonProperty(PropertyName = "defaultValue")]
                public JToken DefaultValue { get; set; }

                public AdfParameterModel Clone()
                {
                    return new AdfParameterModel()
                    {
                        Type = this.Type,
                        DefaultValue = this.DefaultValue.DeepClone(),
                    };
                }
            }
        }
    }
}

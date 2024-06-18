// <copyright file="ResourceParameters.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Utilities
{
    /// <summary>
    /// An UpgradeParameter describes a parameter from an ADF Resource.
    /// UpgradeParameters are built from (e.g.) a Dataset's 'parameters' property.
    /// </summary>
    public class ResourceParameters
    {
        private readonly Dictionary<string, UpgradeParameter> parameters = new Dictionary<string, UpgradeParameter>();

        public static ResourceParameters FromResourceDeclaration(
            Dictionary<string, JToken> parameterDeclaration,
            string prefix)
        {
            ResourceParameters resourceParams = new ResourceParameters();

            foreach (var declaration in parameterDeclaration)
            {
                resourceParams.parameters[prefix + "." + declaration.Key] = UpgradeParameter.FromDefaultValueToken(declaration.Value);
            }

            return resourceParams;
        }

        public ResourceParameters WithValue(
            string parameterName,
            JToken newValue)
        {
            this.parameters[parameterName] = this.parameters[parameterName].WithValue(newValue);
            return this;
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
        // This way, the result will be "@concat('otter', '.json')
        public JToken IntegratedValue(
            string parameterName)
        {
            return this.parameters[parameterName].IntegratedValue;
        }
    }
}

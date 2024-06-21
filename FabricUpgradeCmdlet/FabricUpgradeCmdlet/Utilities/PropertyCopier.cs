// <copyright file="PropertyCopier.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Utilities
{
    /// <summary>
    /// This class is used by Upgraders to accelerate copying properties from
    /// the ADF resource JSON to the Fabric resource JSON.
    /// This class takes care of validating and resolving expressions.
    /// </summary>
    public class PropertyCopier
    {
        // The path to the ADF resource, for alerting.
        private readonly string resourcePath;

        // Copy values from this object.
        private readonly JToken adfResourceToken;

        // Copy values to this object.
        private readonly JObject fabricResourceObject;

        // Parameters that can be used to resolve Dataset or LinkedSevice parameters in an Expression.
        private readonly ResourceParameters parameters;

        private readonly AlertCollector alerts;

        public PropertyCopier(
            string resourcePath,
            JToken adfResourceToken,
            JObject fabricResourceObject,
            ResourceParameters parameters,
            AlertCollector alerts)
        {
            this.resourcePath = resourcePath;
            this.adfResourceToken = adfResourceToken;
            this.fabricResourceObject = fabricResourceObject;
            this.parameters = parameters ?? new ResourceParameters();
            this.alerts = alerts;
        }

        public PropertyCopier(
            string resourcePath,
            JToken adfResourceToken,
            JObject fabricResourceObject,
            AlertCollector alerts)
            : this(resourcePath, adfResourceToken, fabricResourceObject, null, alerts)
        {
        }

        /// <summary>
        /// Set the Fabric resource property described by 'targetPath' to the value token.
        /// </summary>
        /// <remarks>
        /// This method does not validate nor resolve the value token.
        /// </remarks>
        /// <param name="targetPath">Where to put the value.</param>
        /// <param name="value">The value to put there.</param>
        public void Set(
            string targetPath,
            JToken value)
        {
            // Ensure that the target path exists in the Fabric resource JSON.
            // If it does not, then build that target path.

            // TODO: Handle Arrays!

            string[] targetPathParts = targetPath.Split(".");

            JObject target = fabricResourceObject;
            for (int nPart = 0; nPart < targetPathParts.Length - 1; nPart++)
            {
                string dp = targetPathParts[nPart];
                if (!target.ContainsKey(dp))
                {
                    target[dp] = new JObject();
                }

                target = (JObject)target[dp];
            }

            // Set the Fabric resource property to the value.
            string property = targetPathParts[targetPathParts.Length - 1];
            target[property] = value?.DeepClone();
        }

        /// <summary>
        /// Copy a property from the ADF resource JSON to the Fabric resource JSON.
        /// Validate the property, to ensure that it does not contain any forbidden expression values.
        /// "Resolve" the property to remove references to Dataset or LinkedService parameters.
        /// </summary>
        /// <param name="from">Where to get the value in the ADF resource JSON.</param>
        /// <param name="to">Where to put the value in the Fabric resource JSON.</param>
        public void Copy(
            string from,
            string to,
            bool allowNull = true,
            bool copyIfNull = true)
        {
            JToken value = adfResourceToken.SelectToken(from);

            if (value == null && !allowNull)
            {
                alerts.AddPermanentError($"{resourcePath}/{from} must not be null.");

                // We allow the operation to continue, to support the WhatIf scenario.
                // (To wit: the user just wants to run ConvertTo-FabricResources to see what happens).
            }

            if (copyIfNull || value != null)
            {
                this.ValidateAndResolveAdfToken(from, value, out JToken newValue);
                Set(to, newValue);
            }
        }

        /// <summary>
        /// Copy a property from the ADF resource JSON to the Fabric resource JSON.
        /// Also, validate the property, to ensure that it does not contain any forbidden expression values.
        /// </summary>
        /// <remarks>
        /// This method is a shortcut for the common case that the path to the ADF property
        /// is the same as the path to the Fabric property.
        /// </remarks>
        /// <param name="path">Where to get the value in the ADF resource JSON and put the value in the Fabric resource JSON.</param>
        public void Copy(
            string path,
            bool allowNull = true,
            bool copyIfNull = true)
        {
            Copy(path, path, allowNull, copyIfNull);
        }

        /// <summary>
        /// Ensure that the token value does not contain an invalid expression.
        /// The definition of "invalid" can be found in TODO.
        /// If the token is valid, then resolve any references to Dataset or LinkedService parameters.
        /// </summary>
        /// <param name="path">The path to the token, for Alerting purposes.</param>
        /// <param name="token">The token to recursively validate.</param>
        /// <param name="finalExpression">
        /// If the original token was an expression with Dataset or LinkedService parameter,
        /// then this holds the expression that resolves those parameters.
        /// </param>
        /// <returns>True if and only if the expression is valid.</returns>
        private bool ValidateAndResolveAdfToken(
            string path,
            JToken token,
            out JToken finalExpression)
        {
            if (IsAtom(token))
            {
                finalExpression = token;
                return true;
            }

            if (IsExpression(token))
            {
                // ValidateAndResolveExpression will add an alert for any invalid expression.
                return this.ValidateAndResolveExpression(path, token, out finalExpression);
            }

            if (token.Type == JTokenType.Object)
            {
                JObject valueObject = (JObject)token;

                JObject newValueObject = new JObject();

                bool valid = true;
                foreach (var subToken in valueObject)
                {
                    valid &= this.ValidateAndResolveAdfToken(path + "." + subToken.Key, subToken.Value, out JToken newToken);
                    newValueObject[subToken.Key] = newToken;
                }
                finalExpression = newValueObject;
                return valid;
            }
            else if (token.Type == JTokenType.Array)
            {
                JArray valueArray = (JArray)token;

                JArray newValueArray = new JArray();

                bool valid = true;
                for (int n = 0; n < valueArray.Count; n++)
                {
                    valid &= this.ValidateAndResolveAdfToken(path + $"[{n}]", valueArray[n], out JToken newToken);
                    newValueArray.Add(newToken);
                }
                finalExpression = newValueArray;
                return valid;
            }

            finalExpression = token;
            return false;
        }

        /// <summary>
        /// Check a token to see if it is an Expression.
        /// </summary>
        /// <param name="testToken">The token to check.</param>
        /// <returns>True if and only if this token is an Expression.</returns>
        private bool IsExpression(JToken testToken)
        {
            if (testToken == null || testToken.Type != JTokenType.Object)
            {
                return false;
            }

            JObject testObject = (JObject)testToken;
            return 
                testObject.Count == 2 &&
                testObject.ContainsKey("type") &&
                testObject["type"].ToString() == "Expression" &&
                testObject.ContainsKey("value");
        }

        /// <summary>
        /// A token is an "atom" if it is not an object and is not an array.
        /// </summary>
        /// <param name="testToken">The token to examine.</param>
        /// <returns>True if and only if this is an "atom."</returns>
        private bool IsAtom(JToken testToken)
        {
            if (testToken == null)
            {
                return true;
            }

            return testToken.Type != JTokenType.Object && testToken.Type != JTokenType.Array;
        }

        /// <summary>
        /// Use UpgradeExpression to validate and resolve the expression.
        /// </summary>
        /// <param name="path">The path to the expression, for alerting purposes.</param>
        /// <param name="expressionToken">The expression to validate and resolve.</param>
        /// <param name="finalExpression">
        /// If the original token was an expression with Dataset or LinkedService parameter,
        /// then this holds the expression that resolves those parameters.
        /// </param>
        /// <returns>True if and only if the expression is valid.</returns>
        private bool ValidateAndResolveExpression(
            string path,
            JToken expressionToken,
            out JToken finalExpression)
        {
            finalExpression = expressionToken;
            if (!this.IsExpression(expressionToken))
            {
                return true;
            }

            string originalExpression = ((JObject)expressionToken)["value"].ToString();
            UpgradeExpression expressionModel = new UpgradeExpression(this.resourcePath + "." + path, originalExpression);
            expressionModel.ApplyParameters(this.parameters);

            if (expressionModel.Validate(this.alerts))
            {
                finalExpression = expressionModel.RebuildExpression();
                return true;
            }

            return false;
        }
    }
}

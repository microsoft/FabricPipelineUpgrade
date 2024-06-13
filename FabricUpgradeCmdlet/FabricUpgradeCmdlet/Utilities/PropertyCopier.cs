// <copyright file="PropertyCopier.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Utilities
{
    /// <summary>
    /// This class is used by Upgraders to accelerate copying properties from
    /// the ADF resource JSON to the Fabric resource JSON.
    /// </summary>
    public class PropertyCopier
    {
        private readonly string resourcePath;
        private readonly JToken adfResourceToken;
        private readonly JObject fabricResourceObject;
        private readonly AlertCollector alerts;

        public PropertyCopier(
            string resourcePath,
            JToken adfResourceToken,
            JObject fabricResourceObject,
            AlertCollector alerts)
        {
            this.resourcePath = resourcePath;
            this.adfResourceToken = adfResourceToken;
            this.fabricResourceObject = fabricResourceObject;
            this.alerts = alerts;
        }

        /// <summary>
        /// Set the Fabric resource property described by 'targetPath' to the token.
        /// </summary>
        /// <param name="targetPath">Where to put the value.</param>
        /// <param name="value">The value to put there.</param>
        public void Set(
            string targetPath,
            JToken value)
        {
            // Ensure that the target path exists in the Fabric resource JSON.
            // TODO: This doesn't handle Arrays!
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
        /// Also, validate the property, to ensure that it does not contain any forbidden expression values.
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
                // (To wit: the user just wants to run ConvertTo-FabricPipeline to see what happens).
            }

            if (copyIfNull || value != null)
            {
                ValidateAdfToken(from, value);
                Set(to, value);
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
        /// </summary>
        /// <param name="path">The path to the token, for Alerting purposes.</param>
        /// <param name="token">The token to recursively validate.</param>
        /// <returns></returns>
        private bool ValidateAdfToken(
             string path,
             JToken token)
        {
            if (IsAtom(token))
            {
                return true;
            }

            if (IsExpression(token))
            {
                // ValidateExpression will add an alert for any invalid expression.
                return ValidateExpression(path, token);
            }

            if (token.Type == JTokenType.Object)
            {
                JObject valueObject = (JObject)token;

                bool valid = true;
                foreach (var subToken in valueObject)
                {
                    valid &= ValidateAdfToken(path + "." + subToken.Key, subToken.Value);
                }

                return valid;
            }
            else if (token.Type == JTokenType.Array)
            {
                JArray valueArray = (JArray)token;

                bool valid = true;
                for (int n = 0; n < valueArray.Count; n++)
                {
                    valid &= ValidateAdfToken(path + $"[{n}]", valueArray[n]);
                }
                return valid;
            }


            return false;
        }

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
        /// <returns></returns>
        private bool IsAtom(JToken testToken)
        {
            if (testToken == null)
            {
                return true;
            }

            return testToken.Type != JTokenType.Object && testToken.Type != JTokenType.Array;
        }

        private bool ValidateExpression(
            string path,
            JToken expression)
        {
            // TODO: Ensure that this expression does not include global configuration, dataset parameters, etc.
            // TODO: Make dataset parameters work!

            // this.alerts.AddPermanentError($"Cannot process expression found at {this.resourcePath}/{path}.");
            // return false;
            return true;
        }
    }
}

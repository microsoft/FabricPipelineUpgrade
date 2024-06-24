// <copyright file="TestHttpClientFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModuleTests.Utilities
{
    internal static class JsonUtils
    {
        /// <summary>
        /// Execute a "deep" comparison of two JTokens, and report the mismatches.
        /// </summary>
        /// <param name="expected">The expected JToken.</param>
        /// <param name="actual">The actual JToken.</param>
        /// <param name="key">The key being compared, for recursion.</param>
        /// <returns>A JObject describing the mismatches.</returns>
        public static JObject DeepCompare(JToken expected, JToken actual, string key = "")
        {
            JObject mismatches = new JObject();

            if (expected.Type != actual.Type)
            {
                if (!(expected.Type == JTokenType.Null && actual.Type == JTokenType.String && actual.Value<string>() == null))
                {
                    mismatches["typeMismatch"] = $"Expected type {expected.Type}, Actual type {actual.Type}";
                }
            }
            else if (expected.Type == JTokenType.Object)
            {
                JObject eObj = (JObject)expected;
                JObject aObj = (JObject)actual;

                foreach (var x in eObj)
                {
                    string expectedKey = x.Key;

                    if (aObj.ContainsKey(expectedKey))
                    {
                        JObject childMismatches = JsonUtils.DeepCompare(eObj[expectedKey], aObj[expectedKey], expectedKey);
                        if (childMismatches != null)
                        {
                            mismatches[expectedKey] = childMismatches;
                        }
                    }
                    else
                    {
                        mismatches[expectedKey] = "Missing in Actual";
                    }
                }

                foreach (var x in aObj)
                {
                    string actualKey = x.Key;

                    if (!eObj.ContainsKey(actualKey))
                    {
                        mismatches[actualKey] = "Only in Actual";
                    }
                }
            }
            else if (expected.Type == JTokenType.Array)
            {
                JArray eArr = (JArray)expected;
                JArray aArr = (JArray)actual;

                if (eArr.Count() != actual.Count())
                {
                    mismatches["countMismatch"] = $"Expected has {eArr.Count()} elements, Actual has {aArr.Count()} elements";
                }

                int count = Math.Min(eArr.Count(), aArr.Count());

                for (int index = 0; index < count; index++)
                {
                    JObject elementMismatch = JsonUtils.DeepCompare(eArr[index], aArr[index]);
                    if (elementMismatch != null)
                    {
                        mismatches[$"{index}"] = elementMismatch;
                    }
                }
            }
            else if (!JToken.DeepEquals(expected, actual))
            {
                mismatches["valueMismatch"] = $"Expected value {expected}, Actual value {actual}";
            }

            if (mismatches.Count > 0)
            {
                return mismatches;
            }

            return null;
        }
    }
}

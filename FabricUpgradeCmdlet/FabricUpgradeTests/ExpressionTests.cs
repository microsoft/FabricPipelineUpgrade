// <copyright file="ExpressionTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using FabricUpgradeTests.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace FabricUpgradeTests
{
    [TestClass]
    public class ExpressionTests
    {
        [TestMethod]
        [DataRow("ApplyParameters_SingletonString_DefaultOnly")]
        [DataRow("ApplyParameters_SingletonString_DefaultAndConstOverride")]
        [DataRow("ApplyParameters_SingletonString_DefaultAndExpressionOverride")]

        [DataRow("ApplyParameters_SingletonInt_DefaultOnly")]
        [DataRow("ApplyParameters_SingletonInt_DefaultAndConstOverride")]
        [DataRow("ApplyParameters_SingletonInt_DefaultAndExpressionOverride")]
        
        [DataRow("ApplyParameters_DefaultOnly")]
        [DataRow("ApplyParameters_DefaultAndConstOverride")]
        [DataRow("ApplyParameters_DefaultAndExpressionOverride")]
        public void ApplyParameter_Test(
            string testConfigFile)
        {
            var testConfig = ApplyParameterTestConfig.LoadFromFile(testConfigFile);

            ResourceParameters declaredParameters = ResourceParameters.FromResourceDeclaration(testConfig.ParameterDeclarations, "dataset()");
            ResourceParameters activeParameters = declaredParameters.BuildResolutionContext(testConfig.ValuesFromCaller);

            UpgradeExpression expressionModel = new UpgradeExpression("", testConfig.OriginalExpression);

            expressionModel.ApplyParameters(activeParameters);

            var actualExpression = expressionModel.RebuildExpression();

            var mismatches = JsonUtils.DeepCompare(testConfig.ExpectedExpression, actualExpression);
            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{testConfig.ExpectedExpression}\n\nACTUAL:\n{actualExpression}");
        }

        private class ApplyParameterTestConfig
        {
            [JsonProperty(PropertyName = "originalExpression")]
            public string OriginalExpression { get; set; }

            [JsonProperty(PropertyName = "parameterDeclarations")]
            public Dictionary<string, JToken> ParameterDeclarations { get; set; } = new Dictionary<string, JToken>();

            [JsonProperty(PropertyName = "valuesFromCaller")]
            public Dictionary<string, JToken> ValuesFromCaller { get; set; } = new Dictionary<string, JToken>();

            [JsonProperty(PropertyName = "expectedExpression")]
            public JToken ExpectedExpression { get; set; }

            public static ApplyParameterTestConfig LoadFromFile(string testFilename)
            {
                string testConfig = File.ReadAllText("./TestFiles/" + testFilename + ".json");
                return JsonConvert.DeserializeObject<ApplyParameterTestConfig>(testConfig);
            }

        }
    }
}
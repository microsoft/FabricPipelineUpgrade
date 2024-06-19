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
        [DataRow(
            "@dataset().param[1].prop1) ,",
            false,
            "@", "dataset().param[1].prop1", ")", ", ")]
        [DataRow(
            "@dataset () .param[1].prop1) ,",
            false,
            "@", "dataset().param[1].prop1", ")", ", ")]
        [DataRow(
            "@pipeline\t ().param[1].prop1) ,",
            true,
            "@", "pipeline().param[1].prop1", ")", ", ")]
        [DataRow(
            "@concat(utcNow(),  \r\npipeline ().Pipeline) ,",
            false,  // pipeline().Pipeline is not allowed
            "@", "concat", "(", "utcNow()", ", ", "pipeline().Pipeline", ")", ", ")]
        [DataRow(
            "@concat(utcNow(), 'linkedService().param[1].prop1,x') ,",
            true,   // the linkedService() is inside of a string, so it's okay.
            "@", "concat", "(", "utcNow()", ", ", "'linkedService().param[1].prop1,x'", ")", ", ")]
        [DataRow(
            "@concat(utcNow(),'pipeline().param[1].prop1,\\'x') ,",
            true,
            "@", "concat", "(", "utcNow()", ", ", "'pipeline().param[1].prop1,\\'x'", ")", ", ")]
        public void ValidateExpression_Test(
            string expression,
            bool expectedValid,
            params string[] expectedTokens)
        {
            AlertCollector alerts = new AlertCollector();
            UpgradeExpression ex = new UpgradeExpression("<path>", expression);
            bool actualValid = ex.Validate(alerts);

            Assert.AreEqual(expectedValid, actualValid);

            List<JToken> tokens = ex.Tokens();

            Assert.AreEqual(expectedTokens.Count(), tokens.Count);

            for (int nToken = 0; nToken < tokens.Count; nToken++)
            {
                Assert.AreEqual(expectedTokens[nToken], tokens[nToken]);
            }
        }

        /*
        [TestMethod]
        [DataRow("@concat(dataset().fileName, string(dataset().fileIndex), '.json')", "@concat('otter', string(0), '.json')")]
        public void ApplyParameter_Test(
            string originalExpression,
            string expectedUpdatedExpression)
        {
            AlertCollector alerts = new AlertCollector();

            UpgradeExpression ex = new UpgradeExpression("<path>", originalExpression);

            Dictionary<string, UpgradeParameter> parameters = new Dictionary<string, UpgradeParameter>();
            parameters.Add("dataset().fileName", UpgradeParameter.FromDefaultValueToken(JObject.Parse("{'type':'string','defaultValue':'otter'}")));
            parameters.Add("dataset().fileIndex", UpgradeParameter.FromDefaultValueToken(JObject.Parse("{'type':'integer','defaultValue':0}")));

            ex.ApplyParameters(new ResourceParameters(parameters));

            string actualUpdatedExpression = string.Join(string.Empty, ex.Tokens());

            Assert.AreEqual(expectedUpdatedExpression, actualUpdatedExpression);
        }
        */

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
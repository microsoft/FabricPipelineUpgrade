// <copyright file="ExpressionTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
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

            List<string> tokens = ex.Tokens();

            Assert.AreEqual(expectedTokens.Count(), tokens.Count);

            for (int nToken = 0; nToken < tokens.Count; nToken++)
            {
                Assert.AreEqual(expectedTokens[nToken], tokens[nToken]);
            }
        }

        [TestMethod]
        [DataRow("@concat(dataset().fileName, string(dataset().fileIndex), '.json')", "@concat('otter', string(0), '.json')")]
        public void ApplyParameter_Test(
            string originalExpression,
            string expectedUpdatedExpression)
        {
            AlertCollector alerts = new AlertCollector();

            UpgradeExpression ex = new UpgradeExpression("<path>", originalExpression);

            Dictionary<string, UpgradeParameter> parameters = new Dictionary<string, UpgradeParameter>();
            parameters.Add("dataset().fileName", UpgradeParameter.FromJToken(JObject.Parse("{'type':'string','defaultValue':'otter'}")));
            parameters.Add("dataset().fileIndex", UpgradeParameter.FromJToken(JObject.Parse("{'type':'integer','defaultValue':0}")));

            ex.ApplyParameters(parameters);

            string actualUpdatedExpression = string.Join(string.Empty, ex.Tokens());

            Assert.AreEqual(expectedUpdatedExpression, actualUpdatedExpression);
        }
    }
}
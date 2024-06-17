// <copyright file="ExpressionTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
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
        [DataRow("@concat(dataset().param1.prop1,dataset().param2, dataset().param3)", "@concat(pipeline().param1.prop1, pipeline().param2, 'filter')")]
        public void ReplaceExpression_Test(
            string originalExpression,
            string expectedReplacement)
        {
            AlertCollector alerts = new AlertCollector();
            UpgradeExpression ex = new UpgradeExpression("<path>", originalExpression);

            Dictionary<string, string> replace = new Dictionary<string, string>()
            {
                { "dataset().param1", "pipeline().param1" },
                { "dataset().param2", "pipeline().param2" },
                { "dataset().param3", "'filter'" }
            };

            string actualReplacement = ex.Replace(replace);

            Assert.AreEqual(expectedReplacement, actualReplacement);
        }
    }
}
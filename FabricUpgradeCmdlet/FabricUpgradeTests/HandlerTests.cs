// <copyright file="HandlerTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeTests.Utilities;
using FabricUpgradeTests.TestConfigModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeTests
{
    [TestClass]
    public class HandlerTests
    {
        [TestMethod]
        [DataRow("ImportNoSuchSupportFile")]
        [DataRow("ImportNotAZipFile")]
        [DataRow("ImportEmptyPipeline")]
        [DataRow("ImportEmptyPipeline_AfterImportResolutions")]
        [DataRow("ImportPipelineWithExecutePipeline")]
        [DataRow("ImportPipelineWithIf")]
        [DataRow("ImportPipelineWithCopy_JsonToJson")]
        public void ImportAdfSupportFile_Test(
            string testConfigFilename)
        {
            ImportTestConfig testConfig = ImportTestConfig.LoadFromFile(testConfigFilename);

            FabricUpgradeProgress actualResponse = new FabricUpgradeHandler().ImportAdfSupportFile(
                testConfig.Progress?.ToString(),
                "./TestFiles/AdfSupportFiles/" + testConfig.AdfSupportFile);

            JObject actualResponseObject = actualResponse.ToJObject();

            JObject expectedResponseObject = testConfig.ExpectedResponse;

            JObject mismatches = this.DeepCompare(expectedResponseObject, actualResponseObject);

            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponseObject}\n\nACTUAL:\n{actualResponse}");
        }

        [TestMethod]
        [DataRow("ConvertNotAZipFile")]
        [DataRow("ConvertPipelineWithUnsupportedActivity")]

        [DataRow("ConvertEmptyPipeline")]
        [DataRow("ConvertEmptyPipeline_AfterImportResolutions")]

        [DataRow("ConvertPipelineWithWait")]
        [DataRow("ConvertPipelineWithWaitWithExpression")]
        [DataRow("ConvertPipelineWithWait_NullWaitTime")]

        [DataRow("ConvertPipelineWithExecutePipeline")]

        [DataRow("ConvertPipelineWithIf")]

        [DataRow("ConvertPipelineWithWeb")]

        [DataRow("ConvertPipelineWithCopy_JsonToJson")]
        [DataRow("ConvertPipelineWithCopy_StagingAndLogging")]
        [DataRow("ConvertPipelineWithCopy_SqlToSql")]
        public void ConvertToFabricPipeline_Test(
            string testConfigFilename,
            string workspaceId = null) // we can set ws in param or in progress.
        {
            ImportTestConfig testConfig = ImportTestConfig.LoadFromFile(testConfigFilename);

            FabricUpgradeProgress importResponse = new FabricUpgradeHandler().ImportAdfSupportFile(
                testConfig.Progress?.ToString(),
                "./TestFiles/AdfSupportFiles/" + testConfig.AdfSupportFile);

            FabricUpgradeProgress actualConvertResponse = new FabricUpgradeHandler().ConvertToFabricPipeline(importResponse.ToString(), null);

            JObject actualResponseObject = actualConvertResponse.ToJObject();

            JObject expectedResponseObject = testConfig.ExpectedResponse;

            JObject mismatches = this.DeepCompare(expectedResponseObject, actualResponseObject);

            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponseObject}\n\nACTUAL:\n{actualConvertResponse}");
        }

        [TestMethod]
        [DataRow("{\"state\":\"Failed\", \"alerts\": []}", "{\"state\":\"Failed\", \"alerts\": [], \"resolutions\": [], \"result\": {} }")]
        public void ConvertToFabricPipeline_WithPreviousFailure_Test(
            string progress,
            string expectedResponse)
        {
            JObject expectedResponseObject = JObject.Parse(expectedResponse);
            FabricUpgradeProgress actualResponse = new FabricUpgradeHandler().ConvertToFabricPipeline(progress, null);

            Assert.AreEqual(FabricUpgradeProgress.FabricUpgradeState.Failed, actualResponse.State);
            Assert.AreEqual(0, actualResponse.Alerts.Count);

            var mismatches = this.DeepCompare(expectedResponseObject, actualResponse.ToJObject());
            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponseObject}\n\nACTUAL:\n{actualResponse}");
        }

        [TestMethod]
        [DataRow("{\"state\":\"Failed\", \"alerts\": []}", "passthrough")]
        [DataRow("{\"state\":\"Failed\", \"alerts\": [{\"severity\": \"Permanent\"}]}", "passthrough")]
        [DataRow("abc", "invalid")]
        [DataRow("{\"state\": \"Failed\"", "invalid")]
        public void ConvertToFabricPipeline_ErrorForwarding_Test(
            string progress,
            string expectedResponseType)
        {
            FabricUpgradeProgress expectedResponse = expectedResponseType switch
            {
                "passthrough" => FabricUpgradeProgress.FromString(progress),
                "invalid" => new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = new List<FabricUpgradeAlert> {
                        new FabricUpgradeAlert() {
                            Severity = FabricUpgradeAlert.FailureSeverity.Permanent,
                            Details = "Input is not a valid JSON string."
                        }
                    }
                },
                _ => null,
            };

            FabricUpgradeProgress actualResponse = new FabricUpgradeHandler().ConvertToFabricPipeline(progress, null);

            var mismatches = this.DeepCompare(expectedResponse.ToJObject(), actualResponse.ToJObject());
            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponse}\n\nACTUAL:\n{actualResponse}");
        }

        [TestMethod]
        [DataRow("ImportResolutions_NoSuchFile")]
        [DataRow("ImportResolutions_OneFileThenNoSuchFile")]
        [DataRow("ImportResolutions_NoSuchFileThenOneFile")]
        [DataRow("ImportResolutions_OneFile")]
        [DataRow("ImportResolutions_TwoFiles")]
        public void ImportFabricResolutions_Test(
            string testFilename)
        {
            ResolutionTestConfig testConfig = ResolutionTestConfig.LoadFromFile(testFilename);

            FabricUpgradeProgress runningProgress = testConfig.Progress;

            foreach (string filename in testConfig.ResolutionFiles)
            {
                string fullFilename = "./TestFiles/ResolutionFiles/" + filename;

                runningProgress = new FabricUpgradeHandler().ImportFabricResolutions(runningProgress?.ToString(), fullFilename);
            }

            var expectedResponse = testConfig.ExpectedProgress;
            var actualResponse = runningProgress;

            var mismatches = this.DeepCompare(expectedResponse.ToJObject(), actualResponse?.ToJObject());
            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponse}\n\nACTUAL:\n{actualResponse}");

        }

        [TestMethod]
        [DataRow("ExportEmptyPipeline")]
        [DataRow("ExportPipelineWithWait")]
        public async Task ExportFabricPipeline_TestAsync(
            string testConfigFilename)
        {
            Guid workspaceId = Guid.NewGuid();

            List<Guid> expectedGuids = new List<Guid>
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
            };

            ExportTestConfig testConfig = ExportTestConfig.LoadFromFile(testConfigFilename);
            testConfig.UpdateItemGuids(workspaceId, expectedGuids);

            TestPublicApiEndpoints endpoints = new TestPublicApiEndpoints("https://dailyapi.fabric.microsoft.com/v1/");
            endpoints.PrepareGuids(expectedGuids);
            TestHttpClientFactory.RegisterTestHttpClientFactory(endpoints);

            FabricUpgradeProgress actualResponse = await new FabricUpgradeHandler().ExportFabricPipelineAsync(
                testConfig.Progress.ToString(),
                "daily",
                workspaceId.ToString(),
                "123").ConfigureAwait(false);

            Assert.AreEqual(testConfig.ExpectedResponse.State, actualResponse.State);
            Assert.AreEqual(testConfig.ExpectedResponse.Alerts.Count, actualResponse.Alerts.Count);

            JObject expectedResult = testConfig.ExpectedResponse.Result;
            JObject actualResult = actualResponse.Result;

            var resultMismatches = this.DeepCompare(expectedResult, actualResult);
            Assert.IsNull(
                    resultMismatches,
                    $"MISMATCHES:\n{resultMismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResult}\n\nACTUAL:\n{actualResult}");



            int expectedNumItems = testConfig.ExpectedItems.Count();

            (int numItems, int numItemDefinitions) = endpoints.CountItems();
            Assert.AreEqual(expectedNumItems, numItems);
            Assert.AreEqual(expectedNumItems, numItemDefinitions);

            int nItem = 0;
            foreach (JToken expectedItemToken in testConfig.ExpectedItems)
            {
                JObject expectedItem = JObject.Parse(expectedItemToken.ToString());

                JObject actualItem = endpoints.ReadItemDirectly(workspaceId, expectedGuids[nItem]);
                string eis = expectedItem.ToString();
                string ais = actualItem.ToString();

                JObject mismatches = this.DeepCompare(expectedItem, actualItem);

                Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{eis}\n\nACTUAL:\n{ais}");

                nItem++;
            }
        }

        private class ImportTestConfig
        {
            [JsonProperty(PropertyName = "progress")]
            public FabricUpgradeProgress Progress { get; set; }

            [JsonProperty(PropertyName = "adfSupportFile")]
            public string AdfSupportFile { get; set; }

            [JsonProperty(PropertyName = "expectedResponse")]
            public JObject ExpectedResponse { get; set; }

            public static ImportTestConfig LoadFromFile(string testFilename)
            {
                string test = File.ReadAllText("./TestFiles/" + testFilename + ".json");
                ImportTestConfig config = JsonConvert.DeserializeObject<ImportTestConfig>(test);
                return config;
            }
        }

        private class ConvertTestConfig
        {
            [JsonProperty(PropertyName = "adfSupportFile")]
            public string AdfSupportFile { get; set; }

            [JsonProperty(PropertyName = "expectedResponse")]
            public JObject expectedResponse { get; set; }

            public static ImportTestConfig LoadFromFile(string testFilename)
            {
                string test = File.ReadAllText("./TestFiles/" + testFilename + ".json");
                ImportTestConfig config = JsonConvert.DeserializeObject<ImportTestConfig>(test);
                return config;
            }
        }

        private class ResolutionTestConfig
        {
            [JsonProperty(PropertyName = "progress")]
            public FabricUpgradeProgress Progress { get; set; }

            [JsonProperty(PropertyName = "resolutionFiles")]
            public List<string> ResolutionFiles { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "expectedProgress")]
            public FabricUpgradeProgress ExpectedProgress { get; set; }

            public static ResolutionTestConfig LoadFromFile(string testFilename)
            {
                string test = File.ReadAllText("./TestFiles/" + testFilename + ".json");
                ResolutionTestConfig config = JsonConvert.DeserializeObject<ResolutionTestConfig>(test);
                return config;
            }

        }

        private class ExportTestConfig
        {
            [JsonProperty(PropertyName = "progress")]
            public JObject Progress { get; set; }

            [JsonProperty(PropertyName = "prestock")]
            public List<Prestock> Prestocks { get; set; } = new List<Prestock>();

            [JsonProperty(PropertyName = "guidSubstitutions")]
            public List<GuidSubstitution> GuidSubstitutions { get; set; } = new List<GuidSubstitution>();

            [JsonProperty(PropertyName = "expectedResponse")]
            public FabricUpgradeProgress ExpectedResponse { get; set; }

            [JsonProperty(PropertyName = "expectedItems")]
            public JArray ExpectedItems { get; set; } = new JArray();

            public static ExportTestConfig LoadFromFile(string testFilename)
            {
                string test = File.ReadAllText("./TestFiles/" + testFilename + ".json");
                ExportTestConfig config = JsonConvert.DeserializeObject<ExportTestConfig>(test);
                return config;
            }

            public ExportTestConfig UpdateItemGuids(Guid workspaceId, List<Guid> itemIds)
            {
                foreach (var r in this.ExpectedResponse.Result)
                {
                    r.Value["workspaceId"] = workspaceId.ToString();
                    int idGuidIndex = r.Value["id"].ToObject<int>();
                    r.Value["id"] = itemIds[idGuidIndex].ToString();
                }

                int numItem = 0;
                foreach (JToken expectedItemToken in this.ExpectedItems)
                {
                    Guid itemId = itemIds[numItem];
                    JObject expectedItem = (JObject)expectedItemToken;
                    expectedItem["item"]["workspaceId"] = workspaceId.ToString();
                    expectedItem["item"]["id"] = itemId.ToString();
                    numItem++;

                    foreach (Prestock prestock in this.Prestocks)
                    {
                        // Find a prestock of the same type and name.
                        if (prestock.Type != expectedItem.SelectToken("$.item.type").ToString())
                        {
                            continue;
                        }

                        if (prestock.DisplayName != expectedItem.SelectToken("$.item.displayName").ToString())
                        {
                            continue;
                        }

                        prestock.WorkspaceId = workspaceId.ToString();
                        prestock.Id = itemId.ToString();
                    }
                }

                // In an ExecutePipeline Activity, we need to tweak one of the expected fields to point at another pipeline.
                // There may be more uses for this.
                foreach (GuidSubstitution gSub in this.GuidSubstitutions ?? new List<GuidSubstitution>())
                {
                    JToken items = this.ExpectedItems;
                    JToken field = items.SelectToken(gSub.Path);
                    if (gSub.GuidIndex < 0)
                    {
                        field.Replace(workspaceId);
                    }
                    else
                    {
                        field.Replace(itemIds[gSub.GuidIndex]);
                    }
                }

                return this;
            }

            public class Prestock
            {
                [JsonProperty(PropertyName = "workspaceId")]
                public string WorkspaceId { get; set; }

                [JsonProperty(PropertyName = "id")]
                public string Id { get; set; }

                [JsonProperty(PropertyName = "type")]
                public string Type { get; set; }

                [JsonProperty(PropertyName = "displayName")]
                public string DisplayName { get; set; }
            }
        }

        /// <summary>
        /// Execute a "deep" comparison of two JTokens, and report the mismatches.
        /// </summary>
        /// <param name="expected">The expected JToken.</param>
        /// <param name="actual">The actual JToken.</param>
        /// <param name="key">The key being compared, for recursion.</param>
        /// <returns>A JObject describing the mismatches.</returns>
        private JObject DeepCompare(JToken expected, JToken actual, string key = "")
        {
            JObject mismatches = new JObject();

            if (expected.Type != actual.Type)
            {
                if (!(expected.Type == JTokenType.Null && actual.Value<string>() == null))
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
                        JObject childMismatches = this.DeepCompare(eObj[expectedKey], aObj[expectedKey], expectedKey);
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
                    JObject elementMismatch = this.DeepCompare(eArr[index], aArr[index]);
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
using FabricUpgradeCmdlet;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeTests.Utilities;
using System.Security.Policy;

namespace FabricUpgradeTests
{
    [TestClass]
    public class HandlerTests
    {
        [TestMethod]
        [DataRow("ImportNoSuchSupportFile")]
        [DataRow("ImportNotAZipFile")]
        [DataRow("ImportEmptyPipeline")]
        [DataRow("ImportPipelineWithExecutePipeline")]
        [DataRow("ImportPipelineWithCopy")]
        public void ImportAdfSupportFile_Test(
            string testConfigFilename)
        {
            ImportTestConfig testConfig = ImportTestConfig.LoadFromFile(testConfigFilename);
            FabricUpgradeProgress actualResponse = new FabricUpgradeHandler().ImportAdfSupportFile("./TestFiles/AdfSupportFiles/" + testConfig.AdfSupportFile);
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
        [DataRow("ConvertPipelineWithWait")]
        public void ConvertToFabricPipeline_Test(
            string testConfigFilename)
        {
            ImportTestConfig testConfig = ImportTestConfig.LoadFromFile(testConfigFilename);
            FabricUpgradeProgress importResponse = new FabricUpgradeHandler().ImportAdfSupportFile("./TestFiles/AdfSupportFiles/" + testConfig.AdfSupportFile);

            FabricUpgradeProgress actualConvertResponse = new FabricUpgradeHandler().ConvertToFabricPipeline(importResponse.ToString(), null);

            JObject actualResponseObject = actualConvertResponse.ToJObject();

            JObject expectedResponseObject = testConfig.ExpectedResponse;

            JObject mismatches = this.DeepCompare(expectedResponseObject, actualResponseObject);

            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponseObject}\n\nACTUAL:\n{actualConvertResponse}");
        }

        [TestMethod]
        [DataRow("{\"state\":\"Failed\", \"alerts\": []}", "{\"state\":\"Failed\", \"alerts\": [], \"result\": {} }")]
        public void ConvertToFabricPipeline_WithPreviousFailure_Test(
            string adfSupportData,
            string expectedResponse)
        {
            JObject expectedResponseObject = JObject.Parse(expectedResponse);
            FabricUpgradeProgress actualResponse = new FabricUpgradeHandler().ConvertToFabricPipeline(adfSupportData, null);

            Assert.AreEqual(FabricUpgradeProgress.FabricUpgradeState.Failed, actualResponse.State);
            Assert.AreEqual(0, actualResponse.Alerts.Count);

            var mismatches = this.DeepCompare(expectedResponseObject, actualResponse.ToJObject());
            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponseObject}\n\nACTUAL:\n{actualResponse}");
        }

        [TestMethod]
        //[DataRow("{\"state\":\"Succeeded\", \"alerts\": []}", "passthrough")]
        [DataRow("{\"state\":\"Failed\", \"alerts\": []}", "passthrough")]
        [DataRow("{\"state\":\"Failed\", \"alerts\": [{\"severity\": \"Permanent\"}]}", "passthrough")]
        [DataRow("abc", "invalid")]
        [DataRow("{\"state\": \"Failed\"", "invalid")]
        public void ConvertToFabricPipeline_ErrorForwarding_Test(
            string inputString,
            string expectedResponseType)
        {
            FabricUpgradeProgress expectedResponse = expectedResponseType switch
            {
                "passthrough" => FabricUpgradeProgress.FromString(inputString),
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

            FabricUpgradeProgress actualResponse = new FabricUpgradeHandler().ConvertToFabricPipeline(inputString, null);

            var mismatches = this.DeepCompare(expectedResponse.ToJObject(), actualResponse.ToJObject());
            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponse}\n\nACTUAL:\n{actualResponse}");
        }

        [TestMethod]
        [DataRow("ExportEmptyPipeline")]
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
            [JsonProperty(PropertyName = "adfSupportFile")]
            public string AdfSupportFile { get; set; }

            [JsonProperty(PropertyName = "expectedResponse")]
            public JObject ExpectedResponse { get; set; }

            public static ImportTestConfig LoadFromFile(string testFileName)
            {
                string test = File.ReadAllText("./TestFiles/" + testFileName + ".json");
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

            public static ImportTestConfig LoadFromFile(string testFileName)
            {
                string test = File.ReadAllText("./TestFiles/" + testFileName + ".json");
                ImportTestConfig config = JsonConvert.DeserializeObject<ImportTestConfig>(test);
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

            public static ExportTestConfig LoadFromFile(string testFileName)
            {
                string test = File.ReadAllText("./TestFiles/" + testFileName + ".json");
                ExportTestConfig config = JsonConvert.DeserializeObject<ExportTestConfig>(test);
                return config;
            }

            public ExportTestConfig UpdateItemGuids(Guid workspaceId, List<Guid> itemIds)
            {
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
                    JToken item = this.ExpectedItems[gSub.ItemIndex];
                    JToken field = item.SelectToken(gSub.Path);
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

            public class GuidSubstitution
            {
                [JsonProperty(PropertyName = "itemIndex")]
                public int ItemIndex { get; set; }

                [JsonProperty(PropertyName = "path")]
                public string Path { get; set; }

                [JsonProperty(PropertyName = "guidIndex")]
                public int GuidIndex { get; set; }
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
                mismatches["typeMismatch"] = $"Expected type {expected.Type}, Actual type {actual.Type}";
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
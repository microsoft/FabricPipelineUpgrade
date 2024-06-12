using FabricUpgradeCmdlet;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeTests.Utilities;
using FabricUpgradeTests.TestConfigModels;

namespace FabricUpgradeTests
{
    [TestClass]
    public class EndToEndTests
    {
        [TestMethod]
        [DataRow("E2eNoSuchSupportFile")]
        [DataRow("E2eEmptyPipeline")]
        [DataRow("E2ePipelineWithUnsupportedActivity")]

        [DataRow("E2ePipelineWithWait")]
        [DataRow("E2ePipelineWithIf")]
        [DataRow("E2ePipelineWithWaitAndIf")]

        [DataRow("E2ePipelineWithExecutePipeline")]
        [DataRow("E2ePipelineWithExecutePipeline_MissingResolution")]


        [DataRow("E2ePipelineWithWeb")]
        [DataRow("E2ePipelineWithWeb_Post")]
        [DataRow("E2ePipelineWithWeb_HttpUrl")]
        [DataRow("E2ePipelineWithWeb_NoHeaders")]
        [DataRow("E2ePipelineWithWeb_MissingResolution")]
        [DataRow("E2ePipelineWithWeb_DynamicUrl")]
        [DataRow("E2ePipelineWithWeb_NullUrl")]
        [DataRow("E2ePipelineWithWeb_BadUrl")]
        [DataRow("E2ePipelineWithWeb_WithLinkedServicesAndDatasets")]
        public async Task ExportFabricPipeline_TestAsync(
            string testConfigFilename)
        {
            Guid workspaceId = Guid.NewGuid();

            List<Guid> expectedGuids = new List<Guid>
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
            };

            EndToEndTestConfig testConfig = EndToEndTestConfig.LoadFromFile(testConfigFilename);
            testConfig.UpdateItemGuids(workspaceId, expectedGuids);

            TestPublicApiEndpoints endpoints = new TestPublicApiEndpoints("https://dailyapi.fabric.microsoft.com/v1/");
            endpoints.PrepareGuids(expectedGuids);
            TestHttpClientFactory.RegisterTestHttpClientFactory(endpoints);

            FabricUpgradeProgress runningProgress = testConfig.Progress;

            runningProgress = new FabricUpgradeHandler().ImportAdfSupportFile(
                runningProgress?.ToString(),
                "./TestFiles/AdfSupportFiles/" + testConfig.AdfSupportFile);

            runningProgress = new FabricUpgradeHandler().ConvertToFabricPipeline(
                runningProgress?.ToString(),
                null);

            foreach (FabricUpgradeResolution resolution in testConfig.Resolutions)
            {
                runningProgress = new FabricUpgradeHandler().AddFabricResolution(
                    runningProgress?.ToString(),
                    resolution?.ToString());
            }

            runningProgress = await new FabricUpgradeHandler().ExportFabricPipelineAsync(
                runningProgress?.ToString(),
                "daily",
                workspaceId.ToString(),
                "123").ConfigureAwait(false);

            Assert.AreEqual(testConfig.ExpectedResponse.State, runningProgress.State, runningProgress.ToString());
            Assert.AreEqual(testConfig.ExpectedResponse.Alerts.Count, runningProgress.Alerts.Count, runningProgress.ToString());
            for (int nAlert = 0; nAlert <  testConfig.ExpectedResponse.Alerts.Count; nAlert++)
            {
                var expectedAlert = testConfig.ExpectedResponse.Alerts[nAlert].ToJToken();
                var actualAlert = runningProgress.Alerts[nAlert].ToJToken();
                var alertMismatches = this.DeepCompare(expectedAlert, actualAlert);
                Assert.IsNull(
                    alertMismatches,
                    $"Alert[{nAlert}] MISMATCHES:\n{alertMismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedAlert}\n\nACTUAL:\n{actualAlert}");
            }

            Assert.AreEqual(testConfig.ExpectedResponse.Resolutions.Count, runningProgress.Resolutions.Count, runningProgress.ToString());
            for (int nResolution = 0; nResolution < testConfig.ExpectedResponse.Resolutions.Count; nResolution++)
            {
                var expectedResolution = testConfig.ExpectedResponse.Resolutions[nResolution].ToJToken();
                var actualResolution = runningProgress.Resolutions[nResolution].ToJToken();
                var alertMismatches = this.DeepCompare(expectedResolution, actualResolution);
                Assert.IsNull(
                    alertMismatches,
                    $"Resolution[{nResolution}] MISMATCHES:\n{alertMismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResolution}\n\nACTUAL:\n{actualResolution}");
            }


            JObject expectedResult = testConfig.ExpectedResponse.Result;
            JObject actualResult = runningProgress.Result;

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
                    $"Item[{nItem}] MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{eis}\n\nACTUAL:\n{ais}");

                nItem++;
            }
        }

        private class EndToEndTestConfig
        {
            [JsonProperty(PropertyName = "progress")]
            public FabricUpgradeProgress Progress { get; set; }

            [JsonProperty(PropertyName = "adfSupportFile")]
            public string AdfSupportFile { get; set; }

            [JsonProperty(PropertyName = "resolutions")]
            public List<FabricUpgradeResolution> Resolutions { get; set; } = new List<FabricUpgradeResolution>();

            [JsonProperty(PropertyName = "prestock")]
            public List<Prestock> Prestocks { get; set; } = new List<Prestock>();

            [JsonProperty(PropertyName = "guidSubstitutions")]
            public List<GuidSubstitution> GuidSubstitutions { get; set; } = new List<GuidSubstitution>();

            [JsonProperty(PropertyName = "expectedResponse")]
            public FabricUpgradeProgress ExpectedResponse { get; set; }

            [JsonProperty(PropertyName = "expectedItems")]
            public JArray ExpectedItems { get; set; } = new JArray();

            public static EndToEndTestConfig LoadFromFile(string testFilename)
            {
                string testConfig = File.ReadAllText("./TestFiles/" + testFilename + ".json");
                return JsonConvert.DeserializeObject<EndToEndTestConfig>(testConfig);
            }

            public EndToEndTestConfig UpdateItemGuids(Guid workspaceId, List<Guid> itemIds)
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
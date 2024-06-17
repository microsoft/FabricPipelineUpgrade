// <copyright file="EndToEndTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Utilities;
using FabricUpgradeTests.Utilities;
using FabricUpgradeTests.TestConfigModels;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace FabricUpgradeTests
{
    [TestClass]
    public class EndToEndTests
    {
        [TestMethod]
        [DataRow("E2eNoSuchSupportFile")]

        [DataRow("E2eEmptyPipeline")]
        [DataRow("E2eEmptyPipeline_Update")]
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

        [DataRow("E2ePipelineWithCopy_JsonToJson")]
        [DataRow("E2ePipelineWithCopy_JsonToJson_MissingResolution")]
        [DataRow("E2ePipelineWithCopy_StagingAndLogging")]
        [DataRow("E2ePipelineWithCopy_JsonToJson_DefaultDatasetParams")]
        //[DataRow("E2ePipelineWithCopy_JsonToJson_Params")]
        [DataRow("E2ePipelineWithCopy_JsonToJson_Params_GlobalConfig")]

        [DataRow("E2ePipelineWithCopy_SqlToSql")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_MissingResolution")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_NoConnectionString")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_ExpressionConnectionString")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_ConnectionStringLacksInitialCatalog")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_ConnectionStringExpressionInitialCatalog")]

        [DataRow("E2ePipelineWithExecutePipeline_PrestockFirst")]
        [DataRow("E2ePipelineWithExecutePipeline_PrestockSecond")]
        [DataRow("E2ePipelineWithExecutePipeline_PrestockBoth")]

        [DataRow("E2eEmptyPipeline_DisplayNameAlreadyInUse")]
        [DataRow("E2ePipelineWithExecutePipeline_ReserveFirst")]
        [DataRow("E2ePipelineWithExecutePipeline_ReserveSecond")]
        public async Task ExportFabricPipeline_TestAsync(
            string testConfigFilename)
        {
            Guid workspaceId = Guid.NewGuid();
            string pbiAadToken = Guid.NewGuid().ToString();

            List<Guid> expectedGuids = new List<Guid>
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
            };

            EndToEndTestConfig testConfig = EndToEndTestConfig.LoadFromFile(testConfigFilename);
            testConfig.UpdateItemGuids(workspaceId, expectedGuids);

            TestPublicApiEndpoints endpoints = new TestPublicApiEndpoints("https://dailyapi.fabric.microsoft.com/v1/");
            // Tell the endpoints which guids to use when creating artifacts, so that we can validate them in tests.
            endpoints.PrepareGuids(expectedGuids);
            endpoints.RequireUserToken(pbiAadToken);

            // Pre-create some pipelines to force an update.
            endpoints.Prestock((JArray)UpgradeSerialization.ToJToken(testConfig.Prestocks));

            // Pretend that the user has recently deleted a pipeline with these display names.
            // Currently, it takes a little while before display names become re-useable.
            testConfig.ReservedDisplayNames.ForEach(rdn => endpoints.ReserveDisplayName(rdn));

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
                pbiAadToken,
                CancellationToken.None).ConfigureAwait(false);

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
                string eis = UpgradeSerialization.Serialize(expectedItem);
                string ais = UpgradeSerialization.Serialize(actualItem);

                JObject mismatches = this.DeepCompare(expectedItem, actualItem);

                Assert.IsNull(
                    mismatches,
                    $"Item[{nItem}] MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{eis}\n\nACTUAL:\n{ais}");

                nItem++;
            }

            List<string> actualEndpointEvents = endpoints.FetchEvents();

            Assert.AreEqual(testConfig.ExpectedEndpointEvents.Count, actualEndpointEvents.Count, "\r\n" + string.Join("\r\n", actualEndpointEvents));
            for (int nEvent = 0; nEvent < testConfig.ExpectedEndpointEvents.Count; nEvent++)
            {
                string expectedEvent = testConfig.ExpectedEndpointEvents[nEvent];
                for (int nGuid = expectedGuids.Count - 1; nGuid >= 0; nGuid--)
                {
                    expectedEvent = expectedEvent.Replace($"${nGuid}", expectedGuids[nGuid].ToString());
                }

                Assert.AreEqual(expectedEvent, actualEndpointEvents[nEvent]);
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

            [JsonProperty(PropertyName = "prestocks")]
            public List<Prestock> Prestocks { get; set; } = new List<Prestock>();

            [JsonProperty(PropertyName = "reservedDisplayNames")]
            public List<string> ReservedDisplayNames { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "guidSubstitutions")]
            public List<GuidSubstitution> GuidSubstitutions { get; set; } = new List<GuidSubstitution>();

            [JsonProperty(PropertyName = "expectedResponse")]
            public FabricUpgradeProgress ExpectedResponse { get; set; }

            [JsonProperty(PropertyName = "expectedItems")]
            public JArray ExpectedItems { get; set; } = new JArray();

            [JsonProperty(PropertyName = "expectedEndpointEvents")]
            public List<string> ExpectedEndpointEvents { get; set; } = new List<string>();

            public static EndToEndTestConfig LoadFromFile(string testFilename)
            {
                string testConfig = File.ReadAllText("./TestFiles/" + testFilename + ".json");
                return JsonConvert.DeserializeObject<EndToEndTestConfig>(testConfig);
            }

            public EndToEndTestConfig UpdateItemGuids(Guid workspaceId, List<Guid> itemIds)
            {
                foreach (var r in this.ExpectedResponse.Result)
                {
                    if (((JObject)r.Value)["workspaceId"]?.ToString() == "")
                    {
                        r.Value["workspaceId"] = workspaceId.ToString();
                    }

                    if (r.Value["id"].Type == JTokenType.Integer)
                    {
                        int idGuidIndex = r.Value["id"].ToObject<int>();
                        r.Value["id"] = itemIds[idGuidIndex].ToString();
                    }
                }

                foreach (JToken expectedItemToken in this.ExpectedItems)
                {
                    JObject expectedItem = (JObject)expectedItemToken;
                    expectedItem["item"]["workspaceId"] = workspaceId.ToString();
                    if (expectedItem["item"]["id"].Type == JTokenType.Integer)
                    {
                        int guidIndex = expectedItem["item"]["id"].ToObject<int>();
                        expectedItem["item"]["id"] = itemIds[guidIndex].ToString();
                    }
                }

                foreach (Prestock prestock in this.Prestocks)
                {
                    prestock.WorkspaceId = workspaceId.ToString();
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
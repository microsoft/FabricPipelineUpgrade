// <copyright file="EndToEndTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using FabricUpgradePowerShellModuleTests.Utilities;
using FabricUpgradePowerShellModuleTests.TestConfigModels;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace FabricUpgradePowerShellModuleTests
{
    [TestClass]
    public class EndToEndTests
    {
        [TestMethod]
        [DataRow("E2eNoSuchSupportFile")]

        [DataRow("E2eEmptyPipeline")]
        [DataRow("E2eEmptyPipeline_Update")]
        [DataRow("E2ePipelineWithUnsupportedActivity")]
        [DataRow("E2ePipelineWithTriggerAndUnsupportedActivity")]

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

        [DataRow("E2ePipelineWithCopy_Binary_DatasetParams")]

        [DataRow("E2ePipelineWithCopy_MisplacedExpressionsInBlobLinkedService")]
        [DataRow("E2ePipelineWithCopy_MisplacedExpressionsInAzureSqlLinkedService")]
        [DataRow("E2ePipelineWithCopy_MisplacedExpressionsInAzureSqlLinkedService_RecommendedForm")]
        [DataRow("E2ePipelineWithCopy_SAMI_BlobLinkedService_support_live")]

        [DataRow("E2ePipelineWithCopy_JsonToJson")]
        [DataRow("E2ePipelineWithCopy_JsonToJson_MissingResolution")]
        [DataRow("E2ePipelineWithCopy_StagingAndLogging")]
        [DataRow("E2ePipelineWithCopy_JsonToJson_DefaultDatasetParams")]
        [DataRow("E2ePipelineWithCopy_JsonToJson_OverrideDefaultDatasetParams")]
        [DataRow("E2ePipelineWithCopy_JsonToJson_OverrideDefaultDatasetParamsToPipelineParams")]
        [DataRow("E2ePipelineWithCopy_JsonToJson_Params_GlobalConfig")]

        [DataRow("E2ePipelineWithCopy_JsonToJson_SAMI_ADLS2_support_live")]
        [DataRow("E2ePipelineWithCopy_JsonToJson_SASURL_ADLS2_support_live")]

        [DataRow("E2ePipelineWithCopy_IntegerDatasetTypeProperty")]

        [DataRow("E2ePipelineWithCopy_SqlToSql")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_RecommendedForm")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_MissingResolution")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_NoConnectionStringAndNoServer")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_ExpressionConnectionString")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_ConnectionStringLacksInitialCatalog")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_ExpressionInitialCatalog")]

        [DataRow("E2ePipelineWithCopy_SqlToSql_DefaultLinkedServiceParams")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_DatasetConstToLinkedServiceParams")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_ActivityConstToDatasetToLinkedServiceParams")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_PipelineParamToDatasetToLinkedServiceParams")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_PipelineParamToDatasetToLinkedServiceParams2")]
        [DataRow("E2ePipelineWithCopy_SqlToSql_PipelineParamToDatasetToLinkedServiceParams3")]

        [DataRow("E2ePipelineWithExecutePipeline_PrestockFirst")]
        [DataRow("E2ePipelineWithExecutePipeline_PrestockSecond")]
        [DataRow("E2ePipelineWithExecutePipeline_PrestockBoth")]

        [DataRow("E2eEmptyPipeline_DisplayNameAlreadyInUse")]
        [DataRow("E2ePipelineWithExecutePipeline_ReserveFirst")]
        [DataRow("E2ePipelineWithExecutePipeline_ReserveSecond")]
        public async Task EndToEndUpgradePipeline_TestAsync(
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

            runningProgress = new FabricUpgradeHandler().ConvertToFabricResources(runningProgress?.ToString());

            FabricUpgradeProgress whatIfProgress = new FabricUpgradeHandler().SelectWhatIf(runningProgress?.ToString());

            foreach (FabricUpgradeResolution resolution in testConfig.Resolutions)
            {
                runningProgress = new FabricUpgradeHandler().AddFabricResolution(
                    runningProgress?.ToString(),
                    resolution?.ToString());
            }

            runningProgress = await new FabricUpgradeHandler().ExportFabricResourcesAsync(
                runningProgress?.ToString(),
                "daily",
                workspaceId.ToString(),
                pbiAadToken,
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(testConfig.ExpectedResponse.State, runningProgress.State, runningProgress.ToString());
            Assert.AreEqual(testConfig.ExpectedResponse.Alerts.Count, runningProgress.Alerts.Count, runningProgress.ToString());
            Assert.AreEqual(testConfig.ExpectedWhatIfResponse.State, whatIfProgress.State, whatIfProgress.ToString());
            Assert.AreEqual(testConfig.ExpectedWhatIfResponse.Alerts.Count, whatIfProgress.Alerts.Count, whatIfProgress.ToString());
            for (int nAlert = 0; nAlert < testConfig.ExpectedResponse.Alerts.Count; nAlert++)
            {
                var expectedAlert = testConfig.ExpectedResponse.Alerts[nAlert].ToJToken();
                var actualAlert = runningProgress.Alerts[nAlert].ToJToken();
                var alertMismatches = JsonUtils.DeepCompare(expectedAlert, actualAlert);
                Assert.IsNull(
                    alertMismatches,
                    $"Alert[{nAlert}] MISMATCHES:\n{alertMismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedAlert}\n\nACTUAL:\n{actualAlert}");
            }

            Assert.AreEqual(testConfig.ExpectedResponse.Resolutions.Count, runningProgress.Resolutions.Count, runningProgress.ToString());
            for (int nResolution = 0; nResolution < testConfig.ExpectedResponse.Resolutions.Count; nResolution++)
            {
                var expectedResolution = testConfig.ExpectedResponse.Resolutions[nResolution].ToJToken();
                var actualResolution = runningProgress.Resolutions[nResolution].ToJToken();
                var alertMismatches = JsonUtils.DeepCompare(expectedResolution, actualResolution);
                Assert.IsNull(
                    alertMismatches,
                    $"Resolution[{nResolution}] MISMATCHES:\n{alertMismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResolution}\n\nACTUAL:\n{actualResolution}");
            }


            JObject expectedResult = testConfig.ExpectedResponse.Result;
            JObject actualResult = runningProgress.Result;

            var resultMismatches = JsonUtils.DeepCompare(expectedResult, actualResult);
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

                JObject mismatches = JsonUtils.DeepCompare(expectedItem, actualItem);

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

            // Before running the test, create these Pipelines in the TestPublicApiEndpoints.
            [JsonProperty(PropertyName = "prestocks")]
            public List<Prestock> Prestocks { get; set; } = new List<Prestock>();

            // Return an ItemDisplayNameAlreadyInUse error if the upgrade tries to create a Pipeline with this name.
            [JsonProperty(PropertyName = "reservedDisplayNames")]
            public List<string> ReservedDisplayNames { get; set; } = new List<string>();

            // Fix up an expectedItem's JSON by inserting a GUID at the correct spot.
            // This is used for ExecutePipeline, so that we can "expect" the correct GUID in that activity.
            [JsonProperty(PropertyName = "guidSubstitutions")]
            public List<GuidSubstitution> GuidSubstitutions { get; set; } = new List<GuidSubstitution>();

            [JsonProperty(PropertyName = "expectedResponse")]
            public FabricUpgradeProgress ExpectedResponse { get; set; }

            [JsonProperty(PropertyName = "expectedItems")]
            public JArray ExpectedItems { get; set; } = new JArray();

            [JsonProperty(PropertyName = "expectedEndpointEvents")]
            public List<string> ExpectedEndpointEvents { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "expectedWhatIfResponse")]
            public FabricUpgradeProgress ExpectedWhatIfResponse { get; set; } = new FabricUpgradeProgress() { State = FabricUpgradeProgress.FabricUpgradeState.Succeeded };

            public static EndToEndTestConfig LoadFromFile(string testFilename)
            {
                string testConfig = File.ReadAllText("./TestFiles/" + testFilename + ".json");
                return JsonConvert.DeserializeObject<EndToEndTestConfig>(testConfig);
            }

            public EndToEndTestConfig UpdateItemGuids(Guid workspaceId, List<Guid> itemIds)
            {
                JObject expectedResponseItems = (JObject)(this.ExpectedResponse.Result[FabricUpgradeProgress.ExportedFabricResourcesKey]) ?? new JObject();
                foreach (var r in expectedResponseItems)
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
    }
}
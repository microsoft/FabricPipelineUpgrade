// <copyright file="HandlerTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModuleTests.Utilities;
using FabricUpgradePowerShellModuleTests.TestConfigModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.ApplicationInsights.DataContracts;

namespace FabricUpgradePowerShellModuleTests
{
    [TestClass]
    public class HandlerTests
    {
        // Validate Import-AdfSupportFile.
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

            JObject mismatches = JsonUtils.DeepCompare(expectedResponseObject, actualResponseObject);

            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponseObject}\n\nACTUAL:\n{actualResponse}");
        }

        // Validate ConvertTo-FabricResources
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

            FabricUpgradeProgress actualConvertResponse = new FabricUpgradeHandler().ConvertToFabricResources(importResponse.ToString());

            JObject actualResponseObject = actualConvertResponse.ToJObject();

            JObject expectedResponseObject = testConfig.ExpectedResponse;

            JObject mismatches = JsonUtils.DeepCompare(expectedResponseObject, actualResponseObject);

            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponseObject}\n\nACTUAL:\n{actualConvertResponse}");
        }

        // If the progress sent to ConvertTo-FabricResources does not contain an "importedResources" property,
        // then ConvertTo-FabricResources fails.
        [TestMethod]
        [DataRow("x")]
        [DataRow(FabricUpgradeProgress.ExportableFabricResourcesKey)]
        [DataRow(FabricUpgradeProgress.ExportedFabricResourcesKey)]
        public void ConvertNotImportedResources_Test(
            string resultKey)
        {
            FabricUpgradeProgress progress = new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
            };
            progress.Result = JObject.Parse($"{{ '{resultKey}': {{}} }}");

            FabricUpgradeProgress actualResponse = new FabricUpgradeHandler().ConvertToFabricResources(progress.ToString());

            Assert.AreEqual(FabricUpgradeProgress.FabricUpgradeState.Failed, actualResponse.State);
            Assert.AreEqual(1, actualResponse.Alerts.Count);
            Assert.AreEqual(FabricUpgradeAlert.AlertSeverity.Permanent, actualResponse.Alerts[0].Severity);
            Assert.AreEqual("ConvertTo-FabricResources expects imported ADF resources.", actualResponse.Alerts[0].Details);
        }

        // If the progress passed to ConvertTo-FabricResources has a state of Failed,
        // then ConvertTo-FabricResources returns the same progress.
        // If the progress passed to ConvertTo-FabricResources is not a valid JSON string,
        // then ConvertTo-FabricResources fails with the appropriate error.
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
                            Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                            Details = "Input is not a valid JSON FabricUpgradeProgress."
                        }
                    },
                },
                _ => null,
            };

            FabricUpgradeProgress actualResponse = new FabricUpgradeHandler().ConvertToFabricResources(progress);

            var mismatches = JsonUtils.DeepCompare(expectedResponse.ToJObject(), actualResponse.ToJObject());
            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponse}\n\nACTUAL:\n{actualResponse}");
        }

        // Validate the Import-FabricResolutions method.
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

            var mismatches = JsonUtils.DeepCompare(expectedResponse.ToJObject(), actualResponse?.ToJObject());
            Assert.IsNull(
                    mismatches,
                    $"MISMATCHES:\n{mismatches?.ToString(Formatting.Indented)}\n\nEXPECTED:\n{expectedResponse}\n\nACTUAL:\n{actualResponse}");

        }

        // If the progress passed to Export-FabricResources does not contain an "exportableFabricResources" property,
        // then Export-FabricResources should fail.
        [TestMethod]
        [DataRow("yyy")]
        [DataRow(FabricUpgradeProgress.ImportedResourcesKey)]
        [DataRow(FabricUpgradeProgress.ExportedFabricResourcesKey)]
        public async Task ExportNotExportableResources_TestAsync(
            string resultKey)
        {
            FabricUpgradeProgress progress = new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
            };
            progress.Result = JObject.Parse($"{{ '{resultKey}': {{}} }}");

            FabricUpgradeProgress actualResponse = await new FabricUpgradeHandler().ExportFabricResourcesAsync(
                progress.ToString(),
                "daily",
                "wsId",
                "token",
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(FabricUpgradeProgress.FabricUpgradeState.Failed, actualResponse.State);
            Assert.AreEqual(1, actualResponse.Alerts.Count);
            Assert.AreEqual(FabricUpgradeAlert.AlertSeverity.Permanent, actualResponse.Alerts[0].Severity);
            Assert.AreEqual("Export-FabricResources expects exportable Fabric resources.", actualResponse.Alerts[0].Details);
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
    }
}
// <copyright file="HandlerTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
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
                "./TestFiles/AdfSupportFiles/" + testConfig.AdfSupportFile,
                true); 

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

        [DataRow("ConvertPipelineWithAzureFunction")]
        [DataRow("ConvertPipelineWithSproc")]
        [DataRow("ConvertPipelineWithLookup")]
        [DataRow("ConvertPipelineWithSwitch")]
        [DataRow("ConvertPipelineWithForeach")]
        [DataRow("ConvertSimpleParentWithDescriptionAndConcurrency")]
        public void ConvertToFabricPipeline_Test(
            string testConfigFilename,
            string workspaceId = null) // we can set ws in param or in progress.
        {
            ImportTestConfig testConfig = ImportTestConfig.LoadFromFile(testConfigFilename);

            FabricUpgradeProgress importResponse = new FabricUpgradeHandler().ImportAdfSupportFile(
                testConfig.Progress?.ToString(),
                "./TestFiles/AdfSupportFiles/" + testConfig.AdfSupportFile,
                true);

            FabricUpgradeProgress actualConvertResponse = new FabricUpgradeHandler().ConvertToFabricResources(importResponse.ToString()); 

            JObject actualResponseObject = actualConvertResponse.ToJObject();

            JObject expectedResponseObject = testConfig.ExpectedResponse;

            JObject mismatches = JsonUtils.DeepCompare(expectedResponseObject, actualResponseObject);

            Console.WriteLine(actualConvertResponse.ToString().Replace("\r", "").Replace("\n", ""));

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

        /// <summary>
        /// Test that ConvertTo-FabricResources preserves the ImportedResourcesKey for workspace creation.
        /// This validates the fix for the issue where ADF subscription/resource group information
        /// was being lost during the conversion process.
        /// </summary>
        [TestMethod]
        public void ConvertToFabricResources_PreservesImportedResourcesKey_Test()
        {
            // Arrange: Create a mock progress with ImportedResourcesKey containing ADF info
            var mockAdfUpgradePackage = new JObject
            {
                ["type"] = "AdfSupportFile",
                ["adfName"] = "test-adf-factory",
                ["subscriptionId"] = "12345678-1234-1234-1234-123456789012",
                ["resourceGroupName"] = "test-resource-group", 
                ["adfRegion"] = "East US",
                ["pipelines"] = new JObject(),
                ["datasets"] = new JObject(),
                ["linkedServices"] = new JObject(),
                ["triggers"] = new JObject()
            };

            var inputProgress = new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                Alerts = new List<FabricUpgradeAlert>(),
                Resolutions = new List<FabricUpgradeResolution>(),
                Result = new JObject
                {
                    [FabricUpgradeProgress.ImportedResourcesKey] = mockAdfUpgradePackage
                }
            };

            // Act: Call ConvertToFabricResources
            var handler = new FabricUpgradeHandler();
            var result = handler.ConvertToFabricResources(inputProgress.ToString());

            // Assert: Verify the result contains both ExportableFabricResourcesKey AND ImportedResourcesKey
            Assert.AreEqual(FabricUpgradeProgress.FabricUpgradeState.Succeeded, result.State,
                $"ConvertToFabricResources should succeed. Alerts: {string.Join(", ", result.Alerts.Select(a => a.Details))}");

            Assert.IsNotNull(result.Result, "Result should not be null");

            // Verify ExportableFabricResourcesKey exists (conversion output)
            Assert.IsTrue(result.Result.ContainsKey(FabricUpgradeProgress.ExportableFabricResourcesKey),
                "Result should contain ExportableFabricResourcesKey after conversion");

            // Verify ImportedResourcesKey is preserved (critical for workspace creation)
            Assert.IsTrue(result.Result.ContainsKey(FabricUpgradeProgress.ImportedResourcesKey),
                "Result should still contain ImportedResourcesKey to preserve ADF information for workspace creation");

            // Verify the preserved ImportedResourcesKey contains the original ADF information
            var preservedImportedResources = result.Result[FabricUpgradeProgress.ImportedResourcesKey] as JObject;
            Assert.IsNotNull(preservedImportedResources, "Preserved ImportedResources should not be null");
            
            Assert.AreEqual("test-adf-factory", preservedImportedResources["adfName"]?.ToString(),
                "ADF name should be preserved");
            Assert.AreEqual("12345678-1234-1234-1234-123456789012", preservedImportedResources["subscriptionId"]?.ToString(),
                "ADF subscription ID should be preserved");
            Assert.AreEqual("test-resource-group", preservedImportedResources["resourceGroupName"]?.ToString(),
                "ADF resource group should be preserved");
            Assert.AreEqual("East US", preservedImportedResources["adfRegion"]?.ToString(),
                "ADF region should be preserved");

            Console.WriteLine("? ConvertToFabricResources correctly preserves ImportedResourcesKey for workspace creation");
        }

        /// <summary>
        /// Test that the preserved ADF information can be extracted for workspace creation.
        /// This simulates the ExtractAdfInfoFromProgress method behavior.
        /// </summary>
        [TestMethod] 
        public void ConvertToFabricResources_PreservedAdfInfo_CanBeExtracted_Test()
        {
            // Arrange: Create mock imported resources with ADF info
            var mockAdfUpgradePackage = new JObject
            {
                ["type"] = "AdfSupportFile",
                ["adfName"] = "capacity-name-test-adf",
                ["subscriptionId"] = "test-subscription-id",
                ["resourceGroupName"] = "test-rg",
                ["adfRegion"] = "West US",
                ["pipelines"] = new JObject(),
                ["datasets"] = new JObject(), 
                ["linkedServices"] = new JObject(),
                ["triggers"] = new JObject()
            };

            var inputProgress = new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                Alerts = new List<FabricUpgradeAlert>(),
                Resolutions = new List<FabricUpgradeResolution>(),
                Result = new JObject
                {
                    [FabricUpgradeProgress.ImportedResourcesKey] = mockAdfUpgradePackage
                }
            };

            // Act: Convert and then simulate ADF info extraction
            var handler = new FabricUpgradeHandler();
            var convertResult = handler.ConvertToFabricResources(inputProgress.ToString());

            // Parse the result back to simulate what ExtractAdfInfoFromProgress does
            var progressAfterConvert = FabricUpgradeProgress.FromString(convertResult.ToString());

            // Assert: Verify the ADF info can be extracted from the preserved ImportedResourcesKey
            Assert.IsTrue(progressAfterConvert.Result.ContainsKey(FabricUpgradeProgress.ImportedResourcesKey),
                "Converted progress should contain ImportedResourcesKey");

            var importedResourcesToken = progressAfterConvert.Result[FabricUpgradeProgress.ImportedResourcesKey];
            var upgradePackage = AdfSupportFileUpgradePackage.FromJToken(importedResourcesToken);

            Assert.IsNotNull(upgradePackage, "Should be able to parse ImportedResources as AdfSupportFileUpgradePackage");
            Assert.AreEqual("capacity-name-test-adf", upgradePackage.AdfName, "ADF name should be extractable");
            Assert.AreEqual("test-subscription-id", upgradePackage.SubscriptionId, "Subscription ID should be extractable"); 
            Assert.AreEqual("test-rg", upgradePackage.ResourceGroupName, "Resource group should be extractable");
            Assert.AreEqual("West US", upgradePackage.AdfRegion, "ADF region should be extractable");

            // Simulate capacity name generation using the extracted ADF info
            var capacityName = WorkspaceCreationHelper.GenerateCapacityName(upgradePackage.AdfName, null);
            
            // Verify capacity name is generated without hyphens (the original issue)
            Assert.IsFalse(capacityName.Contains("-"), $"Capacity name '{capacityName}' should not contain hyphens");
            Assert.IsTrue(capacityName.Contains("capacitynametestadffabric"), 
                $"Capacity name '{capacityName}' should contain sanitized ADF name");

            Console.WriteLine($"? Generated capacity name: {capacityName}");
            Console.WriteLine("? ADF information successfully extracted from preserved ImportedResourcesKey");
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
// <copyright file="InvalidAdfModelTests.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Upgraders.DatasetUpgraders;
using FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;
using static FabricUpgradePowerShellModule.Models.FabricUpgradeAlert;

namespace FabricUpgradePowerShellModuleTests
{
    /// <summary>
    /// These tests exercise the code that handles invalid ADF models in various upgraders.
    /// These are otherwise very difficult to test because it is difficult to create an invalid ADF model in ADF.
    /// </summary>
    [TestClass]
    public class InvalidAdfModelTests
    {
        [TestMethod]
        public void InvalidAzureBlobStorageLinkedServiceModelTest()
        {
            JObject adfModelWithInvalidConnectionString = JObject.Parse(@"
                {
                    ""name"": ""TheLinkedService"", 
                    ""properties"": {
                        ""type"": ""AzureBlobStorage"",
                        ""typeProperties"": { 
                            ""connectionString"": [""a"", ""b""]
                        }
                    }
                }");

            JObject adfModelWithNullConnectionString = JObject.Parse(@"
                {
                    ""name"": ""TheLinkedService"", 
                    ""properties"": {
                        ""type"": ""AzureBlobStorage"",
                        ""typeProperties"": { 
                        }
                    }
                }");

            var upgrader = new AzureBlobStorageLinkedServiceUpgrader(adfModelWithInvalidConnectionString, null);
            var alertCollector1 = new AlertCollector();
            upgrader.Compile(alertCollector1);

            var alerts = alertCollector1.ToList();

            Assert.AreEqual(1, alerts.Count);

            Assert.AreEqual(AlertSeverity.Permanent, alerts[0].Severity);
            Assert.AreEqual("Cannot upgrade LinkedService 'TheLinkedService' because its ConnectionString is not a string.", alerts[0].Details);

            upgrader = new AzureBlobStorageLinkedServiceUpgrader(adfModelWithNullConnectionString, null);
            var alertCollector2 = new AlertCollector();
            upgrader.Compile(alertCollector2);

            alerts = alertCollector2.ToList();

            Assert.AreEqual(1, alerts.Count);

            Assert.AreEqual(AlertSeverity.Permanent, alerts[0].Severity);
            Assert.AreEqual("Cannot upgrade LinkedService 'TheLinkedService' because its ConnectionString is missing.", alerts[0].Details);

        }
    }
}
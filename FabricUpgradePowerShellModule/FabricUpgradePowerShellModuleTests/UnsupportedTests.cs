// <copyright file="UnsupportedTests.cs" company="Microsoft">
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
    /// These tests exercise the Unsupported upgraders.
    /// These are otherwise very difficult to test because it is difficult to create an unsupported resource in ADF.
    /// </summary>
    [TestClass]
    public class UnsupportedTests
    {
        [TestMethod]
        public void UnsupportedDatasetUpgraderTest()
        {
            JObject datasetObject = JObject.Parse(@"{""name"": ""TheDataset"", ""properties"": { ""type"": ""Unsupported"" } }");
            var upgrader = new UnsupportedDatasetUpgrader(datasetObject, null);
            var alertCollector = new AlertCollector();
            upgrader.Compile(alertCollector);
            
            Assert.AreEqual(2, alertCollector.Count);

            var alerts = alertCollector.ToList();

            Assert.AreEqual(AlertSeverity.Permanent, alerts[0].Severity);
            Assert.AreEqual("Dataset 'TheDataset' property properties.linkedServiceName.referenceName must not be null.", alerts[0].Details);

            Assert.AreEqual(AlertSeverity.UnsupportedResource, alerts[1].Severity);
            Assert.AreEqual("Cannot upgrade Dataset 'TheDataset' because its Type is 'Unsupported'.", alerts[1].Details);
        }

        [TestMethod]
        public void UnsupportedLinkedServiceUpgraderTest()
        {
            JObject linkedServiceObject = JObject.Parse(@"{""name"": ""TheLinkedService"", ""properties"": { ""type"": ""Unsupported"" } }");

            var upgrader = new UnsupportedLinkedServiceUpgrader(linkedServiceObject, null);
            var alertCollector = new AlertCollector();
            upgrader.Compile(alertCollector);
            
            Assert.AreEqual(1, alertCollector.Count);

            var alerts = alertCollector.ToList();

            Assert.AreEqual(AlertSeverity.UnsupportedResource, alerts[0].Severity);
            Assert.AreEqual("Cannot upgrade LinkedService 'TheLinkedService' because its Type is 'Unsupported'.", alerts[0].Details);
        }
    }
}
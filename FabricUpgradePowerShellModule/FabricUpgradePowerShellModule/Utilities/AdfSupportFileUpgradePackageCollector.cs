// <copyright file="AdfSupportFileUpgradePackageCollector.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Utilities
{
    /// <summary>
    /// A class to accept the callbacks from Unzip and collect the contents of the ZIP file.
    /// </summary>
    internal class AdfSupportFileUpgradePackageCollector : IUpgradePackageCollector
    {
        private AdfSupportFileUpgradePackage upgradePackage = new AdfSupportFileUpgradePackage();

        /// <inheritdoc/>
        public void Collect(string entryName, string entryData)
        {
            if (entryName == "diagnostic.json")
            {
                this.ProcessDiagnosticFile(entryData);
            }
            else if (entryName.StartsWith("pipeline/") && !entryName.EndsWith("/"))
            {
                JObject pipelineObject = JObject.Parse(entryData);
                string pipelineName = pipelineObject.SelectToken("$.name").ToString();
                upgradePackage.Pipelines[pipelineName] = JObject.Parse(entryData);
            }
            else if (entryName.StartsWith("dataset/") && !entryName.EndsWith("/"))
            {
                JObject datasetObject = JObject.Parse(entryData);
                string datasetName = datasetObject.SelectToken("$.name").ToString();
                upgradePackage.Datasets[datasetName] = JObject.Parse(entryData);
            }
            else if (entryName.StartsWith("linkedService/") && !entryName.EndsWith("/"))
            {
                JObject linkedServiceObject = JObject.Parse(entryData);
                string linkedServiceName = linkedServiceObject.SelectToken("$.name").ToString();
                upgradePackage.LinkedServices[linkedServiceName] = JObject.Parse(entryData);
            }
            else if (entryName.StartsWith("trigger/") && !entryName.EndsWith("/"))
            {
                JObject triggerObject = JObject.Parse(entryData);
                string triggerName = triggerObject.SelectToken("$.name").ToString();
                upgradePackage.Triggers[triggerName] = JObject.Parse(entryData);
            }
        }

        /// <summary>
        /// Construct the JObject that can be inserted into the FabricUpgradeProgress' Result field.
        /// </summary>
        /// <returns>The Result that will be returned to the client in the FabricUpgradeProgress.</returns>
        public JObject Build()
        {
            JObject built = new JObject();
            built[FabricUpgradeProgress.ImportedResourcesKey] = UpgradeSerialization.ToJToken(upgradePackage); // JObject.Parse(UpgradeSerialization.Serialize(upgradePackage));
            return built;
        }

        /// <summary>
        /// Extract some data from the diagnostic.json file and add it to the UpgradePackage.
        /// </summary>
        /// <param name="entryData">The contents of the diagnostic.json file.</param>
        private void ProcessDiagnosticFile(string entryData)
        {
            JObject diagnostic = JObject.Parse(entryData);
            this.upgradePackage.AdfName = diagnostic.SelectToken("$.environment.resourceName")?.ToString() ?? string.Empty;
        }
    }
}

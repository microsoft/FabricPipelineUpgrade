using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Utilities
{
    internal class AdfSupportFileUpgradePackageCollector : IUpgradePackageCollector
    {
        private AdfSupportFileUpgradePackage upgradePackage = new AdfSupportFileUpgradePackage()
        {
            Type = AdfUpgradePackage.UpgradePackageType.AdfSupportFile,
        };

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

        public JObject Build()
        {
            return JObject.Parse(UpgradeSerialization.Serialize(upgradePackage));
        }

        private void ProcessDiagnosticFile(string entryData)
        {
            JObject diagnostic = JObject.Parse(entryData);
            this.upgradePackage.AdfName = diagnostic.SelectToken("$.environment.resourceName")?.ToString() ?? string.Empty;
        }
    }
}

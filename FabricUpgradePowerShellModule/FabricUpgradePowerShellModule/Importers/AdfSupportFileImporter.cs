// <copyright file="AdfSupportFileImporter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Importers
{
    /// <summary>
    /// This class Imports an ADF Support File.
    /// </summary>
    public class AdfSupportFileImporter : IUpgradePackageCollector
    {
        private readonly FabricUpgradeProgress progress;
        private readonly string adfSupportFilename;
        private readonly AlertCollector alerts;

        private readonly AdfSupportFileUpgradePackage upgradePackage = new AdfSupportFileUpgradePackage();

        public AdfSupportFileImporter(
            FabricUpgradeProgress progress,
            string adfSupportFilename,
            AlertCollector alerts)
        {
            this.progress = progress;
            this.adfSupportFilename = adfSupportFilename;
            this.alerts = alerts;
        }

        public FabricUpgradeProgress Import()
        {
            byte[] supportFileData;
            try
            {
                supportFileData = File.ReadAllBytes(this.adfSupportFilename);
            }
            catch (Exception)
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                }
                .WithAlert(
                    new FabricUpgradeAlert()
                    {
                        Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                        Details = $"Failed to load Support File '{adfSupportFilename}'.",
                    });
            }

            try
            {
                UpgradeUnzipper.Unzip(supportFileData, this);
            }
            catch (Exception)
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.alerts.ToList(),
                }
                .WithAlert(
                    new FabricUpgradeAlert()
                    {
                        Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                        Details = "Failed to unzip Upgrade Package.",
                    });
            }

            return new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                Alerts = this.alerts.ToList(),
                Result = this.BuildResult(),
                Resolutions = this.progress.Resolutions,
            };
        }

        /// <inheritdoc/>
        public void CollectZipFileEntry(string entryName, string entryData)
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
        public JObject BuildResult()
        {
            JObject built = new JObject();
            built[FabricUpgradeProgress.ImportedResourcesKey] = UpgradeSerialization.ToJToken(upgradePackage);
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


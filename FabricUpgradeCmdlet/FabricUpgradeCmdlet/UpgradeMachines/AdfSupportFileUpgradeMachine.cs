// <copyright file="AdfSupportFilesUpgradeMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Upgraders;
using FabricUpgradeCmdlet.Upgraders.DatasetUpgraders;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.UpgradeMachines
{
    /// <summary>
    /// A FabricUpgradeMachine to process an ADF Support File.
    /// </summary>
    public class AdfSupportFileUpgradeMachine : FabricUpgradeMachine
    {
        private AdfSupportFileUpgradePackage upgradePackage;

        public AdfSupportFileUpgradeMachine(
            JObject toUpgrade,
            List<FabricUpgradeResolution> resolutions,
            AlertCollector alerts)
            : base(resolutions, alerts)
        {
            this.upgradePackage = AdfSupportFileUpgradePackage.FromJToken(toUpgrade);
        }

        /// <inheritdoc/>
        public override FabricUpgradeProgress Upgrade()
        {
            foreach (var entry in this.upgradePackage.Pipelines)
            {
                JToken pipelineToken = entry.Value;
                Upgrader pipelineUpgrader = new PipelineUpgrader(pipelineToken, this);
                this.Upgraders.Add(pipelineUpgrader);
            }

            foreach (var entry in this.upgradePackage.Datasets)
            {
                JToken datasetToken = entry.Value;
                Upgrader datasetUpgrader = DatasetUpgrader.CreateDatasetUpgrader(datasetToken, this);
                this.Upgraders.Add(datasetUpgrader);
            }

            /*
            foreach (var entry in this.upgradePackage.LinkedServices)
            {
                JToken pipelineToken = entry.Value;
                Upgrader pipelineUpgrader = new PipelineUpgrader(pipelineToken, this);
                this.Upgraders.Add(pipelineUpgrader);
            }
            */

            try
            {
                JObject result = this.PerformUpgrade();

                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                    Alerts = this.Alerts.ToList(),
                    Result = result,
                };
            }
            catch (UpgradeFailureException)
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.Alerts.ToList(),
                };
            }

        }

        private JObject PerformUpgrade()
        {
            this.CompileUpgraders();
            this.PreLinkUpgraders();
            this.SortUpgraders();
            return this.GenerateFabricResources();
        }

        private void CompileUpgraders()
        {
            foreach (Upgrader upgrader in this.Upgraders)
            {
                upgrader.Compile(this.Alerts);
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("Compile");
            }
        }

        private void PreLinkUpgraders()
        {
            foreach (Upgrader upgrader in this.Upgraders)
            {
                upgrader.PreLink(this.Upgraders, this.Alerts);
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("PreLink");
            }
        }

        private void SortUpgraders()
        {
            List<Upgrader> sortedUpgraders = new List<Upgrader>();
            while (true)
            {
                Upgrader unsortedUpgrader = this.Upgraders.Where(u => u.SortingState == Upgrader.UpgraderSortingState.Unsorted).FirstOrDefault();
                if (unsortedUpgrader == null)
                {
                    this.Upgraders = sortedUpgraders;
                    break;
                }

                unsortedUpgrader.Sort(sortedUpgraders, this.Alerts);
            }
            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("Sort");
            }
        }

        private JObject GenerateFabricResources()
        {
            JArray fabricResources = new JArray();
            foreach (Upgrader upgrader in this.Upgraders)
            {
                Symbol pipelineSymbol = upgrader.ResolveExportedSymbol("fabricResource", this.Alerts);
                if (pipelineSymbol.State == Symbol.SymbolState.Ready)
                {
                    if (pipelineSymbol.Value != null)
                    {
                        fabricResources.Add(pipelineSymbol.Value);
                    }
                }
                else
                {
                    this.Alerts.AddPermanentError($"Cannot upgrade {upgrader.Name}.");

                }
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("Precheck");
            }

            JObject result = new JObject();
            result["fabricResources"] = fabricResources;

            return result;
        }

        private bool AlertsIndicateFailure()
        {
            return this.Alerts.Any(f => f.Severity != FabricUpgradeAlert.FailureSeverity.Warning);
        }

        /*
        /// <summary>
        /// From the unzipped file, build the appropriate Upgrader, extract some metadata, etc..
        /// </summary>
        /// <param name="fileName">The name of the file (lets us interpret the type of the contents).</param>
        /// <param name="fileContents">The contents of the file.</param>
        private void ProcessFile(
            string fileName,
            string fileContents)
        {
            if (fileName == "diagnostic.json")
            {
                JObject diagnosticInfo = JObject.Parse(fileContents);
                this.ProcessDiagnosticInfo(diagnosticInfo);
            }
            else if (fileName.StartsWith("pipeline/") && !fileName.EndsWith("/"))
            {
                JObject pipelineToken = JObject.Parse(fileContents);
                this.Upgraders.Add(new PipelineUpgrader(pipelineToken, this));
            }
            else if (fileName.StartsWith("dataset/") && !fileName.EndsWith("/"))
            {
                JObject datasetToken = JObject.Parse(fileContents);
                this.Upgraders.Add(DatasetUpgrader.CreateDatasetUpgrader(datasetToken, this));
            }
            else if (fileName.StartsWith("linkedService/") && !fileName.EndsWith("/"))
            {
                JObject linkedServiceToken = JObject.Parse(fileContents);
                this.Upgraders.Add(LinkedServiceUpgrader.CreateLinkedServiceUpgrader(linkedServiceToken, this));
            }
            else if (fileName.StartsWith("trigger/") && !fileName.EndsWith("/"))
            {
                JObject triggerToken = JObject.Parse(fileContents);
                this.Upgraders.Add(TriggerUpgrader.CreateTriggerUpgrader(triggerToken, this));
            }
        }
        */

        /*
        /// <summary>
        /// Process the contents of the diagnostic.json file.
        /// </summary>
        /// <param name="diagnosticInfo">The contents of the diagnostic.json file.</param>
        private void ProcessDiagnosticInfo(
            JToken diagnosticInfo)
        {
            DiagnosticModel diagnosticModel = DiagnosticModel.Build(diagnosticInfo);
            string originalDataFactory = diagnosticModel.DataFactoryName;
            if (!string.IsNullOrEmpty(originalDataFactory))
            {
                this.Metadata.OriginalDataFactory = originalDataFactory;
            }
        }
        */
    }
}

// <copyright file="AdfSupportFilesUpgradeMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradeCmdlet.UpgradeMachines
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FabricUpgradeCmdlet.Models;
    using FabricUpgradeCmdlet.Upgraders;
    using FabricUpgradeCmdlet.Utilities;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// A FabricUpgradeMachine to process ADF Support Files.
    /// </summary>
    public class AdfSupportFilesUpgradeMachine : FabricUpgradeMachine
    {
        private AdfSupportFileUpgradePackage upgradePackage;
        private List<Upgrader> pipelineUpgraders = new List<Upgrader>();

        public AdfSupportFilesUpgradeMachine(
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
            foreach (var pipelineEntry in this.upgradePackage.Pipelines)
            {
                JToken pipelineToken = pipelineEntry.Value;
                Upgrader pipelineUpgrader = new PipelineUpgrader(pipelineToken, this);
                this.Upgraders.Add(pipelineUpgrader);
                this.pipelineUpgraders.Add(pipelineUpgrader);
            }

            // TODO: Add upgraders for Datasets.

            // TODO: Add upgraders for LinkedServices.

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
            this.CompilePipelines();
            return this.ResolvePipelines();
        }

        private void CompilePipelines()
        {
            foreach (Upgrader upgrader in this.pipelineUpgraders)
            {
                upgrader.Compile(this.Alerts);
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("Precheck");
            }
        }

        private JObject ResolvePipelines()
        {
            JObject result = new JObject();
            foreach (Upgrader upgrader in this.pipelineUpgraders)
            {
                Symbol pipelineSymbol = upgrader.ResolveExportedSymbol("pipeline", this.Alerts);
                if (pipelineSymbol.State == Symbol.SymbolState.Ready)
                {
                    result[upgrader.Name] = pipelineSymbol.Value;
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

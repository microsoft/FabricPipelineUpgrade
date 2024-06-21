// <copyright file="AdfSupportFileUpgradeMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Exceptions;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Upgraders;
using FabricUpgradeCmdlet.Upgraders.DatasetUpgraders;
using FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders;
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
            try
            {
                this.BuildUpgraders();
                this.CompileUpgraders();
                this.PreLinkUpgraders();
                this.SortUpgraders();
                JObject result = this.GenerateExportInstructions();

                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                    Alerts = this.Alerts.ToList(),
                    Result = result,
                    Resolutions = this.Resolutions,
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

        /// <summary>
        /// Construct all of the Upgraders and add them to the Upgraders list.
        /// </summary>
        private void BuildUpgraders()
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

            foreach (var entry in this.upgradePackage.LinkedServices)
            {
                JToken linkedServiceToken = entry.Value;
                Upgrader pipelineUpgrader = LinkedServiceUpgrader.CreateLinkedServiceUpgrader(linkedServiceToken, this);
                this.Upgraders.Add(pipelineUpgrader);
            }
        }

        /// <summary>
        /// Invoke the Compile method on all of the Upgraders.
        /// </summary>
        /// <exception cref="UpgradeFailureException"></exception>
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

        /// <summary>
        /// Invoke the PreLink method on all of the Upgraders.
        /// </summary>
        /// <exception cref="UpgradeFailureException"></exception>
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

        /// <summary>
        /// Perform a topological sort on the Upgraders, to ensure that we Generate them in the correct order.
        /// </summary>
        /// <exception cref="UpgradeFailureException"></exception>
        private void SortUpgraders()
        {
            List<Upgrader> sortedUpgraders = new List<Upgrader>();
            while (true)
            {
                Upgrader unsortedUpgrader = this.Upgraders
                    .Where(u => u.SortingState == Upgrader.UpgraderSortingState.Unsorted)
                    .FirstOrDefault();

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

        private JObject GenerateExportInstructions()
        {
            JArray fabricResources = new JArray();
            foreach (Upgrader upgrader in this.Upgraders)
            {
                Symbol resourceSymbol = upgrader.ResolveExportedSymbol(Symbol.CommonNames.ExportInstructions, this.Alerts);
                if (resourceSymbol.State == Symbol.SymbolState.Ready)
                {
                    if (resourceSymbol.Value != null)
                    {
                        fabricResources.Add(resourceSymbol.Value);
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
            result[FabricUpgradeProgress.ExportableFabricResourcesKey] = fabricResources;

            return result;
        }

        /// <summary>
        /// Check the Alerts; if any are worse than a Warning, then the Upgrade has failed.
        /// </summary>
        /// <returns>True if the Upgrade has failed; False otherwise.</returns>
        private bool AlertsIndicateFailure()
        {
            return this.Alerts.Any(f => f.Severity != FabricUpgradeAlert.AlertSeverity.Warning);
        }
    }
}

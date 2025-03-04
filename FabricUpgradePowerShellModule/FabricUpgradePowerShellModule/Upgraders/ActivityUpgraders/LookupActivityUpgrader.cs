// <copyright file="LookupActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// Upgrades an ADF Lookup activity to a Fabric Lookup activity.
    /// This version brings in dataset details from the referenced dataset and
    /// writes them into the Fabric activity so that the dataset is selected.
    /// </summary>
    public class LookupActivityUpgrader : ActivityUpgrader
    {
        // JSON paths for key properties in the ADF Lookup activity.
        private const string adfSourcePath = "typeProperties.source";
        private const string adfDatasetReferencePath = "typeProperties.dataset.referenceName";
        private const string adfFirstRowOnlyPath = "typeProperties.firstRowOnly";

        // Reference to the dataset upgrader.
        private Upgrader datasetUpgrader;

        public LookupActivityUpgrader(string parentPath, JToken activityToken, IFabricUpgradeMachine machine)
            : base(ActivityTypes.Lookup, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
            // Ensure required properties exist.
            this.CheckRequiredAdfProperties(new List<string> { adfSourcePath, adfDatasetReferencePath }, alerts);
        }

        /// <inheritdoc/>
        public override void PreSort(List<Upgrader> allUpgraders, AlertCollector alerts)
        {
            // Look up the dataset upgrader for the referenced dataset.
            datasetUpgrader = this.FindOtherUpgrader(allUpgraders, FabricUpgradeResourceTypes.Dataset, adfDatasetReferencePath, alerts);
            if (datasetUpgrader != null)
            {
                this.DependsOn.Add(datasetUpgrader);
            }
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.Activity)
            {
                return BuildActivitySymbol(parameterAssignments, alerts);
            }
            // Minimal implementation: skip export resolution steps.
            return Symbol.ReadySymbol(null);
        }

        /// <summary>
        /// Builds the final Fabric Lookup activity JSON, including dataset details.
        /// </summary>
        protected override Symbol BuildActivitySymbol(Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            // Use the helper method from the base class to get the common activity JSON.
            Symbol baseSymbol = this.BuildCommonActivitySymbol(alerts);
            JObject fabricActivity = baseSymbol?.Value as JObject ?? new JObject();

            // Explicitly copy the dependency information from the original ADF activity.
            JToken adfDependsOn = this.AdfResourceToken.SelectToken("dependsOn");
            if (adfDependsOn != null)
            {
                // Overwrite the Fabric JSON's "dependsOn" with the original dependencies.
                fabricActivity["dependsOn"] = adfDependsOn.DeepClone();
            }

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivity, alerts);
            copier.Copy("description");
            copier.Copy(adfSourcePath);
            copier.Copy(adfFirstRowOnlyPath);
            copier.Copy("dependsOn");


            // Copy the dataset reference.
            string datasetRef = this.AdfResourceToken.SelectToken(adfDatasetReferencePath)?.ToString();
            if (!string.IsNullOrWhiteSpace(datasetRef))
            {
                // Set the dataset ID for Fabric.
                copier.Set("typeProperties.datasetId", datasetRef);
            }

            // Retrieve dataset details from the dataset upgrader.
            if (datasetUpgrader != null)
            {
                Symbol datasetSettingsSymbol = datasetUpgrader.EvaluateSymbol(Symbol.CommonNames.DatasetSettings, parameterAssignments, alerts);
                if (datasetSettingsSymbol != null &&
                    datasetSettingsSymbol.State == Symbol.SymbolState.Ready &&
                    datasetSettingsSymbol.Value != null)
                {
                    // Instead of using a separate property, write the dataset details
                    // directly into "typeProperties.dataset" so the dataset is selected.
                    copier.Set("typeProperties.dataset", datasetSettingsSymbol.Value);
                }
            }

            // Set the operation type for Fabric.
            copier.Set("typeProperties.operationType", "Lookup");


            return Symbol.ReadySymbol(fabricActivity);
        }

        /// <summary>
        /// Minimal implementation: No export resolution steps.
        /// </summary>
        protected override Symbol BuildExportResolveStepsSymbol(Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            return Symbol.ReadySymbol(null);
        }
    }
}

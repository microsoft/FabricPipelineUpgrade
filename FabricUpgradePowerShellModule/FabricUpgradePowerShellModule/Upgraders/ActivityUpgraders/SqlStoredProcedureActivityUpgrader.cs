// <copyright file="StoredProcedureActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders;
using FabricUpgradePowerShellModule.Utilities;

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// Upgrades ADF Stored Procedure activity to a Fabric SqlServerStoredProcedure activity.
    /// This version brings in resolve steps to from the referenced linked service and
    /// writes them into the Fabric activity so that the connection is selected.
    /// </summary>
    public class StoredProcedureActivityUpgrader : ActivityUpgrader
    {
        // JSON paths for key properties in the ADF Stored Procedure activity.
        private const string adfStoredProcedureNamePath = "typeProperties.storedProcedureName";
        private const string adfLinkedServiceReferencePath = "linkedServiceName.referenceName";

        // Reference to the linked service upgrader.
        private LinkedServiceUpgrader linkedServiceUpgrader { get; set; }

        public StoredProcedureActivityUpgrader(string parentPath, JToken activityToken, IFabricUpgradeMachine machine)
            : base(ActivityTypes.SqlStoredProcedure, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
            // Ensure required properties exist.
            this.CheckRequiredAdfProperties(new List<string> { adfStoredProcedureNamePath, adfLinkedServiceReferencePath }, alerts);
        }

        /// <inheritdoc/>
        public override void PreSort(List<Upgrader> allUpgraders, AlertCollector alerts)
        {
            // Look up the linked service upgrader for the referenced linked service.
            Upgrader upgrader = this.FindOtherUpgrader(allUpgraders, FabricUpgradeResourceTypes.LinkedService, adfLinkedServiceReferencePath, alerts);
            linkedServiceUpgrader = (LinkedServiceUpgrader)upgrader;
            if (linkedServiceUpgrader != null)
            {
                this.DependsOn.Add(linkedServiceUpgrader);
            }
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(string symbolName, Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
            {
                return this.BuildExportResolveStepsSymbol(parameterAssignments, alerts);
            }
            if (symbolName == Symbol.CommonNames.Activity)
            {
                return BuildActivitySymbol(parameterAssignments, alerts);
            }
            return Symbol.ReadySymbol(null);
        }

        /// <summary>
        /// Builds the final Fabric activity JSON, including resolve steps.
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
            copier.Copy(adfStoredProcedureNamePath, allowNull: false);

            Symbol databaseNameSymbol = this.linkedServiceUpgrader.EvaluateSymbol(Symbol.CommonNames.LinkedServiceDatabaseName, parameterAssignments, alerts);
            if (databaseNameSymbol.State == Symbol.SymbolState.Ready && databaseNameSymbol.Value != null)
            {
                copier.Set("typeProperties.database", databaseNameSymbol.Value);
            }
            else
            {
                // The linked service upgrader should have already alerted.
                copier.Set("typeProperties.database", "UNKNOWN");
            }

            copier.Copy("typeProperties.enforceOneTimeExecution", copyIfNull: false);
            copier.Copy("typeProperties.storedProcedureParameters", copyIfNull: false);

            // This property cannot be set until the Export operation phase.
            // We include this property in the "exportResolve" symbol.
            copier.Set("externalReferences.connection", Guid.Empty.ToString());

            // Set the operation type for Fabric.
            copier.Set("typeProperties.operationType", "SqlServerStoredProcedure");

            return Symbol.ReadySymbol(fabricActivity);
        }

        /// <summary>
        /// Build resolve steps so the linkedService's externalReferences.connection gets replaced.
        /// </summary>
        protected override Symbol BuildExportResolveStepsSymbol(Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            List<FabricExportResolveStep> resolves = new List<FabricExportResolveStep>();

            if (this.linkedServiceUpgrader != null)
            {
                // Ask the linkedService for its resolve steps (which resolve its LinkedService -> Connection resource id).
                Symbol linkedServiceResolveStepsSymbol = this.linkedServiceUpgrader.EvaluateSymbol(Symbol.CommonNames.ExportResolveSteps, parameterAssignments, alerts);
                if (linkedServiceResolveStepsSymbol.State == Symbol.SymbolState.Ready && linkedServiceResolveStepsSymbol.Value != null)
                {
                    foreach (JToken requiredLink in (JArray)linkedServiceResolveStepsSymbol.Value)
                    {
                        FabricExportResolveStep step = FabricExportResolveStep.FromJToken(requiredLink);
                        // Place inside this activity's path.
                        step.TargetPath = $"{step.TargetPath}"; // becomes properties.activities[n].externalReferences.connection
                        resolves.Add(step);
                    }
                }
            }

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }
    }
}

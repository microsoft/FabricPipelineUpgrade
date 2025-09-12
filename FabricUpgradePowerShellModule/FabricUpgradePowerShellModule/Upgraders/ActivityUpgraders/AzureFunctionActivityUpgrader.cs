// <copyright file="AzureFunctionActivityUpgrader.cs" company="Microsoft">
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
    /// Upgrades an ADF Azure Function activity to a Fabric InvokeAzureFunction activity.
    /// Supports anonymous access if typeProperties.azureFunctionConnection is null.
    /// </summary>
    public class AzureFunctionActivityUpgrader : ActivityUpgrader
    {
        // JSON paths for key properties in the ADF Azure Function activity.
        private const string adfFunctionNamePath = "typeProperties.functionName";
        private const string adfFunctionMethodPath = "typeProperties.method";
        private const string adfFunctionHeadersPath = "typeProperties.headers";
        private const string adfLinkedServiceReferencePath = "linkedServiceName.referenceName";

        // Required properties for a valid Azure Function activity.
        private readonly List<string> requiredAzureFunctionProperties = new List<string>
        {
            adfFunctionNamePath,
            adfFunctionMethodPath,
            adfLinkedServiceReferencePath
        };

        // Reference to the linked service upgrader.
        private LinkedServiceUpgrader linkedServiceUpgrader { get; set; }

        public AzureFunctionActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base("AzureFunctionActivity", parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
            // Ensure required properties exist.
            this.CheckRequiredAdfProperties(this.requiredAzureFunctionProperties, alerts);
        }

        /// <inheritdoc/>
        public override void PreSort(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
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
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
            {
                return this.BuildExportResolveStepsSymbol(parameterAssignments, alerts);
            }

            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameterAssignments, alerts);
            }

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildExportResolveStepsSymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
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

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
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
            copier.Copy(adfFunctionNamePath, allowNull: false);
            copier.Copy(adfFunctionMethodPath, allowNull: false);
            copier.Copy(adfFunctionHeadersPath, copyIfNull: false);

            // This property cannot be set until the Export operation phase.
            // We include this property in the "exportResolve" symbol.
            copier.Set("externalReferences.connection", Guid.Empty.ToString());

            return Symbol.ReadySymbol(fabricActivity);
        }
    }
}

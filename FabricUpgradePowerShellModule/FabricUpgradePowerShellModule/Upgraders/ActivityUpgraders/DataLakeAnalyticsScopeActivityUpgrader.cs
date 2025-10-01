// <copyright file="AzureDataExplorerCommandActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>


using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Upgraders.LinkedServiceUpgraders;
using FabricUpgradePowerShellModule.Utilities;

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    public class DataLakeAnalyticsScopeActivityUpgrader : ActivityUpgrader
    {
        // JSON paths for key properties in the ADF Stored Procedure activity.
        private const string linkedServiceReferencePath = "linkedServiceName.referenceName";
        private const string scriptLinkedServicePath = "typeProperties.scriptLinkedService";
        private const string scriptLinkedServiceReferencePath = "typeProperties.scriptLinkedService.referenceName";
        private const string scriptFolderPath = "typeProperties.scriptFolderPath";
        private const string scriptFilePath = "typeProperties.scriptFileName";
        private const string fabricAdlaConnectionPath = "externalReferences.connection";
        private const string fabricAdlsScriptConnectionPath = "typeProperties.externalReferences.connection";

        private LinkedServiceUpgrader linkedServiceUpgrader;
        private LinkedServiceUpgrader scriptLinkedServiceUpgrader;

        public DataLakeAnalyticsScopeActivityUpgrader(string parentPath, JToken activityToken, IFabricUpgradeMachine machine)
            : base(ActivityTypes.DataLakeAnalyticsScope, parentPath, activityToken, machine)
        {
        }

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
            // Ensure required properties exist.
            this.CheckRequiredAdfProperties(new List<string> { linkedServiceReferencePath, scriptLinkedServiceReferencePath, scriptFolderPath, scriptFilePath }, alerts);
        }

        /// <inheritdoc/>
        public override void PreSort(List<Upgrader> allUpgraders, AlertCollector alerts)
        {
            // Look up the linked service upgrader for the referenced linked service.
            linkedServiceUpgrader = (LinkedServiceUpgrader)this.FindOtherUpgrader(allUpgraders, FabricUpgradeResourceTypes.LinkedService, linkedServiceReferencePath, alerts);
            if (linkedServiceUpgrader != null)
            {
                this.DependsOn.Add(linkedServiceUpgrader);
            }

            // Look up the script service upgrader for the referenced linked service.
            scriptLinkedServiceUpgrader = (LinkedServiceUpgrader)this.FindOtherUpgrader(allUpgraders, FabricUpgradeResourceTypes.LinkedService, scriptLinkedServiceReferencePath, alerts);
            if (scriptLinkedServiceUpgrader != null)
            {
                this.DependsOn.Add(scriptLinkedServiceUpgrader);
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

            JToken scriptLinkedService = this.AdfResourceToken.SelectToken(scriptLinkedServicePath);
            if (scriptLinkedService != null && scriptLinkedService.Parent is JProperty property && property.Parent is JObject typePropertiesObject)
            {
                typePropertiesObject.Remove(property.Name);
            }

            copier.Copy("typeProperties", allowNull: true, copyIfNull: true);

            // These properties cannot be set until the Export operation phase.
            // We include this property in the "exportResolve" symbol.
            copier.Set(fabricAdlaConnectionPath, Guid.Empty.ToString());
            copier.Set(fabricAdlsScriptConnectionPath, Guid.Empty.ToString());

            return Symbol.ReadySymbol(fabricActivity);
        }

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

            if (this.scriptLinkedServiceUpgrader != null)
            {
                // Ask the linkedService for its resolve steps (which resolve its LinkedService -> Connection resource id).
                Symbol scriptLinkedServiceResolveStepsSymbol = this.scriptLinkedServiceUpgrader.EvaluateSymbol(Symbol.CommonNames.ExportResolveSteps, parameterAssignments, alerts);
                if (scriptLinkedServiceResolveStepsSymbol.State == Symbol.SymbolState.Ready && scriptLinkedServiceResolveStepsSymbol.Value != null)
                {
                    foreach (JToken requiredLink in (JArray)scriptLinkedServiceResolveStepsSymbol.Value)
                    {
                        FabricExportResolveStep step = FabricExportResolveStep.FromJToken(requiredLink);
                        // Place inside this activity's path.
                        step.TargetPath = $"{fabricAdlsScriptConnectionPath}"; // becomes properties.activities[n].externalReferences.connection
                        resolves.Add(step);
                    }
                }
            }

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }
    }
}

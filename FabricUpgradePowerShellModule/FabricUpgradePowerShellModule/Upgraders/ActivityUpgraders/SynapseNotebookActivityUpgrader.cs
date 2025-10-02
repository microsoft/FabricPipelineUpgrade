// <copyright file="SynapseNotebookActivityUpgrader.cs" company="Microsoft">
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
    /// This class Upgrades an ADF SynapseNotebook Activity to a Fabric Notebook Activity.
    /// </summary>
    /// <remarks>
    /// Note the name change!
    /// </remarks>
    public class SynapseNotebookActivityUpgrader : ActivityUpgrader
    {
        private const string adfNotebookToExecutePath = "typeProperties.notebook.referenceName";
        private const string adfLinkedServiceReferencePath = "linkedServiceName.referenceName";

        private LinkedServiceUpgrader linkedServiceUpgrader { get; set; }

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            adfNotebookToExecutePath
        };

        public SynapseNotebookActivityUpgrader(string parentPath, JToken activityToken, IFabricUpgradeMachine machine) 
            : base(ActivityTypes.SynapseNotebook, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);
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

            // The workspaceId will be included in the ExportFabricPipeline phase (from resolution file).
            FabricExportResolveStep workspaceIdResolve = new FabricExportResolveStep(
                FabricUpgradeResolution.ResolutionType.WorkspaceId,
                null,
                "typeProperties.workspaceId");
            resolves.Add(workspaceIdResolve);

            // We need to update the id of the notebook to execute
            // user should have premtively have Fabric resources.
            string notebookName = null;
            JToken referencedNotebookToken = this.AdfResourceToken.SelectToken(adfNotebookToExecutePath);

            if (referencedNotebookToken?.Type == JTokenType.String)
            {
                notebookName = referencedNotebookToken.ToString();
            }
            else
            {
                notebookName = $"{this.ActivityType}--{this.Name}";
            }

            FabricExportResolveStep referencedNotebookLink = new FabricExportResolveStep(
                FabricUpgradeResolution.ResolutionType.AdfResourceNameToFabricResourceId,
                $"{FabricUpgradeResourceTypes.Notebook}:{notebookName}",
                "typeProperties.notebookId");

            resolves.Add(referencedNotebookLink);

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

            // These properties cannot be set until the Export operation phase.
            // We include these properties in the ExportLinks symbol.
            copier.Set("typeProperties.notebookId", Guid.Empty.ToString());
            copier.Set("typeProperties.workspaceId", Guid.Empty.ToString());
            
            copier.Copy("typeProperties.parameters", copyIfNull: false);

            // This property cannot be set until the Export operation phase.
            // We include this property in the "exportResolve" symbol.
            copier.Set("externalReferences.connection", Guid.Empty.ToString());

            return Symbol.ReadySymbol(fabricActivity);
        }
    }
}

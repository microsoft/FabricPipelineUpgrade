// <copyright file="ExecutePipelineActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// This class Upgrades an ADF ExecutePipeline Activity to a Fabric InvokePipeline Activity.
    /// </summary>
    /// <remarks>
    /// Note the name change!
    /// </remarks>
    public class ExecutePipelineActivityUpgrader : ActivityUpgrader
    {
        private const string adfPipelineToExecutePath = "typeProperties.pipeline.referenceName";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            adfPipelineToExecutePath,
        };

        private Upgrader pipelineToExecute;

        public ExecutePipelineActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.InvokePipeline, parentPath, activityToken, machine)
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
            this.pipelineToExecute = this.FindOtherUpgrader(
                allUpgraders,
                FabricUpgradeResourceTypes.DataPipeline,
                adfPipelineToExecutePath,
                alerts);

            if (this.pipelineToExecute != null)
            {
                this.DependsOn.Add(this.pipelineToExecute);
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

            // The workspaceId is included in the ExportFabricPipeline phase.
            FabricExportResolveStep workspaceIdResolve = new FabricExportResolveStep(
                FabricUpgradeResolution.ResolutionType.WorkspaceId,
                null,
                "typeProperties.workspaceId");
            resolves.Add(workspaceIdResolve);



            FabricUpgradeResolution.ResolutionType resolutionType = FabricUpgradeResolution.ResolutionType.CredentialConnectionId;
            FabricExportResolveStep userCredentialConnectionResolve = new FabricExportResolveStep(
                resolutionType,
                "user",
                "externalReferences.connection")
                .WithHint(new FabricUpgradeConnectionHint()
                {
                    LinkedServiceName = null,
                    ConnectionType = "Fabric Data Pipelines",
                    Datasource = "FabricDataPipelines",
                }
                    .WithTemplate(new FabricUpgradeResolution()
                    {
                        Type = resolutionType,
                        Key = "user",
                        Value = "<Fabric Connection ID>"
                    }
               ));

            resolves.Add(userCredentialConnectionResolve);

            // We need to update the id of the pipeline to execute
            // after that pipeline has been created and has a Fabric resource ID.
            string pipelineToExecuteName = this.AdfResourceToken.SelectToken(adfPipelineToExecutePath)?.ToString();

            FabricExportResolveStep otherPipelineLink = new FabricExportResolveStep(
                FabricUpgradeResolution.ResolutionType.AdfResourceNameToFabricResourceId,
                $"{FabricUpgradeResourceTypes.DataPipeline}:{pipelineToExecuteName}",
                "typeProperties.pipelineId");

            resolves.Add(otherPipelineLink);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            Symbol activitySymbol = base.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);

            copier.Copy("description");
            // copier.Copy("policy"); // Recently removed in https://dev.azure.com/powerbi/MWC/_git/workload-di/pullrequest/577524
            copier.Copy("typeProperties.waitOnCompletion");
            copier.Copy("typeProperties.parameters");
            copier.Set("typeProperties.operationType", "InvokeFabricPipeline");

            // These properties cannot be set until the Export operation phase.
            // We include these properties in the ExportLinks symbol.
            copier.Set("typeProperties.pipelineId", Guid.Empty.ToString());
            copier.Set("typeProperties.workspaceId", Guid.Empty.ToString());

            // This property cannot be set until the Export operation phase.
            // We include this property in the "exportResolve" symbol.
            copier.Set("externalReferences.connection", Guid.Empty.ToString());

            return Symbol.ReadySymbol(fabricActivityObject);
        }
    }
}

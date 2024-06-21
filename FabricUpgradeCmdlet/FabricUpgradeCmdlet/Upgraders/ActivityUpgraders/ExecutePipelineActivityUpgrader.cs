// <copyright file="ExecutePipelineActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    // TODO: ADF ExecutePipeline => Fabric InvokePipeline!

    /// <summary>
    /// This class Upgrades an ADF ExecutePipeline Activity to a Fabric ExecutePipeline Activity.
    /// </summary>
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
        public override void PreLink(
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
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                return this.BuildExportLinksSymbol(parameters, alerts);
            }

            if (symbolName == Symbol.CommonNames.ExportResolves)
            {
                return this.BuildExportResolvesSymbol(parameters, alerts);
            }

            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameters, alerts);
            }

            return base.ResolveExportedSymbol(symbolName, parameters, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildExportLinksSymbol(
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            List<FabricExportLink> links = new List<FabricExportLink>();

            // We need to update the id of the pipeline to execute
            // after that pipeline has been created and has a Fabric resource ID.
            string pipelineToExecuteName = this.AdfResourceToken.SelectToken(adfPipelineToExecutePath)?.ToString();

            FabricExportLink otherPipelineLink = new FabricExportLink(
                $"{FabricUpgradeResourceTypes.DataPipeline}:{pipelineToExecuteName}",
                "typeProperties.pipelineId");

            links.Add(otherPipelineLink);

            // The workspaceId is included in the ExportFabricPipeline phase.
            FabricExportLink workspaceIdLink = new FabricExportLink(
                "$workspace",
                "typeProperties.workspaceId");

            links.Add(workspaceIdLink);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(links)));
        }

        /// <inheritdoc/>
        protected override Symbol BuildExportResolvesSymbol(
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            List<FabricExportResolve> resolves = new List<FabricExportResolve>();

            FabricUpgradeResolution.ResolutionType resolutionType = FabricUpgradeResolution.ResolutionType.CredentialConnectionId;
            FabricExportResolve userCredentialConnectionResolve = new FabricExportResolve(
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
                        Value = "<guid>"
                    }
               ));

            resolves.Add(userCredentialConnectionResolve);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            Symbol activitySymbol = base.ResolveExportedSymbol(Symbol.CommonNames.Activity, parameters, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);

            copier.Copy("description");
            copier.Copy("policy");
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

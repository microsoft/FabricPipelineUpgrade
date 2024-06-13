// <copyright file="ExecutePipelineActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
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
            : base(ActivityUpgrader.ActivityTypes.Wait, parentPath, activityToken, machine)
        {
        }

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);
        }

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

        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                List<FabricExportLink> links = new List<FabricExportLink>();

                // We need to update the id of the pipeline to execute
                // after that pipeline has been created and has a fabric resource ID.
                string pipelineToExecuteName = this.AdfResourceToken.SelectToken("$.typeProperties.pipeline.referenceName")?.ToString();

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

            if (symbolName == Symbol.CommonNames.ExportResolves)
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

            if (symbolName == Symbol.CommonNames.Activity)
            {
                Symbol activitySymbol = base.ResolveExportedSymbol(Symbol.CommonNames.Activity, alerts);

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

            return base.ResolveExportedSymbol(symbolName, alerts);
        }
    }
}

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    public class ExecutePipelineActivityUpgrader : ActivityUpgrader
    {
        private readonly List<string> requiredAdfProperties = new List<string>
        {
            "typeProperties.pipeline.referenceName",
        };

        private Upgrader pipelineToExecute;

        public ExecutePipelineActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.WaitActivity, parentPath, activityToken, machine)
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
                "typeProperties.pipeline.referenceName",
                alerts);

            if (pipelineToExecute != null)
            {
                this.DependsOn.Add(pipelineToExecute);
            }
        }

        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == "exportLinks")
            {
                List<FabricExportLink> links = new List<FabricExportLink>();

                // We need to update the id of the pipeline to execute
                // after that pipeline has been created and has a fabric resource ID.
                string pipelineToExecute = this.AdfResourceToken.SelectToken("$.typeProperties.pipeline.referenceName")?.ToString();

                FabricExportLink otherPipelineResolution = new FabricExportLink(
                    $"{FabricUpgradeResourceTypes.DataPipeline}:{pipelineToExecute}",
                    "typeProperties.pipelineId");

                links.Add(otherPipelineResolution);

                // The workspaceId is included in the ExportFabricPipeline phase.
                FabricExportLink workspaceIdResolution = new FabricExportLink(
                    "$workspace",
                    "typeProperties.workspaceId");

                links.Add(workspaceIdResolution);

                return Symbol.ReadySymbol(JArray.Parse(JsonConvert.SerializeObject(links)));
            }

            if (symbolName == "exportResolves")
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

                return Symbol.ReadySymbol(JArray.Parse(JsonConvert.SerializeObject(resolves)));

            }
            if (symbolName == "activity")
            {
                Symbol activitySymbol = base.ResolveExportedSymbol("activity.common", alerts);

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
                // We include these properties in the "exportLinks" symbol.
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

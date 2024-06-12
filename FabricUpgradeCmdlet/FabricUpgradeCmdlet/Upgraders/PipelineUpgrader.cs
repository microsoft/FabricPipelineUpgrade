using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Upgraders.ActivityUpgraders;
using FabricUpgradeCmdlet.Utilities;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Upgraders
{
    public class PipelineUpgrader : Upgrader
    {
        protected AdfPipelineModel adfModel;
        protected List<Upgrader> activityUpgraders = new List<Upgrader>();

        public PipelineUpgrader(
            JToken pipelineToken,
            IFabricUpgradeMachine machine)
            : base(pipelineToken, machine)
        {
            this.UpgraderType = FabricUpgradeResourceTypes.DataPipeline;
            this.adfModel = AdfPipelineModel.FromJToken(pipelineToken);
            this.Name = this.adfModel.Name;
            this.Path = this.Name;
        }

        public override void Compile(
            AlertCollector alerts)
        {
            base.Compile(alerts);

            JToken activitiesToken = this.AdfResourceToken.SelectToken("$.properties.activities");

            if (activitiesToken != null && activitiesToken.Type == JTokenType.Array)
            {
                JArray activities = (JArray)activitiesToken;
                foreach (var activity in activities)
                {
                    Upgrader activityUpgrader = ActivityUpgrader.CreateActivityUpgrader(this.Name, activity, this.Machine);
                    activityUpgrader.Compile(alerts);
                    this.activityUpgraders.Add(activityUpgrader);
                }
            }
        }

        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            foreach (Upgrader activityUpgrader in this.activityUpgraders)
            {
                activityUpgrader.PreLink(allUpgraders, alerts);
                this.DependsOn.AddRange(activityUpgrader.DependsOn);
            }
        }

        /// <inheritdoc/>
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            
            if (symbolName == Symbol.CommonNames.FabricResource)
            {
                PipelineExportInstruction exportInstruction = new PipelineExportInstruction(this.Name);

                this.AddExportLinks(exportInstruction, alerts);

                this.AddExportResolves(exportInstruction, alerts);

                FabricPipelineModel pipeline = new FabricPipelineModel()
                {
                    Name = this.adfModel.Name,
                    Properties = new FabricPipelineProperties()
                    {
                        Activities = new List<JObject>(),
                        Concurrency = this.adfModel.Properties.Concurrency,
                        Parameters = this.adfModel.Properties.Parameters,
                        Variables = this.adfModel.Properties.Variables,
                    },
                };

                this.AddActivities(pipeline, alerts);

                exportInstruction.Export = pipeline.ToJObject();

                return Symbol.ReadySymbol(exportInstruction.ToJObject());
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
        }

        private void AddExportLinks(
            PipelineExportInstruction exportInstruction,
            AlertCollector alerts)
        {
            int nActivity = 0;
            foreach (Upgrader activityUpgrader in this.activityUpgraders)
            {
                Symbol resolutionSymbol = activityUpgrader.ResolveExportedSymbol(Symbol.CommonNames.ExportLinks, alerts);
                if (resolutionSymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                if (resolutionSymbol.Value != null)
                {
                    foreach (JToken requiredLink in (JArray)resolutionSymbol.Value)
                    {
                        FabricExportLink link = FabricExportLink.FromJToken(requiredLink);
                        link.TargetPath = $"properties.activities[{nActivity}].{link.TargetPath}";
                        exportInstruction.Links.Add(link);
                    }
                }

                nActivity++;
            }
        }

        private void AddExportResolves(
            PipelineExportInstruction exportInstruction,
            AlertCollector alerts)
        {
            int nActivity = 0;
            foreach (Upgrader activityUpgrader in this.activityUpgraders)
            {
                Symbol resolutionSymbol = activityUpgrader.ResolveExportedSymbol(Symbol.CommonNames.ExportResolves, alerts);
                if (resolutionSymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                if (resolutionSymbol.Value != null)
                {
                    foreach (JToken requiredResolution in (JArray)resolutionSymbol.Value)
                    {
                        FabricExportResolve resolve = FabricExportResolve.FromJToken(requiredResolution);
                        resolve.TargetPath = $"properties.activities[{nActivity}].{resolve.TargetPath}";
                        exportInstruction.Resolves.Add(resolve);
                    }
                }

                nActivity++;
            }
        }

        private void AddActivities(
            FabricPipelineModel pipeline,
            AlertCollector alerts)
        {
            foreach (Upgrader activityUpgrader in this.activityUpgraders)
            {
                Symbol activitySymbol = activityUpgrader.ResolveExportedSymbol(Symbol.CommonNames.Activity, alerts);
                if (activitySymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }
                pipeline.Properties.Activities.Add((JObject)activitySymbol.Value);
            }
        }

        protected class AdfPipelineModel
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "description")]
            public string Description { get; set; }

            [JsonProperty(PropertyName = "properties", Order = 2)]
            public FabricPipelineProperties Properties { get; set; }

            public static AdfPipelineModel FromJToken(JToken jToken)
            {
                return UpgradeSerialization.FromJToken<AdfPipelineModel>(jToken);
            }
        }

        /// <summary>
        /// The Fabric model of a Pipeline.
        /// </summary>
        public class FabricPipelineModel
        {
            [JsonProperty(PropertyName = "name", Order = 1)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "properties", Order = 2)]
            public FabricPipelineProperties Properties { get; set; }

            [JsonProperty(PropertyName = "annotations", Order = 3)]
            public JArray Annotations { get; set; } = new JArray();

            public JObject ToJObject()
            {
                return (JObject)UpgradeSerialization.ToJToken(this);
            }
        }

        public class FabricPipelineProperties
        {
            [JsonProperty(PropertyName = "activities")]
            public IList<JObject> Activities { get; set; } = new List<JObject>();

            [JsonProperty(PropertyName = "concurrency")]
            public int? Concurrency { get; set; }

            [JsonProperty(PropertyName = "parameters")]
            public JToken Parameters { get; set; }

            [JsonProperty(PropertyName = "variables")]
            public JObject Variables { get; set; }
        }
    }
}

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
            this.UpgraderType = Upgrader.Type.DataPipeline;
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
                    //this.AddDagEdgeTo(activityUpgrader);
                    activityUpgrader.Compile(alerts);
                    this.activityUpgraders.Add(activityUpgrader);
                }
            }
        }
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == "name")
            {
                string name = this.AdfResourceToken.SelectToken("$.name")?.ToString();
                return Symbol.ReadySymbol(name);
            }
            if (symbolName == "pipeline")
            {
                FabricPipelineModel fabricPipeline = new FabricPipelineModel()
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

                foreach (Upgrader activityUpgrader in this.activityUpgraders)
                {
                    Symbol activitySymbol = activityUpgrader.ResolveExportedSymbol("activity", alerts);
                    if (activitySymbol.State != Symbol.SymbolState.Ready)
                    {
                        // TODO!
                    }
                    fabricPipeline.Properties.Activities.Add((JObject)activitySymbol.Value);
                }

                return Symbol.ReadySymbol(fabricPipeline.ToJObject());
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
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

// <copyright file="PipelineUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders
{
    /// <summary>
    /// This class upgrades an ADF Pipeline to a Fabric Pipeline.
    /// </summary>

    public class PipelineUpgrader : Upgrader
    {
        protected const string AdfActivityPath = "properties.activities";
        protected const string FabricActivityPath = "properties.activities";

        protected AdfPipelineModel adfModel;

        // These are the Upgraders for all the Activities in the Pipeline.
        // Each of them needs to be upgraded.
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

        /// <inheritdoc/>
        public override void Compile(
            AlertCollector alerts)
        {
            base.Compile(alerts);

            JToken activitiesToken = this.AdfResourceToken.SelectToken(AdfActivityPath);

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

        /// <inheritdoc/>
        public override void PreSort(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            foreach (Upgrader activityUpgrader in this.activityUpgraders)
            {
                activityUpgrader.PreSort(allUpgraders, alerts);

                // Used in Sort().
                this.DependsOn.AddRange(activityUpgrader.DependsOn);
            }
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportInstructions)
            {
                PipelineExportInstruction exportInstruction = new PipelineExportInstruction(this.Name, this.adfModel.Description);

                this.CollectActivityExportResolves(exportInstruction, alerts);

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

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <summary>
        /// Collect the ExportResolves from all of the Activities and add them to this Pipeline's ExportResolves.
        /// </summary>
        /// <param name="exportInstruction">Where to put the ExportResolves.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void CollectActivityExportResolves(
            PipelineExportInstruction exportInstruction,
            AlertCollector alerts)
        {
            int nActivity = 0;
            foreach (Upgrader activityUpgrader in this.activityUpgraders)
            {
                Symbol resolutionSymbol = activityUpgrader.EvaluateSymbol(Symbol.CommonNames.ExportResolveSteps, alerts);
                if (resolutionSymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                if (resolutionSymbol.Value != null)
                {
                    foreach (JToken requiredResolution in (JArray)resolutionSymbol.Value)
                    {
                        FabricExportResolveStep resolve = FabricExportResolveStep.FromJToken(requiredResolution);
                        resolve.TargetPath = $"properties.activities[{nActivity}].{resolve.TargetPath}";
                        exportInstruction.Resolves.Add(resolve);
                    }
                }

                nActivity++;
            }
        }

        /// <summary>
        /// Collect all of the upgrade from all of the Activities and add them to the exported Fabric Pipeline.
        /// </summary>
        /// <param name="pipeline">The Fabric Pipeline into which to insert the Activity.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void AddActivities(
            FabricPipelineModel pipeline,
            AlertCollector alerts)
        {
            foreach (Upgrader activityUpgrader in this.activityUpgraders)
            {
                Symbol activitySymbol = activityUpgrader.EvaluateSymbol(Symbol.CommonNames.Activity, alerts);
                if (activitySymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }
                pipeline.Properties.Activities.Add((JObject)activitySymbol.Value);
            }
        }

        /// <summary>
        /// A model of an ADF Pipeline.
        /// </summary>
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
        /// A model of a Fabric Pipeline.
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

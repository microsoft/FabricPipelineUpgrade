// <copyright file="ActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// The base class for all Activity Upgraders.
    /// </summary>
    public class ActivityUpgrader : Upgrader
    {
        public class ActivityTypes
        {
            public const string Copy = "Copy";
            public const string ExecutePipeline = "ExecutePipeline";
            public const string If = "IfCondition";
            public const string SetVariable = "SetVariable";
            public const string Wait = "Wait";
            public const string Web = "WebActivity";

            // The ADF ExecutePipeline becomes a Fabric InvokePipeline.
            public const string InvokePipeline = "InvokePipeline";
        }

        protected ActivityUpgrader(
            string activityType,
            string parentPath,
            JToken adfActivityToken,
            IFabricUpgradeMachine machine)
            : base(adfActivityToken, machine)
        {
            this.ActivityType = activityType;
            this.AdfBaseModel = AdfBaseActivityModel.Build(adfActivityToken);
            this.Name = AdfBaseModel.Name;
            this.UpgraderType = FabricUpgradeResourceTypes.PipelineActivity;
            this.Path = parentPath + "/" + this.Name;
        }

        // The model of the common properties of all ADF Activities.
        protected AdfBaseActivityModel AdfBaseModel { get; set; }

        // The Type of the activity, like 'Copy'.
        protected string ActivityType { get; set; }

        /// <summary>
        /// A "factory" function that creates the appropriate Upgrader from the ADF Activity's Type.
        /// </summary>
        /// <param name="parentPath">The 'path' to the parent object.</param>
        /// <param name="adfActivityToken">The JObject that describes the ADF Activity.</param>
        /// <param name="machine">The FabricUpgradeMachine that provides utilities to Upgraders.</param>
        /// <returns>A new Upgrader for that Activity type.</returns>
        public static ActivityUpgrader CreateActivityUpgrader(
            string parentPath,
            JToken adfActivityToken,
            IFabricUpgradeMachine machine)
        {
            string activityType = AdfBaseActivityModel.Build(adfActivityToken).ActivityType;
            return activityType switch
            {
                ActivityTypes.Copy => new CopyActivityUpgrader(parentPath, adfActivityToken, machine),
                ActivityTypes.ExecutePipeline => new ExecutePipelineActivityUpgrader(parentPath, adfActivityToken, machine),
                ActivityTypes.If => new IfConditionActivityUpgrader(parentPath, adfActivityToken, machine),
                ActivityTypes.Wait => new WaitActivityUpgrader(parentPath, adfActivityToken, machine),
                ActivityTypes.Web => new WebActivityUpgrader(parentPath, adfActivityToken, machine),
                ActivityTypes.SetVariable => new SetVariableActivityUpgrader(parentPath, adfActivityToken, machine),
                _ => new UnsupportedActivityUpgrader(parentPath, adfActivityToken, machine),
            };
        }

        // <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
        }

        // <inheritdoc/>
        public override void PreSort(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreSort(allUpgraders, alerts);
        }

        // <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.Activity)
            {
                // Each individual Activity Upgrader requests this Symbol
                // in order to start building its particular "activity" Symbol.

                return this.BuildCommonActivitySymbol(alerts);
            }

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <summary>
        /// Create a Symbol whose Value is a JObject that contains all of the 
        /// common activity properties.
        /// </summary>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns></returns>
        private Symbol BuildCommonActivitySymbol(
            AlertCollector alerts)
        {
            // Create a Symbol whose Value is a JObject that contains all of the 
            // common activity properties.

            FabricBaseActivityModel fabricModel = new FabricBaseActivityModel()
            {
                Name = this.Name,
                ActivityType = this.ActivityType,
                Description = this.AdfBaseModel.Description,
                DependsOn = this.AdfBaseModel.DependsOn,
                State = this.AdfBaseModel.State,
                OnInactiveMarkAs = this.AdfBaseModel.OnInactiveMarkAs,
                UserProperties = this.AdfBaseModel.UserProperties,
            };

            return Symbol.ReadySymbol(fabricModel.ToJToken());
        }

        /// <summary>
        /// Build the ExportResolves Symbol whose value will be included in this Activity's Pipeline's ExportResolveSteps.
        /// </summary>
        /// <remarks>
        /// Before this Activity's Pipeline can be exported, we need to populate the IDs of 
        /// this Activities dependencies (Connections used by this Activity's Datasets, or
        /// other Pipelines).
        /// Collect these dependencies now so that the Pipeline can fill out its 'resolve' field.
        /// </remarks>
        /// <param name="parameterAssignments">The parameters from the caller.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>The ExportResolveSteps Symbol whose value is added to the Pipeline's ExportResolveSteps.</returns>
        protected virtual Symbol BuildExportResolveStepsSymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            // For Activities with no ExportResolves, return null.
            return Symbol.ReadySymbol(null);
        }

        /// <summary>
        /// Build the Activity Symbol whose value will be included in this Activity's Pipeline.
        /// </summary>
        /// <param name="parameterAssignments">The parameters from the caller.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>The Activity Symbol whose value is added to the Pipeline's Activities.</returns>
        protected virtual Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            // Each Activity should override this.
            return Symbol.ReadySymbol(null);
        }

        /// <summary>
        /// The ADF Model for the common properties of all ADF Activities.
        /// </summary>
        protected class AdfBaseActivityModel
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "type")]
            public string ActivityType { get; set; }

            [JsonProperty(PropertyName = "description")]
            public string Description { get; set; }

            [JsonProperty(PropertyName = "dependsOn")]
            public List<ActivityDependency> DependsOn { get; set; } = new List<ActivityDependency>();

            [JsonProperty(PropertyName = "state")]
            public string State { get; set; }

            [JsonProperty(PropertyName = "onInactiveMarkAs")]
            public string OnInactiveMarkAs { get; set; }

            // This does not appear to be used in Fabric, but we can include it in the activity.
            [JsonProperty(PropertyName = "userProperties")]
            public List<UserProperty> UserProperties { get; set; } = new List<UserProperty>();

            public static AdfBaseActivityModel Build(JToken activityToken)
            {
                return UpgradeSerialization.FromJToken<AdfBaseActivityModel>(activityToken);
            }
        }

        /// <summary>
        /// The Fabric Model for the common properties of all Fabric Activities.
        /// </summary>
        protected class FabricBaseActivityModel
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "type")]
            public string ActivityType { get; set; }

            [JsonProperty(PropertyName = "description")]
            public string Description { get; set; }

            [JsonProperty(PropertyName = "dependsOn")]
            public List<ActivityDependency> DependsOn { get; set; } = new List<ActivityDependency>();

            [JsonProperty(PropertyName = "state")]
            public string State { get; set; }

            [JsonProperty(PropertyName = "onInactiveMarkAs")]
            public string OnInactiveMarkAs { get; set; }

            // This does not appear to be used in Fabric, but we can include it in the activity.
            [JsonProperty(PropertyName = "userProperties")]
            public List<UserProperty> UserProperties { get; set; } = new List<UserProperty>();

            public JToken ToJToken()
            {
                return UpgradeSerialization.ToJToken(this);
            }
        }

        protected class ActivityDependency
        {
            [JsonProperty(PropertyName = "activity")]
            public string Activity { get; set; }

            [JsonProperty(PropertyName = "dependencyConditions")]
            public IList<string> DependencyConditions { get; set; }
        }

        protected class UserProperty
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "value")]
            public JToken Value { get; set; }
        }

    }
}

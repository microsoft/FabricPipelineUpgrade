using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    public class ActivityUpgrader : Upgrader
    {
        public class ActivityTypes
        {
            public const string ExecutePipeline = "ExecutePipeline";
            public const string WaitActivity = "Wait";
        }

        protected ActivityUpgrader(
            string activityType,
            string parentPath,
            JToken adfActivityToken,
            IFabricUpgradeMachine machine)
            : base(adfActivityToken, machine)
        {
            this.ActivityType = activityType;
            this.AdfModel = AdfPipelineActivityModel.Build(adfActivityToken);
            this.Name = AdfModel.Name;
            this.UpgraderType = FabricUpgradeResourceTypes.PipelineActivity;
            this.Path = parentPath + "/" + this.Name;
        }

        protected AdfPipelineActivityModel AdfModel { get; set; }

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
            string activityType = AdfPipelineActivityModel.Build(adfActivityToken).ActivityType;
            return activityType switch
            {
                ActivityTypes.ExecutePipeline => new ExecutePipelineActivityUpgrader(parentPath, adfActivityToken, machine),
                ActivityTypes.WaitActivity => new WaitActivityUpgrader(parentPath, adfActivityToken, machine),
                _ => new UnsupportedActivityUpgrader(parentPath, adfActivityToken, machine),
            };
        }

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
        }

        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreLink(allUpgraders, alerts);
        }

        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == "activity.common")
            {
                // Create a Symbol whose Value is a JObject that contains all of the 
                // common activity properties.

                FabricPipelineActivityModel fabricModel = new FabricPipelineActivityModel()
                {
                    Name = this.Name,
                    ActivityType = this.ActivityType,
                    Description = this.AdfModel.Description,
                    DependsOn = this.AdfModel.DependsOn,
                    State = this.AdfModel.State,
                    OnInactiveMarkAs = this.AdfModel.OnInactiveMarkAs,
                    UserProperties = this.AdfModel.UserProperties,
                };

                return Symbol.ReadySymbol(fabricModel.ToJToken());

            }
            return base.ResolveExportedSymbol(symbolName, alerts);
        }

        /// <summary>
        /// The ADF Model for a Pipeline Activity.
        /// </summary>
        protected class AdfPipelineActivityModel
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

            public static AdfPipelineActivityModel Build(JToken activityToken)
            {
                return UpgradeSerialization.FromJToken<AdfPipelineActivityModel>(activityToken);
            }
        }

        /// <summary>
        /// The Fabric Model for a Pipeline Activity.
        /// </summary>
        protected class FabricPipelineActivityModel
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

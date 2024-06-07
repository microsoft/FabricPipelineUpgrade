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
            public const string WaitActivity = "Wait";
        }

        protected ActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(activityToken, machine)
        {
            this.AdfActivityModel = AdfPipelineActivityModel.Build(activityToken);
            this.Name = AdfActivityModel.Name;
            this.UpgraderType = Upgrader.Type.PipelineActivity;
            this.Path = parentPath + "/" + this.Name;
        }

        protected AdfPipelineActivityModel AdfActivityModel { get; set; }
        protected string ActivityType { get; set; }

        /// <summary>
        /// A "factory" function that creates the appropriate Upgrader from the ADF Activity's Type.
        /// </summary>
        /// <param name="parentPath">The 'path' to the parent object.</param>
        /// <param name="activityObject">The JObject that describes the ADF Activity.</param>
        /// <param name="machine">The FabricUpgradeMachine that provides utilities to Upgraders.</param>
        /// <returns>A new Upgrader for that Activity type.</returns>
        public static ActivityUpgrader CreateActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
        {
            return AdfPipelineActivityModel.Build(activityToken).ActivityType switch
            {
                ActivityTypes.WaitActivity => new WaitActivityUpgrader(parentPath, activityToken, machine),
                _ => new UnsupportedActivityUpgrader(parentPath, activityToken, machine),
            };
        }

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
        }

        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == "activity")
            {
                FabricPipelineActivityModel fabricModel = new FabricPipelineActivityModel()
                {
                    Name = this.Name,
                    ActivityType = this.ActivityType,
                    Description = this.AdfActivityModel.Description,
                    DependsOn = this.AdfActivityModel.DependsOn,
                    State = this.AdfActivityModel.State,
                    OnInactiveMarkAs = this.AdfActivityModel.OnInactiveMarkAs,
                    UserProperties = this.AdfActivityModel.UserProperties,
                };

                return Symbol.ReadySymbol(fabricModel.ToJToken());

            }
            return Symbol.MissingSymbol();
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
        /// The ADF Model for a Pipeline Activity.
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

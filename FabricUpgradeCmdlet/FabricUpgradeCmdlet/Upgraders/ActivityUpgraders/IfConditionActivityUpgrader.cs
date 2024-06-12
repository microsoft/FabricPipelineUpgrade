using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    public class IfConditionActivityUpgrader : ActivityWithSubActivitiesUpgrader
    {
        private readonly List<string> requiredAdfProperties = new List<string>
        {
            "typeProperties.expression"
        };

        private readonly Dictionary<string, List<Upgrader>> subActivityUpgraders = new Dictionary<string, List<Upgrader>>();

        public IfConditionActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.WaitActivity, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            this.subActivityUpgraders["false"] = this.CompileSubActivities("typeProperties.ifFalseActivities", alerts);
            this.subActivityUpgraders["true"] = this.CompileSubActivities("typeProperties.ifTrueActivities", alerts);
        }

        /// <inheritdoc/>
        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            this.PreLinkSubActivities(this.subActivityUpgraders["false"], allUpgraders, alerts);
            this.PreLinkSubActivities(this.subActivityUpgraders["true"], allUpgraders, alerts);
        }

        /// <inheritdoc/>
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == "exportLinks")
            {
                List<FabricExportLink> links = new List<FabricExportLink>();

                this.CollectSubActivityExportLinks(this.subActivityUpgraders["false"], "ifFalseActivities", links, alerts);
                this.CollectSubActivityExportLinks(this.subActivityUpgraders["true"], "ifTrueActivities", links, alerts);

                return Symbol.ReadySymbol(JArray.Parse(JsonConvert.SerializeObject(links)));
            }

            if (symbolName == "exportResolves")
            {
                List<FabricExportResolve> resolves = new List<FabricExportResolve>();

                this.CollectSubActivityExportResolves(this.subActivityUpgraders["false"], "ifFalseActivities", resolves, alerts);
                this.CollectSubActivityExportResolves(this.subActivityUpgraders["true"], "ifTrueActivities", resolves, alerts);

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

                copier.Copy("typeProperties.expression");

                copier.Set("typeProperties.ifFalseActivities", CollectSubActivities(this.subActivityUpgraders["false"], alerts));
                copier.Set("typeProperties.ifTrueActivities", CollectSubActivities(this.subActivityUpgraders["true"], alerts));

                return Symbol.ReadySymbol(fabricActivityObject);
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
        }

        private List<Upgrader> CompileSubActivities(
            string path,
            AlertCollector alerts
            )
        {
            List<Upgrader> upgraders = new List<Upgrader>();

            JToken subActivitiesToken = this.AdfResourceToken.SelectToken(path);
            if (subActivitiesToken != null && subActivitiesToken.Type == JTokenType.Array)
            {
                JArray activities = (JArray)subActivitiesToken;
                foreach (var activity in activities)
                {
                    Upgrader activityUpgrader = ActivityUpgrader.CreateActivityUpgrader(this.Name, activity, this.Machine);
                    activityUpgrader.Compile(alerts);
                    upgraders.Add(activityUpgrader);
                }
            }

            return upgraders;
        }
    }
}

// <copyright file="IfConditionActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// This class Upgrades an ADF IfCondition Activity to a Fabric IfCondition Activity.
    /// </summary>
    public class IfConditionActivityUpgrader : ActivityWithSubActivitiesUpgrader
    {
        private const string adfExpressionPath = "typeProperties.expression";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            adfExpressionPath
        };

        // TODO: This might move up to ActivityWithSubActivitiesUpgrader.
        private readonly Dictionary<string, List<Upgrader>> subActivityUpgraders = new Dictionary<string, List<Upgrader>>();

        public IfConditionActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.If, parentPath, activityToken, machine)
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
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
            {
                return this.BuildExportResolveStepsSymbol(parameters, alerts);
            }

            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameters, alerts);
            }

            return base.EvaluateSymbol(symbolName, parameters, alerts);
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

        /// <inheritdoc/>
        protected override Symbol BuildExportResolveStepsSymbol(
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            List<FabricExportResolveStep> resolves = new List<FabricExportResolveStep>();

            this.CollectSubActivityExportResolveSteps(this.subActivityUpgraders["false"], "ifFalseActivities", resolves, alerts);
            this.CollectSubActivityExportResolveSteps(this.subActivityUpgraders["true"], "ifTrueActivities", resolves, alerts);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            Symbol activitySymbol = base.EvaluateSymbol(Symbol.CommonNames.Activity, parameters, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);
            copier.Copy("description");

            copier.Copy(adfExpressionPath);

            copier.Set("typeProperties.ifFalseActivities", CollectSubActivities(this.subActivityUpgraders["false"], alerts));
            copier.Set("typeProperties.ifTrueActivities", CollectSubActivities(this.subActivityUpgraders["true"], alerts));

            return Symbol.ReadySymbol(fabricActivityObject);
        }



    }
}

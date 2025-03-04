using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// This class upgrades an ADF ForEach activity to a Fabric ForEach activity.
    /// </summary>
    public class ForEachActivityUpgrader : ActivityWithSubActivitiesUpgrader
    {
        // ADF property paths for the ForEach activity.
        private const string adfItemsPath = "typeProperties.items";
        private const string adfActivitiesPath = "typeProperties.activities";
        private const string adfIsSequentialPath = "typeProperties.isSequential";

        // Required properties for a ForEach activity.
        private readonly List<string> requiredAdfProperties = new List<string>
        {
            adfItemsPath,
            adfActivitiesPath
        };

        // List to hold the compiled sub-activity upgraders.
        private readonly List<Upgrader> subActivityUpgraders = new List<Upgrader>();

        public ForEachActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.ForEach, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            // Verify that required properties exist.
            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            // Compile sub-activities defined in "typeProperties.activities".
            JToken subActivitiesToken = this.AdfResourceToken.SelectToken(adfActivitiesPath);
            if (subActivitiesToken != null && subActivitiesToken.Type == JTokenType.Array)
            {
                JArray activities = (JArray)subActivitiesToken;
                foreach (var activity in activities)
                {
                    Upgrader activityUpgrader = ActivityUpgrader.CreateActivityUpgrader(this.Name, activity, this.Machine);
                    activityUpgrader.Compile(alerts);
                    this.subActivityUpgraders.Add(activityUpgrader);
                }
            }
        }

        /// <inheritdoc/>
        public override void PreSort(List<Upgrader> allUpgraders, AlertCollector alerts)
        {
            // Pre-sort the sub-activities if needed.
            this.PreSortSubActivities(this.subActivityUpgraders, allUpgraders, alerts);
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
            {
                return this.BuildExportResolveStepsSymbol(parameterAssignments, alerts);
            }

            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameterAssignments, alerts);
            }

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildExportResolveStepsSymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            List<FabricExportResolveStep> resolves = new List<FabricExportResolveStep>();

            // Collect export resolve steps from the sub-activities.
            this.CollectSubActivityExportResolveSteps(this.subActivityUpgraders, "activities", resolves, alerts);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            // First, get the base activity symbol.
            Symbol activitySymbol = base.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO: Handle cases when the base activity symbol isn't ready.
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;

            // Copy properties from the original ADF activity to the Fabric activity.
            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);
            copier.Copy("description");
            copier.Copy(adfItemsPath);
            copier.Copy(adfIsSequentialPath);

            // Set the upgraded sub-activities.
            copier.Set(adfActivitiesPath, CollectSubActivities(this.subActivityUpgraders, alerts));

            return Symbol.ReadySymbol(fabricActivityObject);
        }

        /// <summary>
        /// Helper method to collect upgraded sub-activities into a JArray.
        /// </summary>
        /// <param name="upgraders">The list of sub-activity upgraders.</param>
        /// <param name="alerts">Alert collector for warnings or errors.</param>
        /// <returns>A JArray of upgraded sub-activities.</returns>
        private static JArray CollectSubActivities(List<Upgrader> upgraders, AlertCollector alerts)
        {
            JArray activitiesArray = new JArray();
            foreach (var upgrader in upgraders)
            {
                Symbol subActivitySymbol = upgrader.EvaluateSymbol(Symbol.CommonNames.Activity, new Dictionary<string, JToken>(), alerts);
                if (subActivitySymbol.State == Symbol.SymbolState.Ready)
                {
                    activitiesArray.Add(subActivitySymbol.Value);
                }
            }

            return activitiesArray;
        }
    }
}

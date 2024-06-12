using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// This is a base class for activites that include subactivites.
    /// </summary>
    /// <remarks>
    /// If, Switch, ForEach, Until, etc., include subactivities.
    /// The IfConditionActivityUpgrader shows how to use the methods in this class.
    /// </remarks>
    public class ActivityWithSubActivitiesUpgrader : ActivityUpgrader
    {
        public ActivityWithSubActivitiesUpgrader(
            string activityType,
            string parentPath,
            JToken adfActivityToken,
            IFabricUpgradeMachine machine)
            : base(activityType, parentPath, adfActivityToken, machine)
        {
        }

        /// <summary>
        /// PreLink a group of subactivities.
        /// </summary>
        /// <param name="upgraders">The group of subactivites to PreLink.</param>
        /// <param name="allUpgraders">All the upgraders.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        protected void PreLinkSubActivities(
            List<Upgrader> upgraders,
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            foreach (Upgrader activityUpgrader in upgraders)
            {
                activityUpgrader.PreLink(allUpgraders, alerts);
                this.DependsOn.AddRange(activityUpgrader.DependsOn);
            }
        }

        protected void CollectSubActivityExportLinks(
            List<Upgrader> upgraders,
            string activityGroup,
            List<FabricExportLink> links,
            AlertCollector alerts)
        {
            int nActivity = 0;
            foreach (Upgrader activityUpgrader in upgraders)
            {
                Symbol resolutionSymbol = activityUpgrader.ResolveExportedSymbol(Symbol.CommonNames.ExportLinks, alerts);
                if (resolutionSymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                if (resolutionSymbol.Value != null)
                {
                    foreach (JToken requiredResolution in (JArray)resolutionSymbol.Value)
                    {
                        FabricExportLink link = FabricExportLink.FromJToken(requiredResolution);
                        link.TargetPath = $"typeProperties.{activityGroup}[{nActivity}].{link.TargetPath}";
                        links.Add(link);
                    }
                }

                nActivity++;
            }
        }

        protected void CollectSubActivityExportResolves(
            List<Upgrader> upgraders,
            string activityGroup,
            List<FabricExportResolve> resolves,
            AlertCollector alerts)
        {
            int nActivity = 0;
            foreach (Upgrader activityUpgrader in upgraders)
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
                        resolve.TargetPath = $"typeProperties.{activityGroup}[{nActivity}].{resolve.TargetPath}";
                        resolves.Add(resolve);
                    }
                }

                nActivity++;
            }
        }

        protected JArray CollectSubActivities(
            List<Upgrader> upgraders,
            AlertCollector alerts)
        {
            JArray activityArray = new JArray();

            foreach (Upgrader upgrader in upgraders)
            {
                Symbol subActivitySymbol = upgrader.ResolveExportedSymbol(Symbol.CommonNames.Activity, alerts);
                if (subActivitySymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                activityArray.Add(subActivitySymbol.Value);
            }

            return activityArray;
        }
    }
}

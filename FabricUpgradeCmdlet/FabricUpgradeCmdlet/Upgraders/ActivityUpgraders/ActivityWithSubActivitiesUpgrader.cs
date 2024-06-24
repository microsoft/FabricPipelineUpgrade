// <copyright file="ActivityWithSubActivitiesUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

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
    ///
    /// The methods in this class describe a "group" of subactivities.
    /// For example, all of the Activities under "ifFalseActivities" is a "group" of subactivities.
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

        /// <summary>
        /// Ask each of the subactivities in a group for its exportResolveSteps symbol.
        /// </summary>
        /// <param name="upgraders">The Upgraders to query.</param>
        /// <param name="activityGroup">The JSON path to this group of subactivities.</param>
        /// <param name="resolves">Put the gathered exportResolves here.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        protected void CollectSubActivityExportResolveSteps(
            List<Upgrader> upgraders,
            string activityGroup,
            List<FabricExportResolveStep> resolves,
            AlertCollector alerts)
        {
            int nActivity = 0;
            foreach (Upgrader activityUpgrader in upgraders)
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
                        resolve.TargetPath = $"typeProperties.{activityGroup}[{nActivity}].{resolve.TargetPath}";
                        resolves.Add(resolve);
                    }
                }

                nActivity++;
            }
        }

        /// <summary>
        /// Query all of these Upgraders for their "activity" Symbol.
        /// </summary>
        /// <param name="upgraders">The Upgraders to query.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns></returns>
        protected JArray CollectSubActivities(
            List<Upgrader> upgraders,
            AlertCollector alerts)
        {
            JArray activityArray = new JArray();

            foreach (Upgrader upgrader in upgraders)
            {
                Symbol subActivitySymbol = upgrader.EvaluateSymbol(Symbol.CommonNames.Activity, alerts);
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

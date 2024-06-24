// <copyright file="UnsupportedActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// This class handles an unrecognized/unsupported Activity by replacing it with a "stub" Activity.
    /// </summary>
    public class UnsupportedActivityUpgrader : ActivityUpgrader
    {
        public UnsupportedActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.Wait, parentPath, activityToken, machine)
        {
            // This "stub" activity will be a Wait activity with waitTimeInSeconds = 0.
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameters, alerts);
            }

            return base.EvaluateSymbol(symbolName, parameters, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            // We "pretend" that this Activity is actually a Wait Activity with WaitTimeInSeconds = 0
            // and a description that contains useful information for the user.

            Symbol activitySymbol = base.EvaluateSymbol(Symbol.CommonNames.Activity, parameters, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;

            JToken newDescription = this.MakeDescriptionForUnsupportedActivity();

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);

            copier.Set("description", newDescription);
            copier.Set("typeProperties.waitTimeInSeconds", 0);

            // Notify the client that this Activity is unsupported and will be replaced with a supported Activity.
            alerts.AddWarning($"Cannot upgrade Activity '{this.Path}'; please inspect this Activity for more details");

            return Symbol.ReadySymbol(fabricActivityObject);
        }

        /// <summary>
        /// Add some useful information to the description.
        /// </summary>
        /// <returns>A new description.</returns>
        private string MakeDescriptionForUnsupportedActivity()
        {
            string originalType = this.AdfBaseModel.ActivityType;
            string originalDescription = this.AdfBaseModel.Description;
            string newDescription =
                $"Failed to upgrade activity '{this.Name}' because it has type '{originalType}'.\n" +
                $"To run this pipeline anyway, mark this Activity as 'Deactivated' and select the desired value for 'Mark activity as'.";

            if (!string.IsNullOrEmpty(originalDescription))
            {
                newDescription += $"\nOriginal description=\n{originalDescription}";
            }

            return newDescription;
        }
    }
}

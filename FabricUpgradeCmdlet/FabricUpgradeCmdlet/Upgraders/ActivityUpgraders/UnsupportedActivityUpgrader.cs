// <copyright file="UnsupportedActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    public class UnsupportedActivityUpgrader : ActivityUpgrader
    {
        public UnsupportedActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.Wait, parentPath, activityToken, machine)
        {
            // This "stub" activity will be a Wait activity with a waitTimeInSeconds of 0.
        }

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
        }

        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.Activity)
            {
                // We "pretend" that this Activity is actually a Wait Activity
                // with WaitTimeInSeconds = 0
                // and a description that contains useful information for the user.

                Symbol activitySymbol = base.ResolveExportedSymbol(Symbol.CommonNames.Activity, alerts);

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

            return base.ResolveExportedSymbol(symbolName, alerts);
        }

        private string MakeDescriptionForUnsupportedActivity()
        {
            string originalType = this.AdfModel.ActivityType;
            string originalDescription = this.AdfModel.Description;
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

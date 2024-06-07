using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    public class UnsupportedActivityUpgrader : ActivityUpgrader
    {
        public UnsupportedActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(parentPath, activityToken, machine)
        {
            // This "stub" activity will be a Wait activity with a waitTimeInSeconds of 0.
            this.ActivityType = ActivityUpgrader.ActivityTypes.WaitActivity;
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
                // We "pretend" that this Activity is actually a Wait Activity
                // with WaitTimeInSeconds = 0
                // and a description that contains useful information for the user.

                Symbol activitySymbol = base.ResolveExportedSymbol("activity", alerts);

                if (activitySymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                JObject fabricActivity = (JObject)activitySymbol.Value;

                JToken newDescription = this.ResolveExportedSymbol("newDescription", alerts).Value;

                this.Set(newDescription, fabricActivity, "description");
                this.Set(0, fabricActivity, "typeProperties.waitTimeInSeconds");

                // Notify the client that this Activity is unsupported and will be replaced with a supported Activity.
                alerts.AddWarning($"Cannot upgrade Activity '{this.Path}'; please inspect this Activity for more details");

                return Symbol.ReadySymbol(fabricActivity);
            }

            if (symbolName == "newDescription")
            {
                return Symbol.ReadySymbol(this.MakeDescriptionForUnsupportedActivity());
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
        }

        private string MakeDescriptionForUnsupportedActivity()
        {
            string originalType = this.AdfActivityModel.ActivityType;
            string originalDescription = this.AdfActivityModel.Description;
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

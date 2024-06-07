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
    public class WaitActivityUpgrader : ActivityUpgrader
    {
        public WaitActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(parentPath, activityToken, machine)
        {
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
                Symbol activitySymbol = base.ResolveExportedSymbol("activity", alerts);

                if (activitySymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                JObject fabricActivity = (JObject)activitySymbol.Value;

                this.Move(this.AdfResourceToken, "description", fabricActivity, "description");
                this.Move(this.AdfResourceToken, "typeProperties.waitTimeInSeconds", fabricActivity, "typeProperties.waitTimeInSeconds");

                return Symbol.ReadySymbol(fabricActivity);
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
        }
    }
}

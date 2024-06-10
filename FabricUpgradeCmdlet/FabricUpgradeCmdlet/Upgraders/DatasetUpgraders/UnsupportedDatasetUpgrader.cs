using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.DatasetUpgraders
{
    public class UnsupportedDatasetUpgrader : DatasetUpgrader
    {
        public UnsupportedDatasetUpgrader(
            JToken adfDatasetToken,
            IFabricUpgradeMachine machine)
            : base(adfDatasetToken, machine)
        {
        }

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            // Here, we add an Alert that will prevent the Upgrader from continuing.
            alerts.AddUnsupportedResourceAlert($"Cannot upgrade Dataset '{this.Path}' because its Type is '{this.AdfModel.Properties.DatasetType}'");
        }
    }
}

// <copyright file="TriggerUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.TriggerUpgraders
{
    /// <summary>
    /// Base class for all Trigger Upgraders.
    /// </summary>
    public class TriggerUpgrader : Upgrader
    {
        protected TriggerUpgrader(
            JToken triggerToken,
            IFabricUpgradeMachine machine)
            : base(triggerToken, machine)
        {
            AdfTriggerModel adfTriggerModel = AdfTriggerModel.Build(triggerToken);

            this.Name = adfTriggerModel.Name;
            this.UpgraderType = FabricUpgradeResourceTypes.Trigger;
            this.TriggerType = AdfTriggerModel.Build(triggerToken).Properties.TriggerType;
            this.Path = "Trigger " + this.Name;
        }

        protected string TriggerType { get; set; }

        /// <summary>
        /// Create the appropriate subclass of TriggerUpgrader, based on the type of the ADF Trigger.
        /// </summary>
        /// <param name="triggerToken">The JToken describing the ADF LinkedService.</param>
        /// <param name="machine">The FabricUpgradeMachine that provides utilities to Upgraders.</param>
        /// <returns>A DatasetUpgrader.</returns>
        public static TriggerUpgrader CreateTriggerUpgrader(
            JToken triggerToken,
            IFabricUpgradeMachine machine)
        {
            return new UnsupportedTriggerUpgrader(triggerToken, machine);
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <summary>
        /// The ADF Model for a Trigger.
        /// </summary>
        /// <remarks>
        /// This model includes only the properties of interest to the Upgrader.
        /// </remarks>
        protected class AdfTriggerModel
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "properties")]
            public AdfTriggerPropertiesModel Properties { get; set; }

            public static AdfTriggerModel Build(JToken triggerToken)
            {
                return UpgradeSerialization.FromJToken<AdfTriggerModel>(triggerToken);
            }
        }

        protected class AdfTriggerPropertiesModel
        {
            [JsonProperty(PropertyName = "type")]
            public string TriggerType { get; set; }
        }

    }
}

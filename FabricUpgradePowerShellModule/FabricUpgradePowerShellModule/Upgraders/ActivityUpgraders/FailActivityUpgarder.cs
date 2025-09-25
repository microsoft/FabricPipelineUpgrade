// <copyright file="FailActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    public class FailActivityUpgarder : ActivityUpgrader
    {
        public FailActivityUpgarder(
            string parentPath, 
            JToken adfActivityToken, 
            IFabricUpgradeMachine machine) : base(ActivityUpgrader.ActivityTypes.Fail, parentPath, adfActivityToken, machine)
        {
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
            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameterAssignments, alerts);
            }

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            Symbol activitySymbol = base.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);

            copier.Copy("description");
            copier.Copy("typeProperties.message", allowNull: false);
            copier.Copy("typeProperties.errorCode", allowNull: false);

            return Symbol.ReadySymbol(fabricActivityObject);
        }
    }
}

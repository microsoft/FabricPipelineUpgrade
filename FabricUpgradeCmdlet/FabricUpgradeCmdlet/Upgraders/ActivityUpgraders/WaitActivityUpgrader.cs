// <copyright file="WaitActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    public class WaitActivityUpgrader : ActivityUpgrader
    {
        public WaitActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.Wait, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
        }

        /// <inheritdoc/>
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameters, alerts);
            }

            return base.ResolveExportedSymbol(symbolName, parameters, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            Symbol activitySymbol = base.ResolveExportedSymbol(Symbol.CommonNames.Activity, parameters, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);

            copier.Copy("description");
            copier.Copy("typeProperties.waitTimeInSeconds", allowNull: false);

            return Symbol.ReadySymbol(fabricActivityObject);
        }
    }
}

// <copyright file="SetVariableActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;
using static FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders.ActivityUpgrader;
using System.Reflection.PortableExecutable;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// This class Upgrades an ADF SetVariable Activity to a Fabric SetVariable Activity.
    /// </summary>
    public class SetVariableActivityUpgrader : ActivityUpgrader
    {
        private const string adfVariableNamePath = "typeProperties.variableName";
        private const string adfPolicyPath = "policy";
        private const string adfValuePath = "typeProperties.value";
        private const string adfValueValuePath = "typeProperties.value.value";
        private const string adfSetSystemVariablePath = "typeProperties.setSystemVariable";
        private const string fabricValuePath = "typeProperties.value";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            adfVariableNamePath
        };

        public SetVariableActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.SetVariable, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);
        }

        /// <inheritdoc/>
        public override void PreSort(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
            {
                return this.BuildExportResolveStepsSymbol(parameterAssignments, alerts);
            }

            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameterAssignments, alerts);
            }

            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildExportResolveStepsSymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            List<FabricExportResolveStep> resolves = new List<FabricExportResolveStep>();
            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }

        /// <inheritdoc/>
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

            copier.Copy(adfPolicyPath);
            copier.Copy(adfVariableNamePath);

            JToken setSystemVariableToken = this.AdfResourceToken.SelectToken(adfSetSystemVariablePath);
            if (setSystemVariableToken != null)
            {
                copier.Copy(adfSetSystemVariablePath);
                copier.Copy(adfValuePath, fabricValuePath);
            }
            else
            {
                copier.Copy(adfValueValuePath, fabricValuePath);
            }

            return Symbol.ReadySymbol(fabricActivityObject);
        }



    }
}

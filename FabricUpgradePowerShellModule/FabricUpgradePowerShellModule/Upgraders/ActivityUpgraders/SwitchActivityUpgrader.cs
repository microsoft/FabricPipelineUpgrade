// <copyright file="SwitchActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using FabricUpgradePowerShellModule.Models;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// Minimal Switch activity upgrader.
    /// Assumes:
    /// - typeProperties.on exists or will be defaulted.
    /// - typeProperties.cases and typeProperties.defaultActivities are arrays (or a single object).
    /// </summary>
    public class SwitchActivityUpgrader : ActivityWithSubActivitiesUpgrader
    {
        private const string AdfOnPath = "typeProperties.on";
        private const string AdfCasesPath = "typeProperties.cases";
        private const string AdfDefaultActivitiesPath = "typeProperties.defaultActivities";

        private readonly List<SwitchCaseUpgrader> caseUpgraders = new List<SwitchCaseUpgrader>();
        private readonly List<Upgrader> defaultActivityUpgraders = new List<Upgrader>();

        public SwitchActivityUpgrader(string parentPath, JToken activityToken, IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.Switch, parentPath, activityToken, machine)
        {
        }

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            // Check required properties (on and cases).
            this.CheckRequiredAdfProperties(new List<string> { AdfOnPath, AdfCasesPath }, alerts);

            // Process cases.
            JToken casesToken = this.AdfResourceToken.SelectToken(AdfCasesPath);
            JArray casesArray = ConvertTokenToArray(casesToken);
            foreach (JToken caseToken in casesArray)
            {
                var scu = new SwitchCaseUpgrader(this.Path, caseToken, this.Machine);
                scu.Compile(alerts);
                this.caseUpgraders.Add(scu);
            }

            // Process defaultActivities.
            JToken defaultToken = this.AdfResourceToken.SelectToken(AdfDefaultActivitiesPath);
            JArray defaultArray = ConvertTokenToArray(defaultToken);
            foreach (JToken act in defaultArray)
            {
                Upgrader u = ActivityUpgrader.CreateActivityUpgrader(this.Name, act, this.Machine);
                u.Compile(alerts);
                this.defaultActivityUpgraders.Add(u);
            }
        }

        public override void PreSort(List<Upgrader> allUpgraders, AlertCollector alerts)
        {
            base.PreSort(allUpgraders, alerts);

            // PreSort all activities under cases.
            foreach (var caseUpgrader in this.caseUpgraders)
            {
                caseUpgrader.PreSort(allUpgraders, alerts);
                this.DependsOn.AddRange(caseUpgrader.DependsOn);
                foreach (var activityUpgrader in caseUpgrader.ActivityUpgraders)
                {
                    this.DependsOn.AddRange(activityUpgrader.DependsOn);
                }
            }

            // PreSort default activities.
            foreach (var defaultUpgrader in this.defaultActivityUpgraders)
            {
                defaultUpgrader.PreSort(allUpgraders, alerts);
                this.DependsOn.AddRange(defaultUpgrader.DependsOn);
            }
        }

        public override Symbol EvaluateSymbol(string symbolName, Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.Activity)
                return BuildActivitySymbol(parameterAssignments, alerts);
            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
                return BuildExportResolveStepsSymbol(parameterAssignments, alerts);
            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        private Symbol BuildExportResolveStepsSymbol(Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            List<FabricExportResolveStep> resolves = new List<FabricExportResolveStep>();

            // Gather resolves from case activities
            for (int caseIndex = 0; caseIndex < this.caseUpgraders.Count; caseIndex++)
            {
                var caseUpgrader = this.caseUpgraders[caseIndex];
                int activityIndex = 0;
                foreach (var actUpgrader in caseUpgrader.ActivityUpgraders)
                {
                    Symbol resSymbol = actUpgrader.EvaluateSymbol(Symbol.CommonNames.ExportResolveSteps, parameterAssignments, alerts);
                    if (resSymbol.State == Symbol.SymbolState.Ready && resSymbol.Value is JArray arr)
                    {
                        foreach (JToken r in arr)
                        {
                            FabricExportResolveStep step = FabricExportResolveStep.FromJToken(r);
                            step.TargetPath = $"typeProperties.cases[{caseIndex}].activities[{activityIndex}].{step.TargetPath}";
                            resolves.Add(step);
                        }
                    }
                    activityIndex++;
                }
            }

            // Gather resolves from default activities
            for (int i = 0; i < this.defaultActivityUpgraders.Count; i++)
            {
                var actUpgrader = this.defaultActivityUpgraders[i];
                Symbol resSymbol = actUpgrader.EvaluateSymbol(Symbol.CommonNames.ExportResolveSteps, parameterAssignments, alerts);
                if (resSymbol.State == Symbol.SymbolState.Ready && resSymbol.Value is JArray darr)
                {
                    foreach (JToken r in darr)
                    {
                        FabricExportResolveStep step = FabricExportResolveStep.FromJToken(r);
                        step.TargetPath = $"typeProperties.defaultActivities[{i}].{step.TargetPath}";
                        resolves.Add(step);
                    }
                }
            }

            if (resolves.Count == 0)
            {
                return Symbol.ReadySymbol(null);
            }

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }

        protected override Symbol BuildActivitySymbol(Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            // Get the base Fabric activity JSON.
            Symbol baseSymbol = base.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);
            if (baseSymbol.State != Symbol.SymbolState.Ready)
            {
                return baseSymbol;
            }

            JObject fabricActivity = baseSymbol.Value as JObject ?? new JObject();

            // Ensure typeProperties exists.
            if (fabricActivity["typeProperties"] == null || fabricActivity["typeProperties"].Type != JTokenType.Object)
            {
                fabricActivity["typeProperties"] = new JObject();
            }
            JObject typeProps = (JObject)fabricActivity["typeProperties"];

            // Handle the switch expression ("on"). If missing, set a default.
            JToken onToken = this.AdfResourceToken.SelectToken(AdfOnPath);
            if (onToken == null || onToken.Type == JTokenType.Null)
            {
                typeProps["on"] = new JObject { ["value"] = "'Full'", ["type"] = "Expression" };
            }
            else
            {
                typeProps["on"] = onToken.DeepClone();
            }

            // Build new cases array.
            JArray newCases = new JArray();
            foreach (var scu in this.caseUpgraders)
            {
                Symbol caseSymbol = scu.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);
                if (caseSymbol.State == Symbol.SymbolState.Ready && caseSymbol.Value != null)
                    newCases.Add(caseSymbol.Value);
            }
            typeProps["cases"] = newCases;

            // Build new defaultActivities array.
            JArray newDefaults = new JArray();
            foreach (var ua in this.defaultActivityUpgraders)
            {
                Symbol defSymbol = ua.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);
                if (defSymbol.State == Symbol.SymbolState.Ready && defSymbol.Value != null)
                    newDefaults.Add(defSymbol.Value);
            }
            typeProps["defaultActivities"] = newDefaults;

            return Symbol.ReadySymbol(fabricActivity);
        }

        private static JArray ConvertTokenToArray(JToken token)
        {
            if (token == null)
                return new JArray();
            if (token.Type == JTokenType.Array)
                return (JArray)token;
            if (token.Type == JTokenType.Object)
                return new JArray(token);
            return new JArray();
        }
    }

    public class SwitchCaseUpgrader : Upgrader
    {
        private const string AdfCaseValuePath = "value";
        private const string AdfCaseActivitiesPath = "activities";

        private readonly List<Upgrader> activityUpgraders = new List<Upgrader>();

        public IEnumerable<Upgrader> ActivityUpgraders => this.activityUpgraders;

        public SwitchCaseUpgrader(string parentPath, JToken caseToken, IFabricUpgradeMachine machine)
            : base(caseToken, machine)
        {
            this.Path = parentPath;
        }

        public override void Compile(AlertCollector alerts)
        {
            if (this.AdfResourceToken.SelectToken(AdfCaseValuePath) == null)
            {
                this.AdfResourceToken[AdfCaseValuePath] = JValue.CreateNull();
            }

            JToken actsToken = this.AdfResourceToken.SelectToken(AdfCaseActivitiesPath);
            JArray actsArray = ConvertTokenToArray(actsToken);
            foreach (JToken act in actsArray)
            {
                Upgrader u = ActivityUpgrader.CreateActivityUpgrader(this.Path, act, this.Machine);
                u.Compile(alerts);
                this.activityUpgraders.Add(u);
            }
        }

        public override void PreSort(List<Upgrader> allUpgraders, AlertCollector alerts)
        {
            foreach (var u in this.activityUpgraders)
            {
                u.PreSort(allUpgraders, alerts);
                this.DependsOn.AddRange(u.DependsOn);
            }
        }

        public override Symbol EvaluateSymbol(string symbolName, Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.Activity)
                return BuildCaseSymbol(parameterAssignments, alerts);
            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        private Symbol BuildCaseSymbol(Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            JObject caseObj = new JObject();
            JToken caseValue = this.AdfResourceToken.SelectToken(AdfCaseValuePath);
            caseObj["value"] = caseValue != null ? caseValue.DeepClone() : JValue.CreateNull();

            JArray newActs = new JArray();
            foreach (var u in this.activityUpgraders)
            {
                Symbol s = u.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);
                if (s.State == Symbol.SymbolState.Ready && s.Value != null)
                    newActs.Add(s.Value);
            }
            caseObj[AdfCaseActivitiesPath] = newActs;

            return Symbol.ReadySymbol(caseObj);
        }

        private static JArray ConvertTokenToArray(JToken token)
        {
            if (token == null)
                return new JArray();
            if (token.Type == JTokenType.Array)
                return (JArray)token;
            if (token.Type == JTokenType.Object)
                return new JArray(token);
            return new JArray();
        }
    }
}

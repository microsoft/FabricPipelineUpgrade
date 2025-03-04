using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;

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

        private readonly List<SwitchCaseUpgrader> _caseUpgraders = new List<SwitchCaseUpgrader>();
        private readonly List<Upgrader> _defaultActivityUpgraders = new List<Upgrader>();

        public SwitchActivityUpgrader(string parentPath, JToken activityToken, IFabricUpgradeMachine machine)
            : base("Switch", parentPath, activityToken, machine)
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
                _caseUpgraders.Add(scu);
            }

            // Process defaultActivities.
            JToken defaultToken = this.AdfResourceToken.SelectToken(AdfDefaultActivitiesPath);
            JArray defaultArray = ConvertTokenToArray(defaultToken);
            foreach (JToken act in defaultArray)
            {
                Upgrader u = ActivityUpgrader.CreateActivityUpgrader(this.Name, act, this.Machine);
                u.Compile(alerts);
                _defaultActivityUpgraders.Add(u);
            }
        }

        public override Symbol EvaluateSymbol(string symbolName, Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.Activity)
                return BuildActivitySymbol(parameterAssignments, alerts);
            return base.EvaluateSymbol(symbolName, parameterAssignments, alerts);
        }

        private Symbol BuildActivitySymbol(Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            // Get the base Fabric activity JSON.
            Symbol baseSymbol = base.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);
            if (baseSymbol.State != Symbol.SymbolState.Ready)
                return baseSymbol;

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
            foreach (var scu in _caseUpgraders)
            {
                Symbol caseSymbol = scu.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);
                if (caseSymbol.State == Symbol.SymbolState.Ready && caseSymbol.Value != null)
                    newCases.Add(caseSymbol.Value);
            }
            typeProps["cases"] = newCases;

            // Build new defaultActivities array.
            JArray newDefaults = new JArray();
            foreach (var ua in _defaultActivityUpgraders)
            {
                Symbol defSymbol = ua.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);
                if (defSymbol.State == Symbol.SymbolState.Ready && defSymbol.Value != null)
                    newDefaults.Add(defSymbol.Value);
            }
            typeProps["defaultActivities"] = newDefaults;

            return Symbol.ReadySymbol(fabricActivity);
        }

        /// <summary>
        /// Converts a JToken to a JArray.
        /// If token is null, returns an empty JArray.
        /// If token is an object, wraps it in a new JArray.
        /// </summary>
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

    /// <summary>
    /// Minimal Switch case upgrader.
    /// Assumes:
    /// - "value" exists (or is set to null) and
    /// - "activities" is an array (or a single object).
    /// </summary>
    public class SwitchCaseUpgrader : Upgrader
    {
        private const string AdfCaseValuePath = "value";
        private const string AdfCaseActivitiesPath = "activities";

        private readonly List<Upgrader> _activityUpgraders = new List<Upgrader>();

        public SwitchCaseUpgrader(string parentPath, JToken caseToken, IFabricUpgradeMachine machine)
            : base(caseToken, machine)
        {
            this.Path = parentPath;
        }

        public override void Compile(AlertCollector alerts)
        {
            // If the case value is missing, assign null.
            if (this.AdfResourceToken.SelectToken(AdfCaseValuePath) == null)
            {
                this.AdfResourceToken[AdfCaseValuePath] = JValue.CreateNull();
            }

            // Process activities.
            JToken actsToken = this.AdfResourceToken.SelectToken(AdfCaseActivitiesPath);
            JArray actsArray = ConvertTokenToArray(actsToken);
            foreach (JToken act in actsArray)
            {
                Upgrader u = ActivityUpgrader.CreateActivityUpgrader(this.Path, act, this.Machine);
                u.Compile(alerts);
                _activityUpgraders.Add(u);
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
            // Copy the case "value".
            JToken caseValue = this.AdfResourceToken.SelectToken(AdfCaseValuePath);
            caseObj["value"] = caseValue != null ? caseValue.DeepClone() : JValue.CreateNull();

            // Build the activities array.
            JArray newActs = new JArray();
            foreach (var u in _activityUpgraders)
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

// <copyright file="LinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders
{
    /// <summary>
    /// Base class for all LinkedService Upgraders.
    /// </summary>
    public class LinkedServiceUpgrader : Upgrader
    {
        public class LinkedServiceTypes
        {
            public const string AzureBlobStorage = "AzureBlobStorage";
            public const string AzureSqlDatabase = "AzureSqlDatabase";
        }

        protected const string AdfLinkedServiceTypePath = "properties.type";
        protected const string AdfParametersPath = "properties.parameters";
        protected const string AdfConnectionStringPath = "properties.typeProperties.connectionString";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
        };

        protected LinkedServiceUpgrader(
            JToken adfToken,
            IFabricUpgradeMachine machine)
            : base(adfToken, machine)
        {
            this.Name = adfToken.SelectToken("name")?.ToString();
            this.UpgraderType = FabricUpgradeResourceTypes.LinkedService;
            this.LinkedServiceType = adfToken.SelectToken(AdfLinkedServiceTypePath)?.ToString();
            this.Path = "LinkedService " + this.Name;
        }

        protected string LinkedServiceType { get; set; }

        // The parameters declared by a LinkedService.
        protected ResourceParameters LinkedServiceParameters { get; set; }

        /// <summary>
        /// Create the appropriate subclass of LinkedServiceUpgrader, based on the type of the ADF LinkedService.
        /// </summary>
        /// <param name="linkedServiceToken">The JToken describing the ADF LinkedService.</param>
        /// <param name="machine">The FabricUpgradeMachine that provides utilities to Upgraders.</param>
        /// <returns>A DatasetUpgrader.</returns>
        public static LinkedServiceUpgrader CreateLinkedServiceUpgrader(
            JToken linkedServiceToken,
            IFabricUpgradeMachine machine)
        {
            string linkedServiceType = linkedServiceToken.SelectToken("properties.type")?.ToString();
            // The current paradigm is "forbidden unless expressly allowed."
            // Therefore, if we don't explicitly add a LinkedService type to this list, then we will fail the upgrade.
            // We don't _upgrade_ the LinkedService, but we might need to know _something_ about it!
            return linkedServiceType switch
            {
                LinkedServiceTypes.AzureBlobStorage => new AzureBlobStorageLinkedServiceUpgrader(linkedServiceToken, machine),
                LinkedServiceTypes.AzureSqlDatabase => new AzureSqlDatabaseLinkedServiceUpgrader(linkedServiceToken, machine),
                _ => new UnsupportedLinkedServiceUpgrader(linkedServiceToken, machine),
            };
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            // All LinkedServices have their parameter declarations in the same place.

            this.LinkedServiceParameters = ResourceParameters.FromResourceDeclaration(
                this.AdfResourceToken.SelectToken(AdfParametersPath),
                "linkedService()");
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
            {
                return this.BuildExportResolves(parametersFromCaller, alerts);
            }
            if (symbolName == Symbol.CommonNames.ExportInstructions)
            {
                return this.BuildExportInstructions(parametersFromCaller, alerts);
            }

            return base.EvaluateSymbol(symbolName, parametersFromCaller, alerts);
        }

        protected virtual void CheckForExpressionsInConnectionString(
            string connectionString,
            List<string> keysToCheck,
            AlertCollector alerts)
        {
            var connectionSettings = this.BuildConnectionSettings(connectionString);

            foreach (var keyToCheck in keysToCheck)
            {
                this.CheckForExpressionInConnectionSettings(connectionSettings, keyToCheck, alerts);
            }
        }

        protected virtual void CheckForExpressionInConnectionString(
            string connectionString,
            string keyToCheck,
            AlertCollector alerts)
        {
            var connectionSettings = this.BuildConnectionSettings(connectionString);

            this.CheckForExpressionInConnectionSettings(connectionSettings, keyToCheck, alerts);
        }

        protected virtual void CheckForExpressionInConnectionSettings(
            Dictionary<string, JToken> connectionSettings,
            string keyToCheck,
            AlertCollector alerts)
        {
            if (connectionSettings.TryGetValue(keyToCheck, out JToken value) &&
                (value.Type == JTokenType.String) &&
                this.IsLinkedServiceExpression(value.ToString()))
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Name}' because its connection setting '{keyToCheck}' is an expression.");
            }
        }

        protected virtual void CheckForExpressionInProperty(
            string pathToCheck,
            AlertCollector alerts)
        {
            string valueToCheck = this.AdfResourceToken.SelectToken(pathToCheck)?.ToString();
            if (this.IsLinkedServiceExpression(valueToCheck))
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Name}' because its property '{pathToCheck}' is an expression.");
            }
        }

        /// <summary>
        /// During the Export phase, a LinkedService needs to find the ID of the Fabric Connection that
        /// will replace the LinkedService in the Fabric Pipeline. This method prepares the ExportLink 
        /// that will do this job in the LinkedServiceExporter.
        /// </summary>
        /// <param name="parametersFromCaller">The parameters from the caller.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>The Symbol whose value will instruct the LinkedServiceExporter how to find the Fabric Connection ID.</returns>
        protected virtual Symbol BuildExportResolves(
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            List<FabricExportResolveStep> resolves = new List<FabricExportResolveStep>();

            FabricUpgradeResolution.ResolutionType resolutionType = FabricUpgradeResolution.ResolutionType.LinkedServiceToConnectionId;
            FabricExportResolveStep userCredentialConnectionResolve = new FabricExportResolveStep(
                resolutionType,
                this.Name,
                "id")
                .WithHint(this.BuildFabricConnectionHint()
                    .WithTemplate(new FabricUpgradeResolution()
                    {
                        Type = resolutionType,
                        Key = this.Name,
                        Value = "<guid>"
                    }
               ));

            resolves.Add(userCredentialConnectionResolve);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }

        /// <summary>
        /// This method prepares the exportInstruction that will be executed by
        /// the LinkedServiceExporter in the Export phase.
        /// </summary>
        /// <remarks>
        /// You will see in the LinkedServiceExporter that we do not _actually_ export a Fabric Connection.
        /// The exportInstructions tell the LinkedServiceExporter how to "pretend" to export a Fabric Connection.
        /// </remarks>
        /// <param name="parametersFromCaller">The parameters from the caller.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>The Symbol whose value will instruct the LinkedServiceExporter how to export the Connection.</returns>
        protected virtual Symbol BuildExportInstructions(
            Dictionary<string, JToken> parametersFromCaller,
            AlertCollector alerts)
        {
            ConnectionExportInstruction exportInstruction = new ConnectionExportInstruction(this.Name);

            Symbol resolvesSymbol = this.BuildExportResolves(parametersFromCaller, alerts);
            if (resolvesSymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }
            if (resolvesSymbol.Value != null)
            {
                foreach (JToken requiredResolution in (JArray)resolvesSymbol.Value)
                {
                    FabricExportResolveStep resolve = FabricExportResolveStep.FromJToken(requiredResolution);
                    exportInstruction.Resolves.Add(resolve);
                }
            }

            // This is set to an empty GUID here; during the Export phase,
            // the LinkedServiceExporter will use the exportResolves to populate this value.
            exportInstruction.Export["id"] = Guid.Empty.ToString();

            return Symbol.ReadySymbol(exportInstruction.ToJObject());
        }

        /// <summary>
        /// When the LinkedService needs to export a property, like its DatabaseName,
        /// this method performs the slightly complicated steps to do so. 
        /// </summary>
        /// <param name="pathToProperty">Where in the ADF JSON to find the property.</param>
        /// <param name="activeParameters">The parameters to apply to resolve symbol values.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>A Symbol that the LinkedService can return from EvaluateSymbol.</returns>
        protected Symbol BuildLinkedServiceExportableSymbol(
            string pathToProperty,
            ResourceParameters activeParameters,
            AlertCollector alerts)
        {
            // We need to do a bit of fiddling here so that we can use the
            // common symbol resolution code.

            // In a LinkedService, expressions look like
            // "@{...}".
            // Therefore, we first need to convert that to a form like
            // "{ 'type': 'Expression', 'value': '@...' }.
            //
            // If the property is NOT an expression, then fixedupSourceToken is just the same as sourceToken.
            string sourceToken = this.AdfResourceToken.SelectToken(pathToProperty).ToString();
            this.FixupLinkedServiceExpression(sourceToken, out JToken fixedupSourceToken);

            // Then we need to stick this into an object from which the PropertyCopier can copy this value.
            JObject source = new JObject();
            source["value"] = fixedupSourceToken;

            // Then we make an object into which we can copy the value.
            JObject target = new JObject();

            // Then we make a PropertyCopier to move the value.
            // This is important, because PropertyCopier does all of the work to apply expressions and parameters.
            PropertyCopier copier = new PropertyCopier(
                this.Name,
                source,
                target,
                activeParameters,
                alerts);

            // Then, copy from source["value"] to target["value"].
            copier.Copy("value");

            // Finally, pull that "value" property from the target object, and return it!
            return Symbol.ReadySymbol(target["value"]);
        }

        /// <summary>
        /// Combine the default parameter values with the overrides passed in from the caller
        /// to create "active" parameters that can be used when resolving parameters in expressions.
        /// </summary>
        /// <param name="callerValues">Values passed in from the caller.</param>
        /// <returns>A ResourceParameters object that can be used in the PropertyCopier.</returns>
        protected ResourceParameters BuildActiveParameters(
            Dictionary<string, JToken> callerValues)
        {
            return this.LinkedServiceParameters.BuildResolutionContext(callerValues);
        }


        /// <summary>
        /// From the information in the LinkedService, construct a "hint" for the client/user
        /// about the Fabric Connection that should be used to resolve the LinkedService.
        /// </summary>
        /// <returns>A FabricConnectionHint.</returns>
        protected virtual FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            return new FabricUpgradeConnectionHint()
                .WithLinkedServiceName(this.Name);
        }

        /// <summary>
        /// Convert the ConnectionString into a dictionary.
        /// </summary>
        /// <remarks>
        /// A LinkedService may store its "connection string" in different locations (or not even have one!),
        /// so we require the sub-class to extract that and call this method.
        /// </remarks>
        /// <param name="connectionString">The connection string to parse.</param>
        protected Dictionary<string, JToken> BuildConnectionSettings(
            string connectionString)
        {
            Dictionary<string, JToken> settings = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);

            // A connection string looks like "a=b;c=d;e=f'..."
            if (!string.IsNullOrEmpty(connectionString))
            {
                // Split the connection string into elements [ "a=b", "c=d", "e=f", ... ].
                string[] connectionStringParts = connectionString.Split(';');

                // parse each element as "a=b".
                foreach (string connectionStringPart in connectionStringParts)
                {
                    string[] parsedPart = connectionStringPart.Split(new char[] { '=' }, 2);
                    if (parsedPart.Length == 2)
                    {
                        settings[parsedPart[0]] = parsedPart[1];
                    }
                }
            }

            return settings;
        }

        /// <summary>
        /// In LinkedServices, expressions look like "@{...}" for some reason.
        /// This method detects that form.
        /// </summary>
        /// <param name="possibleExpression">A string that might be an expression.</param>
        /// <returns>True if and only if the string is a LinkedService Expression.</returns>
        protected bool IsLinkedServiceExpression(string possibleExpression)
        {
            return ((possibleExpression != null) &&
                possibleExpression.Trim().StartsWith("@{") &&
                possibleExpression.Trim().EndsWith("}"));
        }

        /// <summary>
        /// In LinkedServices, expressions look like "@{...}" for some reason.
        /// This method detects that form and converts it to a form that can be transformed by UpgradeExpression.
        /// </summary>
        /// <param name="possibleExpression">A string that might be an expression.</param>
        /// <param name="canonicalValue">
        /// If 'possibleExpression' is not an expression, the original string.
        /// If 'possibleExpression' is an expression, then the expression converted to a "canonical" form.
        /// </param>
        /// <returns>If this is an expression.</returns>
        protected bool FixupLinkedServiceExpression(
            string possibleExpression,
            out JToken canonicalValue)
        {
            canonicalValue = possibleExpression;
            if (!this.IsLinkedServiceExpression(possibleExpression))
            {
                return false;
            }

            // Remove the "@{" from the beginning.
            string canonicalString = possibleExpression.Trim().Substring(2);
            // Remove the "}" from the end.
            canonicalString = canonicalString.Trim().Substring(0, canonicalString.Length - 1);

            // Insert a new "@" at the beginning.
            canonicalString = "@" + canonicalString;

            JObject expressionObject = new JObject();
            expressionObject["type"] = "Expression";
            expressionObject["value"] = canonicalString;

            canonicalValue = expressionObject;

            return true;
        }
    }
}

// <copyright file="StoredProcedureActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// Minimal upgrader for an ADF Stored Procedure activity to a Fabric SqlServerStoredProcedure activity.
    /// This version builds the final Fabric JSON from scratch, avoiding recursion.
    /// </summary>
    public class StoredProcedureActivityUpgrader : ActivityUpgrader
    {
        public StoredProcedureActivityUpgrader(string parentPath, JToken activityToken, IFabricUpgradeMachine machine)
            : base(ActivityTypes.SqlStoredProcedure, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            // We do minimal processing: ensure a stored procedure name is available.
            JToken spToken = this.AdfResourceToken.SelectToken("typeProperties.storedProcedureName");
            if (spToken != null && spToken.Type == JTokenType.Object)
            {
                JToken valueToken = spToken["value"];
                if (valueToken != null && !string.IsNullOrWhiteSpace(valueToken.ToString()))
                {
                    this.AdfResourceToken["typeProperties"]["storedProcedureName"] = valueToken.ToString();
                }
                else
                {
                    this.AdfResourceToken["typeProperties"]["storedProcedureName"] = "DefaultStoredProcedure";
                }
            }
            else if (spToken == null || string.IsNullOrWhiteSpace(spToken.ToString()))
            {
                this.AdfResourceToken["typeProperties"]["storedProcedureName"] = "DefaultStoredProcedure";
            }
        }

        /// <inheritdoc/>
        public override void PreSort(List<Upgrader> allUpgraders, AlertCollector alerts)
        {
            // Minimal implementation: no additional dependency resolution.
        }

        /// <inheritdoc/>
        public override Symbol EvaluateSymbol(string symbolName, Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            // We only need to build the final activity JSON.
            if (symbolName == Symbol.CommonNames.Activity)
            {
                return BuildActivitySymbol(parameterAssignments, alerts);
            }
            return Symbol.ReadySymbol(null);
        }

        /// <summary>
        /// Builds the final Fabric activity JSON for the stored procedure activity from scratch.
        /// </summary>
        protected override Symbol BuildActivitySymbol(Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            // Create a new JSON object for the Fabric activity.
            JObject fabricActivity = new JObject();

            // Set required top-level properties.
            string activityName = this.AdfResourceToken.SelectToken("name")?.ToString();
            fabricActivity["name"] = string.IsNullOrWhiteSpace(activityName)
                ? "DefaultStoredProcActivity"
                : activityName;

            // Explicitly copy the dependency information from the original ADF activity.
            JToken adfDependsOn = this.AdfResourceToken.SelectToken("dependsOn");
            if (adfDependsOn != null)
            {
                // Overwrite the Fabric JSON's "dependsOn" with the original dependencies.
                fabricActivity["dependsOn"] = adfDependsOn.DeepClone();
            }

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivity, alerts);
            copier.Copy("description");
            copier.Copy("dependsOn");

            // Set the Fabric activity type.
            fabricActivity["type"] = "SqlServerStoredProcedure";

            // Set description.
            fabricActivity["description"] = this.AdfResourceToken["description"] ?? "";

            // Build the typeProperties object.
            JObject typeProps = new JObject
            {
                // Set Fabric-specific operation type.
                ["operationType"] = "SqlServerStoredProcedure",
                // Set stored procedure name.
                ["storedProcedureName"] = this.AdfResourceToken.SelectToken("typeProperties.storedProcedureName")?.ToString() ?? "DefaultStoredProcedure"
            };

            // Copy stored procedure parameters if present.
            JToken sprocParams = this.AdfResourceToken.SelectToken("typeProperties.storedProcedureParameters");
            if (sprocParams != null)
            {
                typeProps["storedProcedureParameters"] = sprocParams.DeepClone();
            }
            fabricActivity["typeProperties"] = typeProps;

            // Pass through the linked service if present; otherwise, use a placeholder.
            JToken linkedService = this.AdfResourceToken.SelectToken("linkedService");
            fabricActivity["linkedService"] = linkedService != null
                ? linkedService.DeepClone()
                : new JObject();

            return Symbol.ReadySymbol(fabricActivity);
        }

        /// <summary>
        /// Minimal implementation: No export resolution steps.
        /// </summary>
        protected override Symbol BuildExportResolveStepsSymbol(Dictionary<string, JToken> parameterAssignments, AlertCollector alerts)
        {
            return Symbol.ReadySymbol(null);
        }
    }
}

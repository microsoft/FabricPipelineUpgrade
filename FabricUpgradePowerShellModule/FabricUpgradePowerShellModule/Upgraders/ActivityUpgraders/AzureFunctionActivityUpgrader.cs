// <copyright file="AzureFunctionActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// Upgrades an ADF Azure Function activity to a Fabric InvokeAzureFunction activity.
    /// Supports anonymous access if typeProperties.azureFunctionConnection is null.
    /// </summary>
    public class AzureFunctionActivityUpgrader : ActivityUpgrader
    {
        // Path to the function name in the ADF activity JSON.
        private const string adfFunctionNamePath = "typeProperties.functionName";
        // Path to the Azure Function connection in the ADF activity JSON.
        private const string adfFunctionConnectionPath = "typeProperties.azureFunctionConnection";

        // We require the function name but the connection can be omitted for anonymous access.
        private readonly List<string> requiredAdfProperties = new List<string>
        {
            adfFunctionNamePath
            // Note: Not including adfFunctionConnectionPath since anonymous access is allowed.
        };

        // Reference to the upgrader that handles the Azure Function connection (if provided).
        private Upgrader azureFunctionConnectionUpgrader;

        // Flag indicating if the activity uses anonymous access.
        private bool isAnonymousAccess = false;

        public AzureFunctionActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base("AzureFunctionActivity", parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
            // Validate that required properties exist.
            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            // Check if the Azure Function connection is provided.
            JToken connectionToken = this.AdfResourceToken.SelectToken(adfFunctionConnectionPath);
            if (connectionToken == null || string.IsNullOrWhiteSpace(connectionToken.ToString()))
            {
                // Allow anonymous access.
                this.isAnonymousAccess = true;
            }
        }

        /// <inheritdoc/>
        public override void PreSort(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            // Only attempt to resolve the connection if it's not anonymous.
            if (!this.isAnonymousAccess)
            {
                this.azureFunctionConnectionUpgrader = this.FindOtherUpgrader(
                    allUpgraders,
                    FabricUpgradeResourceTypes.Connection,
                    adfFunctionConnectionPath,
                    alerts);

                if (this.azureFunctionConnectionUpgrader != null)
                {
                    this.DependsOn.Add(this.azureFunctionConnectionUpgrader);
                }
            }
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
            var resolves = new List<FabricExportResolveStep>();

            // Resolve the workspaceId (to be set during export).
            var workspaceIdResolve = new FabricExportResolveStep(
                FabricUpgradeResolution.ResolutionType.WorkspaceId,
                null,
                "typeProperties.workspaceId");
            resolves.Add(workspaceIdResolve);

            // Only add a resolution step for the connection if it's not anonymous.
            if (!this.isAnonymousAccess)
            {
                var resolutionType = FabricUpgradeResolution.ResolutionType.CredentialConnectionId;
                var functionConnectionResolve = new FabricExportResolveStep(
                    resolutionType,
                    "azurefunction",
                    "typeProperties.azureFunctionConnection")
                    .WithHint(new FabricUpgradeConnectionHint
                    {
                        LinkedServiceName = null,
                        ConnectionType = "AzureFunction",
                        Datasource = "AzureFunction"
                    }
                    .WithTemplate(new FabricUpgradeResolution
                    {
                        Type = resolutionType,
                        Key = "azurefunction",
                        Value = "<Fabric Azure Function Connection ID>"
                    }));
                resolves.Add(functionConnectionResolve);
            }
            else
            {
                // Optionally, you can add a resolve step that marks this connection as anonymous,
                // or simply leave it out and set a default value during the Activity build.
            }

            // Resolve the Azure Function resource ID using the function name.
            string functionName = this.AdfResourceToken.SelectToken(adfFunctionNamePath)?.ToString();
            var functionNameResolve = new FabricExportResolveStep(
                FabricUpgradeResolution.ResolutionType.AdfResourceNameToFabricResourceId,
                $"AzureFunction:{functionName}",
                "typeProperties.functionId");
            resolves.Add(functionNameResolve);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
        }

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            // Build the common activity symbol.
            Symbol activitySymbol = base.EvaluateSymbol(Symbol.CommonNames.Activity, parameterAssignments, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO: Handle non-ready state if necessary.
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;
            var copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);

            // Create a new JSON object for the Fabric activity.
            JObject fabricActivity = new JObject();

            // Set required top-level properties.
            string activityName = this.AdfResourceToken.SelectToken("name")?.ToString();
            fabricActivity["name"] = string.IsNullOrWhiteSpace(activityName)
                ? "DefaultStoredProcActivity"
                : activityName;

            JToken adfDependsOn = this.AdfResourceToken.SelectToken("dependsOn");
            if (adfDependsOn != null)
            {
                // Overwrite the Fabric JSON's "dependsOn" with the original dependencies.
                fabricActivity["dependsOn"] = adfDependsOn.DeepClone();
            }

            // Copy over common properties.
            copier.Copy("description");
            copier.Copy("typeProperties.parameters");
            copier.Copy(adfFunctionNamePath);
            copier.Copy("dependsOn");

            // Set the Fabric-specific operation type.
            copier.Set("typeProperties.operationType", "InvokeAzureFunction");

            // Set placeholder IDs to be replaced during the export phase.
            copier.Set("typeProperties.functionId", Guid.Empty.ToString());
            copier.Set("typeProperties.workspaceId", Guid.Empty.ToString());

            // For the Azure Function connection, either use the resolved connection ID or a default value for anonymous access.
            if (!this.isAnonymousAccess)
            {
                copier.Set("typeProperties.azureFunctionConnection", Guid.Empty.ToString());
            }
            else
            {
                // For anonymous access, set a known placeholder (e.g., "anonymous") instead of null.
                copier.Set("typeProperties.azureFunctionConnection", "anonymous");
            }

            return Symbol.ReadySymbol(fabricActivityObject);
        }
    }
}

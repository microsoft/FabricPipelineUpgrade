// <copyright file="Upgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Upgraders
{
    public class Upgrader
    {
        protected Upgrader(
            JToken adfToken,
            IFabricUpgradeMachine machine)
        {
            this.AdfResourceToken = adfToken;
            this.Machine = machine;
        }

        protected JToken AdfResourceToken { get; set; }

        // The type of this Upgrader, like DataPipeline or LinkedService.
        // Used when resolving SymbolReferences (see below) in the unlikely event that two
        // resources have the same name but different types.
        public FabricUpgradeResourceTypes UpgraderType { get; set; } = FabricUpgradeResourceTypes.Unknown;

        // The name of the ADF resource being upgraded.
        // The generated Fabric resource will have the same name.
        // This name is the only symbol "exported" from an Upgrader;
        // it allows other Upgraders to resolve their SymbolReferences.
        public string Name { get; set; } = null;

        /// <summary>
        /// Gets or sets a description of the "path" to an artifact or activity.
        /// </summary>
        /// <remarks>
        /// It has a form like "pipeline.activity"; e.g., "copyPipeline/wait1".
        /// This path will be used in error messages, and within the resolutions of (for example) WebActivity Connections.
        /// </remarks>
        public virtual string Path { get; protected set; } = string.Empty;

        // The FabricUpgradeMachine that this Upgrader uses to resolve symbols, access the PublicApi, etc.
        public IFabricUpgradeMachine Machine { get; private set; }

        // Used in Sort()
        public List<Upgrader> DependsOn { get; set; } = new List<Upgrader>();

        // Used in Sort()
        public enum UpgraderSortingState
        {
            Unsorted = 0,
            Sorting = 1,
            Sorted = 2,
        }

        // Used in Sort()
        public UpgraderSortingState SortingState { get; set; } = UpgraderSortingState.Unsorted;

        /// <summary>
        /// Verify that there are no impediments to Upgrading.
        /// For example, missing properties, malformed properties, unsupported properties, etc.
        /// This step may also prepare parameters.
        /// </summary>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        public virtual void Compile(AlertCollector alerts)
        {
        }

        /// <summary>
        /// Build the dependencies between this Upgrader and others.
        /// </summary>
        /// <remarks>
        /// This method populates this Upgrader's DependsOn list,
        /// which is used later to sort the Upgraders,
        /// which is vitally important when we export the Fabric Resources.
        /// </remarks>
        /// <param name="allUpgraders">A list of all the upgraders</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        public virtual void PreSort(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
        }

        /// <summary>
        /// A shortcut method for backwards compatibility when the caller does not have any parameterAssignments.
        /// </summary>
        /// <param name="symbolName">The 'name' of the Symbol to export (see Symbol.CommonNames).</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>A Symbol containing the requested JToken.</returns>
        public Symbol EvaluateSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            return this.EvaluateSymbol(symbolName, null, alerts);
        }

        /// <summary>
        /// Build a value that can be used by another Upgrader/Exporter/Machine.
        /// This method handles checking parameters and expressions.
        /// </summary>
        /// <param name="symbolName">The 'name' of the Symbol to export (see Symbol.CommonNames).</param>
        /// <param name="parameterAssignments">
        /// If a property has an expression, and that expression includes a parameter like 'dataset().xyz',
        /// then override the default parameter values with the ones in this dictionary.
        /// </param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>A Symbol containing the requested JToken.</returns>
        public virtual Symbol EvaluateSymbol(
            string symbolName,
            Dictionary<string, JToken> parameterAssignments,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportInstructions)
            {
                // If a subclass does not generate a Fabric resource, then return a null symbol value.
                // For example, Activities and Datasets to do not generate a Fabric resource.
                return Symbol.ReadySymbol(null);
            }

            if (symbolName == Symbol.CommonNames.ExportResolveSteps)
            {
                // If a subclass does not have any resolves, then return a null symbol value.
                // Most Activities do not have any resolves, but Copy, Web, and ExecutePipeline do.
                return Symbol.ReadySymbol(null);
            }

            // If the subclass does not resolve this, then it is an invalid symbol name.
            alerts.AddPermanentError($"Cannot resolve symbol '{symbolName}'.");
            return Symbol.MissingSymbol();
        }

        /// <summary>
        /// Ensure that all of the required properties are not null.
        /// </summary>
        /// <param name="requiredAdfProperties">The path to the properties that must not be null.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        protected void CheckRequiredAdfProperties(
            List<string> requiredAdfProperties,
            AlertCollector alerts)
        {
            foreach (var property in requiredAdfProperties)
            {
                JToken value = this.AdfResourceToken.SelectToken(property);
                if (value == null)
                {
                    alerts.AddPermanentError($"{this.Path} property {property} must not be null.");
                }
            }
        }

        /// <summary>
        /// There is a property in the ADF JSON that is the name of another ADF Resource.
        /// This method finds the Upgrader for that Resource.
        /// </summary>
        /// <param name="allUpgraders">The Upgraders to search.</param>
        /// <param name="upgraderType">The Upgrader type to find (like DataPipeline or Dataset).</param>
        /// <param name="pathToName">The JSON path in this AdfResoureToken to the property that is the name.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns></returns>
        protected Upgrader FindOtherUpgrader(
            List<Upgrader> allUpgraders,
            FabricUpgradeResourceTypes upgraderType,
            string pathToName,
            AlertCollector alerts
            )
        {
            string upgraderName = this.AdfResourceToken.SelectToken(pathToName)?.ToString();
            if (string.IsNullOrEmpty(upgraderName))
            {
                return null;
            }

            Upgrader matchingUpgrader = allUpgraders.FirstOrDefault(u => u.UpgraderType == upgraderType && u.Name == upgraderName);
            if (matchingUpgrader != null)
            {
                return matchingUpgrader;
            }
            else
            {
                alerts.AddPermanentError($"{this.Path} references {upgraderType} '{upgraderName}', but UpgradePackage does not include {upgraderType} '{upgraderName}'.");
            }

            return matchingUpgrader;
        }

        /// <summary>
        /// Populate the sortedUpgraders.
        /// If Upgrader A depends on Upgrader B, then Upgrader B appears before Upgrader A in this list.
        /// </summary>
        /// <remarks>
        /// When we Export the Fabric Resources, we need to export B before we export A, so that 
        /// 1) We can populate A's links with B's Fabric Resource ID;
        /// 2) Fabric will accept A (it will not accept A if all of its links are not correctly populated).
        ///
        /// This performs a topological sort (https://en.wikipedia.org/wiki/Topological_sorting).
        /// </remarks>
        /// <param name="sortedUpgraders">The list of Upgraders to populate.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>Are we able to sort the Upgraders.</returns>
        public bool Sort(
            List<Upgrader> sortedUpgraders,
            AlertCollector alerts)
        {
            if (this.SortingState == UpgraderSortingState.Sorted)
            {
                return true;
            }

            if (this.SortingState == UpgraderSortingState.Sorting)
            {
                // Uh-oh! This DG is not a DAG!
                alerts.AddPermanentError("The UpgradePackage contains circular references");
                return false;
            }

            this.SortingState = UpgraderSortingState.Sorting;

            foreach (Upgrader otherUpgrader in this.DependsOn)
            {
                otherUpgrader.Sort(sortedUpgraders, alerts);
            }

            this.SortingState = UpgraderSortingState.Sorted;

            sortedUpgraders.Add(this);

            return true;
        }
    }
}

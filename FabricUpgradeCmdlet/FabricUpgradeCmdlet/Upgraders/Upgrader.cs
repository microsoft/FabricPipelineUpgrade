using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Upgraders
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

        // When an Upgrader finishes its Generate phase, it will set this ID to one returned by Fabric backend.
        // This ID will be used by other Upgraders to resolve SymbolLinks.
        // (Which is one of the reasons we must Generate Upgraders in a particular order).
        public Guid? FabricResourceId { get; protected set; } = null;

        /// <summary>
        /// Gets or sets a description of the "path" to an artifact or activity.
        /// </summary>
        /// <remarks>
        /// It has a form like "pipeline.activity"; e.g., "copyPipeline/wait1".
        /// This path will be used in error messages, and within the resolutions of (for example) WebActivity Connections.
        /// </remarks>
        public virtual string Path { get; protected set; } = string.Empty;

        /// <summary>
        /// Gets the list of symbols referenced by a resource.
        /// </summary>
        /// <remarks>
        /// Each upgrader will include a list of symbols that "reference" other upgraders.
        ///
        /// For examples:
        /// A Pipeline with an ExecutePipeline Activity will reference the Pipeline it invokes.
        /// A Pipeline with a Copy Activity will reference a Dataset (which will reference a LinkedService).
        ///
        /// During the Compile phase, each Upgrader will identify its SymbolReferences.
        /// During the Link phase, each Upgrader will "resolve" each SymbolReference to another Upgrader.
        /// During the Generate phase, each Upgrader will extract the ID of its SymbolReferences.
        /// </remarks>
        //public List<SymbolReference> SymbolReferences { get; } = new List<SymbolReference>();

        // An Upgrade process generates a directed acyclic graph (DAG) that describes
        // the order in which resources must be upgraded: from leaf to root.
        // If no other Upgrader references this Upgrader, then this is a "root" Upgrader.
        public bool IsRoot { get; set; } = true;

        // A resource may be "accessible" from more than one root of the DAG.
        // This flag prevents the Upgrader from generating the same Fabric resource twice.
        public bool IsAlreadyGenerated { get; set; } = false;

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
        

        public virtual void Compile(AlertCollector alerts)
        {
            // Note: In subclasses, this is where you will ensure that properties have valid values.
        }

        /// <summary>
        /// Build the dependencies between this Upgrader and others.
        /// </summary>
        /// <remarks>
        /// This method populates this Upgrader's DependsOn list,
        /// which is used later to sort the Upgraders.
        /// </remarks>
        /// <param name="allUpgraders">A list of all the upgraders</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        public virtual void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
        }

        public virtual Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == "fabricResource")
            {
                // If a subclass does not generate a fabric resource, then return a null symbol value.
                // For example, Activities and Datasets to do not generate a fabric resource.
                // For example, LinkedServices to not (yet!) generate a fabric resource.
                return Symbol.ReadySymbol(null);
            }

            if (symbolName == "exportLinks")
            {
                // If a subclass does not have any links, then return a null symbol value.
                // Most Activities do not have any links.
                return Symbol.ReadySymbol(null);
            }

            // If the subclass does not resolve this, then it is an invalid symbol name.
            alerts.AddPermanentError($"Cannot resolve symbol '{symbolName}'.");
            return Symbol.MissingSymbol();
        }

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
                alerts.AddPermanentError($"'{this.Path}' references {upgraderType} '{upgraderName}', but UpgradePackage does not include {upgraderType} '{upgraderName}'.");
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








        /// <summary>
        /// An opportunity to check whether this Upgrader is certain to fail.
        /// </summary>
        /// <remarks>
        /// Here is where we might check FeatureFlights and TenantSwitches to ensure that the caller
        /// is allowed to create the Fabric Artifact.
        ///
        /// This method should append to 'alerts' a description of any deviation from complete success.
        /// This way, we can list _all_ the problems, not just the first one.
        /// </remarks>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>Nothing.</returns>
        public virtual Task PreCheckUpgradeAsync(
            AlertCollector alerts,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Resolve each SymbolReference into an Upgrader.
        /// </summary>
        /// <remarks>
        /// This method should append to 'alerts' a description of any deviation from complete success.
        /// This way, we can list _all_ the problems, not just the first one.
        /// </remarks>
        /// <param name="otherUpgraders">The list of other Upgraders.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        public virtual void Link(List<Upgrader> otherUpgraders, AlertCollector alerts)
        {
        }

        /// <summary>
        /// Construct the JObject payload sent to CreateOrUpdateArtifact.
        /// </summary>
        /// <remarks>
        /// For some Upgraders, like Pipeline, this JObject will be the one sent to CreateOrUpdateArtifact.
        /// For some Upgraders, this JObject will be included in another Upgrader's payload.
        /// For examples:
        /// An Activity will return its Activity JObject for inclusion in its parent Pipeline.
        /// A Dataset will return a JObject that can be used by the Activity that references it.
        /// </remarks>
        /// <returns>The payload sent to CreateOrUpdateArtifact or incorporated in another Upgrader's payload.</returns>
        public virtual JObject ConstructPayloadForGenerate()
        {
            return null;
        }

        /// <summary>
        /// Produce the resource specified by this Upgrader.
        /// </summary>
        /// <remarks>
        /// If an Upgrader does not Generate, then it can use this default implementation.
        /// For example, Activities do not Generate; Datasets do not Generate; LinkedServices do not Generate.
        /// </remarks>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Nothing.</returns>
        public virtual Task GenerateAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

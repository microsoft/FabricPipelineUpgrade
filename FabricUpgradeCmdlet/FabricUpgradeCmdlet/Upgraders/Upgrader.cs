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

        /// <summary>
        /// The type of resource that this Upgrader processes.
        /// </summary>
        public enum Type
        {
            /// <summary>Every enum deserves an invalid value.</summary>
            Unknown = 0,

            /// <summary>Upgrader handles a DataPipeline.</summary>
            DataPipeline = 1,

            /// <summary>Upgrader handles a Dataset.</summary>
            Dataset = 2,

            /// <summary>Upgrader handles a LinkedService.</summary>
            LinkedService = 3,

            /// <summary>Upgrader handles a Trigger.</summary>
            Trigger = 4,

            /// <summary>Upgrader handles a PipelineActivity.</summary>
            PipelineActivity = 5,
        }

        protected JToken AdfResourceToken { get; set; }

        // The type of this Upgrader, like "DataPipeline" or "LinkedService".
        // Used when resolving SymbolReferences (see below) in the unlikely event that two
        // resources have the same name but different types.
        public Type UpgraderType { get; set; } = Type.Unknown;

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



        public virtual void Compile(AlertCollector alerts)
        {
        }

        public virtual Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            alerts.AddPermanentError($"Cannot resolve symbol '{symbolName}'.");
            return Symbol.MissingSymbol();
        }


        protected void Set(
            JToken value,
            JObject destination,
            string destinationLocation)
        {
            string[] targetPathParts = destinationLocation.Split(".");

            JObject target = destination;
            for (int nPart = 0; nPart < targetPathParts.Length - 1; nPart++)
            {
                string dp = targetPathParts[nPart];
                if (!target.ContainsKey(dp))
                {
                    target[dp] = new JObject();
                }

                target = (JObject)target[dp];
            }

            string property = targetPathParts[targetPathParts.Length - 1];
            target[property] = value;
        }

        protected void Move(
            JToken source,
            string sourceLocation,
            JObject destination,
            string destinationLocation)
        {
            JToken value = source.SelectToken(sourceLocation);
            this.Set(value, destination, destinationLocation);
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

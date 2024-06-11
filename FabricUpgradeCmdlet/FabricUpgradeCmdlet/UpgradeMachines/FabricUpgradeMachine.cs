// <copyright file="FabricUpgradeMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradeCmdlet.UpgradeMachines
{
    using FabricUpgradeCmdlet.Models;
    using FabricUpgradeCmdlet.Upgraders;
    using FabricUpgradeCmdlet.Utilities;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The base class for FabricUpgradeMachines.
    /// Each derived class will pre-process an UpgradePackage per its format,
    /// and then call the common code in this class.
    /// </summary>
    public abstract class FabricUpgradeMachine : IFabricUpgradeMachine
    {
        protected FabricUpgradeMachine(
            List<FabricUpgradeResolution> resolutions,
            AlertCollector alerts)
        {
            this.Resolutions = resolutions;
            this.Alerts = alerts ?? new AlertCollector();
        }

        protected AlertCollector Alerts { get; set; }

        protected List<FabricUpgradeResolution> Resolutions { get; set; } = new List<FabricUpgradeResolution>();

        protected List<Upgrader> Upgraders { get; set; } = new List<Upgrader>();

        /// <inheritdoc/>
        /// <remarks>
        /// Each subclass will "unpack" the UpgradePackage to initialize the Upgraders and the Metadata,
        /// and will then invoke the common PerformUpgradeAsync (below).
        /// </remarks>
        public abstract FabricUpgradeProgress Upgrade();

        /// <inheritdoc/>
        public string Resolve(
            FabricUpgradeResolution.ResolutionType resolutionType,
            string key)
        {
            FabricUpgradeResolution matchingResolution = this.Resolutions.FirstOrDefault(
                r => r.Type == resolutionType && r.Key == key);

            return matchingResolution?.Value;
        }

        /// <summary>
        /// This method performs the actual upgrading.
        /// </summary>
        /// <remarks>
        /// This method is called after the Upgraders have been constructed.
        /// Each subclass will have its own way of converting the FabricUpgradeRequest
        /// into a set of Upgraders; that happens in UpgradeAsync() (above).
        /// </remarks>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A FabricUpgradeResponse.</returns>
        protected virtual async Task<FabricUpgradeProgress> PerformUpgradeAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                /* Each of the following calls can throw an UpgradeFailureException, and we will report the failures below.
                 * The long-term goal is to allow the system to collect failures at different phases for different upgraders:
                 * One Upgrader might fail in Compile and another in Link, and we could collect all of these failures at once.
                 * For now, though, we'll fail at the first phase in which any Upgrader fails.*/

                await this.PrecheckUpgradersAsync(cancellationToken).ConfigureAwait(false);

                this.CompileUpgraders();

                this.LinkUpgraders();

                this.ValidateDag();

                await this.GenerateRootResourcesAsync(cancellationToken).ConfigureAwait(false);

                // This Upgrade has succeeded!
                // Tell the user about any Warnings that arose along the way.
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                    Alerts = this.Alerts.ToList(),
                };
            }
            catch (UpgradeFailureException)
            {
                return this.RespondWithFailures();
            }
        }

        private async Task PrecheckUpgradersAsync(
            CancellationToken cancellationToken)
        {
            foreach (Upgrader upgrader in this.Upgraders)
            {
                await upgrader.PreCheckUpgradeAsync(this.Alerts, cancellationToken).ConfigureAwait(false);
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("Precheck");
            }
        }

        private void CompileUpgraders()
        {
            foreach (Upgrader upgrader in this.Upgraders)
            {
                upgrader.Compile(this.Alerts);
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("Compile");
            }
        }

        private void LinkUpgraders()
        {
            foreach (Upgrader upgrader in this.Upgraders)
            {
                upgrader.Link(this.Upgraders, this.Alerts);
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("Link");
            }
        }

        /// <summary>
        /// The set of Upgraders should form a DAG. Verify that this is true.
        /// </summary>
        private void ValidateDag()
        {
            /* TODO Task 3005567: Use topological sorting to validate and construct DAG: Perform a topological sort of the Upgraders.
             * See https://en.wikipedia.org/wiki/Topological_sorting.
             * This will:
             * (1) find the root nodes,
             * (2) detect circular references, and
             * (3) construct the order in which the Upgraders should Generate.
             * Item (3) is quite wonderful: it allows us to skip the recursion in GenerateResourceAndPrerequisitesAsync.
             */

            if (this.Upgraders.All(u => !u.IsRoot))
            {
                this.Alerts.AddPermanentError("This UpgradePackage contains no 'root' resource; please examine it for circular references");
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("DAG");
            }
        }

        private async Task GenerateRootResourcesAsync(
            CancellationToken cancellationToken)
        {
            foreach (Upgrader rootUpgrader in this.Upgraders.Where(u => u.IsRoot))
            {
                await this.GenerateResourceAndPrerequisitesAsync(
                    rootUpgrader,
                    cancellationToken).ConfigureAwait(false);
            }

            /* TODO: Task 3014138: In FabricUpgrade, handle errors from GenerateAsync
                * Because we CreateOrUpdate each Fabric Resource, this operation is idempotent.
                * Therefore, the caller can try again.
                */
        }

        /// <summary>
        /// From the current list of alerts, determine whether the Upgrade has failed or will fail.
        /// </summary>
        /// <remarks>
        /// If any of the alerts is not just a Warning,
        /// then the Upgrade will not be able to complete.
        /// </remarks>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>Whether this list of alerts indicates that the Upgrade can continue.</returns>
        private bool AlertsIndicateFailure()
        {
            return this.Alerts.Any(f => f.Severity != FabricUpgradeAlert.FailureSeverity.Warning);
        }

        private FabricUpgradeProgress RespondWithFailures()
        {
            return new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                Alerts = this.Alerts.ToList(),
            };
        }

        /// <summary>
        /// Recursively generate the Upgrades in the correct order.
        /// </summary>
        /// <remarks>
        /// The resources and their SymbolLinks form a DAG.
        /// This method recursively generates the items from the leaves to the roots.
        ///
        /// This method is recursive, which is NOT acceptable; StackOverflowExceptions exist for a reason.
        /// TODO Task 3005567: Use topological sorting to validate and construct DAG: Then, convert this to an iterative method.
        /// </remarks>
        /// <param name="rootUpgrader">The current DAG node to be upgraded, after its prerequisites are upgraded.</param>
        /// <param name="cancellationToken">The CancellationToken.</param>
        /// <returns>Nothing.</returns>
        private async Task GenerateResourceAndPrerequisitesAsync(
            Upgrader rootUpgrader,
            CancellationToken cancellationToken)
        {
            await Task.Delay(0);
            /*
            // First, generate all of the artifacts upon which the root depends.
            // The "resolution" of a SymbolReference is the Upgrader that handles the ADF Resource described by the SymbolReference.
            foreach (SymbolReference prerequisite in rootUpgrader.SymbolReferences)
            {
                // Note that this is recursive.
                await this.GenerateResourceAndPrerequisitesAsync(
                    prerequisite.Upgrader,
                    cancellationToken).ConfigureAwait(false);
            }

            // ... and then generate the root!
            // (Don't generate a Fabric Resource twice; consider a diamond shape in a DAG.)
            if (!rootUpgrader.IsAlreadyGenerated)
            {
                await rootUpgrader.GenerateAsync(cancellationToken).ConfigureAwait(false);
            }
            */
        }
    }
}

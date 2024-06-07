// <copyright file="IFabricUpgradeMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using System.Threading;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.UpgradeMachines
{
    /// <summary>
    /// A FabricUpgradeMachine performs one Upgrade
    /// and provides common utilities to Upgraders used in that Upgrade.
    /// </summary>
    public interface IFabricUpgradeMachine
    {
        /// <summary>
        /// Perform the upgrade.
        /// </summary>
        /// <param name="cancellationToken">The CancellationToken.</param>
        /// <returns>A FabricUpgradeResponse.</returns>
        FabricUpgradeProgress Upgrade();

        /// <summary>
        /// Resolve a symbol to a value provided in the Request.
        /// </summary>
        /// <remarks>
        /// For example, a LinkedService will use this to find the GUID of the Fabric Connection that will replace it.
        /// For example, a Web Activity will use this to find the GUID of the Fabric Connection to use.
        /// </remarks>
        /// <param name="resolutionType">The type of the resolution.</param>
        /// <param name="key">The key used to identify which resolution to find.</param>
        /// <returns>The string value of the resolution.</returns>
        string Resolve(
            FabricUpgradeResolution.ResolutionType resolutionType,
            string key);
    }
}

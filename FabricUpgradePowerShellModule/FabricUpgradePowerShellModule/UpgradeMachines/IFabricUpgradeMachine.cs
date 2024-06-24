// <copyright file="IFabricUpgradeMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Models;

namespace FabricUpgradePowerShellModule.UpgradeMachines
{
    /// <summary>
    /// A FabricUpgradeMachine performs one Upgrade
    /// </summary>
    public interface IFabricUpgradeMachine
    {
        /// <summary>
        /// Perform the upgrade.
        /// </summary>
        /// <returns>A FabricUpgradeResponse.</returns>
        FabricUpgradeProgress Upgrade();
    }
}

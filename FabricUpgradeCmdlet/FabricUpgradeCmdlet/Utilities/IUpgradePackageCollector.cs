// <copyright file="IUpgradePackageCollector.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradeCmdlet.Utilities
{
    internal interface IUpgradePackageCollector
    {
        void Collect(string entryName, string entryData);
    }
}

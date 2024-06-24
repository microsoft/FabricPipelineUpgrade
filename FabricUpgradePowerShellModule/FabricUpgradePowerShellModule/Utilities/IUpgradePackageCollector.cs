// <copyright file="IUpgradePackageCollector.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradePowerShellModule.Utilities
{
    /// <summary>
    /// An interface used to collect the contents of a set of records in a ZIP file.
    /// The Unzip operation will call back into Collect() for each record in the ZIP file.
    /// </summary>
    internal interface IUpgradePackageCollector
    {
        /// <summary>
        /// Include this entry in the list of records.
        /// </summary>
        /// <param name="entryName">The name of the record (file, folder, etc.).</param>
        /// <param name="entryData">The contents of this file.</param>
        void Collect(string entryName, string entryData);
    }
}

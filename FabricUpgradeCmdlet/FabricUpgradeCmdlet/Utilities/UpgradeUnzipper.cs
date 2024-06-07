// <copyright file="UpgradeUnzipper.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradeCmdlet.Utilities
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This class implements the Unzip operation used by the FabricUpgrader.
    /// </summary>
    internal static class UpgradeUnzipper
    {
        /// <summary>
        /// Unzip the UpgradePackage and send each archive entry to a callback lambda.
        /// </summary>
        /// <param name="package">The zipped UpgradePackage.</param>
        /// <param name="onArchiveEntry">Callback to handle name and contents of each unzipped entry.</param>
        /// <returns>Nothing.</returns>
        public static void Unzip(
            byte[] package,
            IUpgradePackageCollector packageCollector)
        {
            using MemoryStream zipStream = new MemoryStream(package);

            using ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using Stream fileContentsStream = entry.Open();
                byte[] fileBytes = new byte[entry.Length];
                fileContentsStream.Read(fileBytes, 0, (int)entry.Length);
                string fileContents = Encoding.UTF8.GetString(fileBytes);

                packageCollector.Collect(entry.FullName, fileContents);
            }
        }
    }
}

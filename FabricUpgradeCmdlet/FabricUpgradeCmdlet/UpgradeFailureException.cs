// <copyright file="UpgradeFailureException.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradeCmdlet
{
    public class UpgradeFailureException : Exception
    {
        public UpgradeFailureException(string phase)
            : base($"Upgrade failed in {phase} phase.")
        {
        }
    }
}

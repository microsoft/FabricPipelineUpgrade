// <copyright file="UpgradeFailureException.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradePowerShellModule.Exceptions
{
    /// <summary>
    /// A generic exception to throw during Upgrade to terminate the current
    /// Upgrade and return an error message to the client.
    /// </summary>
    public class UpgradeFailureException : Exception
    {
        public UpgradeFailureException(string phase)
            : base($"Upgrade failed in {phase} phase.")
        {
        }
    }
}

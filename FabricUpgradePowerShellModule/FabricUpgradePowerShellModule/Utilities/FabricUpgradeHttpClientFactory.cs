// <copyright file="FabricUpgradeHttpClientFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradePowerShellModule.Utilities
{
    /// <summary>
    /// This class is the "production" implementation of IHttpClientFactory.
    /// </summary>
    public class FabricUpgradeHttpClientFactory : IHttpClientFactory
    {
        /// <inheritdoc/>
        public HttpClient CreateHttpClient()
        {
            // TODO: Reuse one HttpClient for the lifespan of the application.
            return new HttpClient();
        }
    }
}

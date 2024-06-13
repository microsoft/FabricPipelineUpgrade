// <copyright file="FabricUpgradeHttpClientFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradeCmdlet.Utilities
{
    public class FabricUpgradeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateHttpClient()
        {
            // TODO: Reuse one HttpClient for the lifespan of the application.
            return new HttpClient();
        }
    }
}

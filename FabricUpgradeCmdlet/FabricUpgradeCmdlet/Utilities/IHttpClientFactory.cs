// <copyright file="IHttpClientFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradeCmdlet.Utilities
{
    /// <summary>
    /// An interface for a factory that generates HttpClients.
    /// This interface exists to support testing.
    /// </summary>
    public interface IHttpClientFactory
    {
        /// <summary>
        /// Create and return an HttpClient.
        /// </summary>
        /// <returns>An HttpClient.</returns>
        HttpClient CreateHttpClient();
    }
}

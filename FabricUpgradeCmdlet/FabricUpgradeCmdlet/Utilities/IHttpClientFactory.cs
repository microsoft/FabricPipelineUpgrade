// <copyright file="IHttpClientFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradeCmdlet.Utilities
{
    public interface IHttpClientFactory
    {
        HttpClient CreateHttpClient();
    }
}

// <copyright file="Services.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;

namespace FabricUpgradeCmdlet
{
    public class Services
    {
        /// <summary>
        /// We extract this HttpClientFactory into this static element in order to
        /// allow testing of the HTTP calls that PublicApiClient makes.
        /// </summary>
        public static IHttpClientFactory HttpClientFactory = new FabricUpgradeHttpClientFactory();
    }
}

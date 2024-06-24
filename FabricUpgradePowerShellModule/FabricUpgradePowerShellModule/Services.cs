// <copyright file="Services.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Utilities;

namespace FabricUpgradePowerShellModule
{
    /// <summary>
    /// A class to hold some global parameters as singletons.
    /// </summary>
    public class Services
    {
        /// <summary>
        /// We extract this HttpClientFactory into this static element in order to
        /// allow testing of the HTTP calls that PublicApiClient makes.
        /// </summary>
        public static IHttpClientFactory HttpClientFactory = new FabricUpgradeHttpClientFactory();
    }
}

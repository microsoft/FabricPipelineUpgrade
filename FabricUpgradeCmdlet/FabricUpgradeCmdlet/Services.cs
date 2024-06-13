// <copyright file="Services.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;

namespace FabricUpgradeCmdlet
{
    public class Services
    {
        public static IHttpClientFactory HttpClientFactory = new FabricUpgradeHttpClientFactory();
    }
}

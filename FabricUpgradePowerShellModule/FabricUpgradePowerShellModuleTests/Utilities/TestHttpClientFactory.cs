// <copyright file="TestHttpClientFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule;
using FabricUpgradePowerShellModule.Utilities;

namespace FabricUpgradePowerShellModuleTests.Utilities
{
    internal class TestHttpClientFactory : IHttpClientFactory
    {
        private TestApiEndpoints endpoints;

        public TestHttpClientFactory(TestApiEndpoints endpoints)
        {
            this.endpoints = endpoints;
        }

        public HttpClient CreateHttpClient()
        {
            return new HttpClient(new Responder(this.endpoints));
        }

        public static void RegisterTestHttpClientFactory(TestApiEndpoints endpoints)
        {
            Services.HttpClientFactory = new TestHttpClientFactory(endpoints);
        }

        private class Responder : DelegatingHandler
        {
            private TestApiEndpoints endpoints;

            public Responder(TestApiEndpoints endpoints)
            {
                this.endpoints = endpoints;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await this.endpoints.HandleRequestAsync(request).ConfigureAwait(false);
            }
        }
    }

}

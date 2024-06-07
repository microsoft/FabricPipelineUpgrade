using FabricUpgradeCmdlet;
using FabricUpgradeCmdlet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeTests.Utilities
{
    internal class TestHttpClientFactory : IHttpClientFactory
    {
        private TestPublicApiEndpoints endpoints;

        public TestHttpClientFactory(TestPublicApiEndpoints endpoints)
        {
            this.endpoints = endpoints;
        }

        public HttpClient CreateHttpClient()
        {
            return new HttpClient(new Responder(this.endpoints));
        }

        public static void RegisterTestHttpClientFactory(TestPublicApiEndpoints endpoints)
        {
            Services.HttpClientFactory = new TestHttpClientFactory(endpoints);
        }

        private class Responder : DelegatingHandler
        {
            private TestPublicApiEndpoints endpoints;

            public Responder(TestPublicApiEndpoints endpoints)
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

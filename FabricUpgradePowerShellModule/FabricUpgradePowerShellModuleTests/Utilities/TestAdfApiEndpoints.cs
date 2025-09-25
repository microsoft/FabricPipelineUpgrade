// <copyright file="TestAdfApiEndpoints.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModuleTests.Utilities
{
    public class TestAdfApiEndpoints : TestApiEndpoints
    {
        private JObject storedArtifacts; 

        public TestAdfApiEndpoints(
            string adfApiBaseUrl)
        {
        }

        public TestAdfApiEndpoints PreLoadArtifacts(JObject adfArtifacts)
        {
            this.storedArtifacts = adfArtifacts;
            return this;
        }

        public override Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request)
        {
            throw new NotImplementedException();
        }
    }
}

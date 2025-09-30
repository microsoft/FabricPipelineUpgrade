// <copyright file="TestAdfApiEndpoints.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;

using System.Net;
using System.Text.RegularExpressions;

namespace FabricUpgradePowerShellModuleTests.Utilities
{
    public class TestAdfApiEndpoints : TestApiEndpoints
    {
        private readonly Regex getFactoryResource;
        private readonly Regex getArtifacts;
        private readonly Regex getArtifact;
        private JObject storedArtifacts; 

        public TestAdfApiEndpoints(
            string adfApiBaseUrl, string apiVersion)
        {
            this.getFactoryResource = new Regex(
                $"^GET {adfApiBaseUrl}subscriptions/(?'subscriptionId'[^/]+)/resourceGroups/(?'resourceGroupName'[^/]+)/providers/Microsoft.DataFactory/factories/(?'factoryName'[^/]+)[\\?]api-version={apiVersion}$",
                RegexOptions.IgnoreCase);
            this.getArtifacts = new Regex(
                $"^GET {adfApiBaseUrl}subscriptions/(?'subscriptionId'[^/]+)/resourceGroups/(?'resourceGroupName'[^/]+)/providers/Microsoft.DataFactory/factories/(?'factoryName'[^/]+)/(?'artifactType'pipelines|linkedservices|datasets|triggers)[\\?]api-version={apiVersion}$",
                RegexOptions.IgnoreCase);
            this.getArtifact = new Regex(
                $"^GET {adfApiBaseUrl}subscriptions/(?'subscriptionId'[^/]+)/resourceGroups/(?'resourceGroupName'[^/]+)/providers/Microsoft.DataFactory/factories/(?'factoryName'[^/]+)/(?'artifactType'pipelines|linkedservices|datasets|triggers)/(?'artifactName'[^/]+)[\\?]api-version={apiVersion}$",
                RegexOptions.IgnoreCase);
        }

        public TestAdfApiEndpoints PreLoadArtifacts(JObject adfArtifacts)
        {
            this.storedArtifacts = adfArtifacts;
            return this;
        }

        public override async Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request)
        {
            string requestPayload = null;
            if (request.Content != null)
            {
                requestPayload = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            string actualUserToken = null;
            if (request.Headers.TryGetValues("Authorization", out var authHeaderValues))
            {
                actualUserToken = authHeaderValues.FirstOrDefault();
            }

            if (this.requiredUserToken != null)
            {
                if (actualUserToken != "Bearer " + this.requiredUserToken)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("token mismatch"),
                    };
                }
            }

            string routeKey = $"{request.Method} {request.RequestUri}";
            var getArtifacts = this.getArtifacts.Matches(routeKey);
            if (getArtifacts.Count == 1)
            {
                string artifactType = getArtifacts[0].Groups["artifactType"].Value;
                return this.GetArtifactsByType(artifactType);
            }

            var getFactoryResource = this.getFactoryResource.Matches(routeKey);
            if (getFactoryResource.Count == 1)
            {
                return this.GetFactoryResource();
            }

            var getArtifact = this.getArtifact.Matches(routeKey);
            if (getArtifact.Count == 1)
            {
                string artifactType = getArtifact[0].Groups["artifactType"].Value;
                string artifactName = getArtifact[0].Groups["artifactName"].Value;
                return this.GetArtifactByTypeAndName(artifactType, artifactName);
            }

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("okay"),
            };
            response.Headers.Add("x-ms-random", "1234");

            return response;
        }

        private HttpResponseMessage GetArtifactsByType(string artifactType)
        {
            IEnumerable<JObject> matchingItems = this.storedArtifacts[artifactType]
                .Where(p => p["id"].ToString() != null)
                .Cast<JObject>();
            JObject responsePayload = new JObject();
            JArray value = new JArray();
            responsePayload["value"] = value;

            foreach (JObject item in matchingItems)
            {
                value.Add(item);
            }

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload.ToString()),
            };
            this.events.Add($"GET {artifactType}");

            return response;
        }

        private HttpResponseMessage GetArtifactByTypeAndName(string artifactType, string artifactName)
        {
            JToken artifact = this.storedArtifacts[artifactType]
                .Single(p => p.SelectToken("$.name").ToString() == artifactName);
            JObject responsePayload = (JObject)artifact;
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload.ToString()),
            };
            this.events.Add($"GET {artifactType} '{artifactName}'");

            return response;
        }

        private HttpResponseMessage GetFactoryResource()
        {
            JObject responsePayload = (JObject)this.storedArtifacts["factory"];
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload.ToString()),
            };
            this.events.Add($"GET Factory");

            return response;
        }
    }
}

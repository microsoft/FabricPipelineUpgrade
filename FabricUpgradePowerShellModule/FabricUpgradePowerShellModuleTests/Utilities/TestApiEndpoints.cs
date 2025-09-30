// <copyright file="TestApiEndpoints.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Net;

namespace FabricUpgradePowerShellModuleTests.Utilities
{
    public abstract class TestApiEndpoints
    {
        protected List<string> events = new List<string>();
        protected readonly Dictionary<string, HttpStatusCode> responseStatusCodes = new Dictionary<string, HttpStatusCode>();
        protected readonly Dictionary<string, string> responsePayloads = new Dictionary<string, string>();        

        // If this is not null, then requests to PublicAPI endpoints
        // must include this token. It is set in RequireUserToken().
        protected string requiredUserToken = null;

        public List<Tuple<HttpRequestMessage, string>> Requests { get; protected set; } = new List<Tuple<HttpRequestMessage, string>>();

        /// <summary>
        /// All requests to PublicAPI endpoints must include this Bearer user token.
        /// This requirement verifies that the PublicAPI endpoints are invoked with
        /// the user's AAD token.
        /// </summary>
        /// <param name="userToken">The user's AAD token.</param>
        /// <returns>this, for chaining.</returns>
        public TestApiEndpoints RequireUserToken(string userToken)
        {
            this.requiredUserToken = userToken;
            return this;
        }

        public TestApiEndpoints PrepareResponse(
            HttpMethod method,
            string requestUrl,
            HttpStatusCode statusCode,
            string responsePayload)
        {
            string routeKey = $"{method} {requestUrl}";
            this.responseStatusCodes[routeKey] = statusCode;
            this.responsePayloads[routeKey] = responsePayload;
            return this;
        }

        public List<string> FetchEvents()
        {
            return new List<string>(this.events);
        }

        public abstract Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request);
    }
}
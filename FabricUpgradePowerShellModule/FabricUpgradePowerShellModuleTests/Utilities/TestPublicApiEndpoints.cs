// <copyright file="TestPublicApiEndpoints.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Text.RegularExpressions;

namespace FabricUpgradePowerShellModuleTests.Utilities
{
    public class TestPublicApiEndpoints
    {
        private List<string> events = new List<string>();

        private readonly Dictionary<string, HttpStatusCode> responseStatusCodes = new Dictionary<string, HttpStatusCode>();
        private readonly Dictionary<string, string> responsePayloads = new Dictionary<string, string>();

        private readonly Regex listItemsRoute;
        private readonly Regex createItemRoute;
        private readonly Regex updateItemRoute;
        private readonly Regex updateItemDefinitionRoute;

        private readonly List<JObject> storedItems = new List<JObject>();
        private readonly Dictionary<string, JObject> storedItemDefinitions = new Dictionary<string, JObject>();

        private readonly List<string> reservedDisplayNames = new List<string>();

        private List<Guid> guids = new List<Guid>();

        // If this is not null, then requests to PublicAPI endpoints
        // must include this token. It is set in RequireUserToken().
        private string requiredUserToken = null;

        // If true, then the PublicAPI writes the CreateItem payload
        // to a file for manual validation.
        private bool writeCreateFile = false;

        public TestPublicApiEndpoints(
            string publicApiBaseUrl)
        {
            this.listItemsRoute = new Regex(
                $"^GET {publicApiBaseUrl}workspaces/(?'workspaceId'[^/]+)/items[\\?]type=(?'itemType'[^\\&]+)");

            this.createItemRoute = new Regex(
                $"^POST {publicApiBaseUrl}workspaces/(?'workspaceId'[^/]+)/items(\\?|$)");

            this.updateItemRoute = new Regex(
                $"^PATCH {publicApiBaseUrl}workspaces/(?'workspaceId'[^/]+)/items/(?'itemId'[^\\?]+)");

            this.updateItemDefinitionRoute = new Regex(
                $"^POST {publicApiBaseUrl}workspaces/(?'workspaceId'[^/]+)/items/(?'itemId'[^/]+)/updateDefinition(\\?|$)");
        }

        public List<Tuple<HttpRequestMessage, string>> Requests { get; private set; } = new List<Tuple<HttpRequestMessage, string>>();

        /// <summary>
        /// All requests to PublicAPI endpoints must include this Bearer user token.
        /// This requirement verifies that the PublicAPI endpoints are invoked with
        /// the user's AAD token.
        /// </summary>
        /// <param name="userToken">The user's AAD token.</param>
        /// <returns>this, for chaining.</returns>
        public TestPublicApiEndpoints RequireUserToken(string userToken)
        {
            this.requiredUserToken = userToken;
            return this;
        }

        public TestPublicApiEndpoints WriteCreateFile(bool doWrite = true)
        {
            this.writeCreateFile = doWrite;
            return this;
        }

        public TestPublicApiEndpoints PrepareResponse(
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

        public TestPublicApiEndpoints PrepareGuids(
            List<Guid> guids)
        {
            this.guids = new List<Guid>(guids);

            return this;
        }

        private Guid GenerateNextGuid()
        {
            Guid newItemId;
            if (this.guids.Count > 0)
            {
                newItemId = this.guids[0];
                this.guids.RemoveAt(0);
            }
            else
            {
                newItemId = Guid.NewGuid();
            }

            return newItemId;
        }

        /// <summary>
        /// Add an artifact to the test endpoints, to induce an Update.
        /// </summary>
        /// <param name="prestocks"></param>
        /// <returns></returns>
        public TestPublicApiEndpoints Prestock(JArray prestocks)
        {
            foreach (JToken prestock in prestocks)
            {
                string itemId = this.GenerateNextGuid().ToString();

                prestock["id"] = itemId;
                this.storedItems.Add((JObject)prestock);

                this.storedItemDefinitions[itemId] = new JObject();
            }

            return this;
        }

        /// <summary>
        /// If a client attempts to create an item with this name, return an
        /// "ItemDisplayNameAlreadyInUse" error. This emulates a poor behavior
        /// by the 'real' backend.
        /// </summary>
        /// <param name="displayName">The display name to reserve.</param>
        /// <returns>this, for chaining.</returns>
        public TestPublicApiEndpoints ReserveDisplayName(string displayName)
        {
            this.reservedDisplayNames.Add(displayName);
            return this;
        }

        public Tuple<int, int> CountItems()
        {
            return Tuple.Create(this.storedItems.Count, this.storedItemDefinitions.Count);
        }

        public JObject ReadItemDirectly(
            Guid workspaceId,
            Guid itemId)
        {
            IEnumerable<JObject> matchingItems = this.storedItems.Where(
                i => i.SelectToken("$.workspaceId").ToString() == workspaceId.ToString() &&
                        i.SelectToken("$.id").ToString() == itemId.ToString());

            if (matchingItems.Count() == 0)
            {
                return null;
            }

            JObject itemObject = new JObject();
            itemObject["item"] = matchingItems.First();

            itemObject["definition"] = this.storedItemDefinitions[itemId.ToString()];
            return itemObject;
        }

        public List<string> FetchEvents()
        {
            return new List<string>(this.events);
        }

        public async Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request)
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

            this.Requests.Add(Tuple.Create(request, requestPayload));

            string routeKey = $"{request.Method} {request.RequestUri}";

            // Let's see if we have a prepared override response to this query.
            if (this.responseStatusCodes.ContainsKey(routeKey))
            {
                HttpStatusCode statusCode = this.responseStatusCodes[routeKey];
                string responsePayload = this.responsePayloads[routeKey];

                if (responsePayload == null)
                {
                    return new HttpResponseMessage(statusCode);
                }

                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responsePayload),
                };
            }

            // We do not have a prepared response, so let's proceed normally.
            var listItemMatches = this.listItemsRoute.Matches(routeKey);
            if (listItemMatches.Count == 1)
            {
                string workspaceId = listItemMatches[0].Groups["workspaceId"].Value;
                string itemType = listItemMatches[0].Groups["itemType"].Value;

                return this.ListItems(workspaceId, itemType);
            }

            var createItemMatches = this.createItemRoute.Matches(routeKey);
            if (createItemMatches.Count == 1)
            {
                string workspaceId = createItemMatches[0].Groups["workspaceId"].Value;
                return this.CreateItem(workspaceId, requestPayload);
            }

            var updateItemMatches = this.updateItemRoute.Matches(routeKey);
            if (updateItemMatches.Count == 1)
            {
                string workspaceId = updateItemMatches[0].Groups["workspaceId"].Value;
                string itemId = updateItemMatches[0].Groups["itemId"].Value;
                return this.UpdateItem(workspaceId, itemId, requestPayload);
            }

            var updateItemDefinitionMatches = this.updateItemDefinitionRoute.Matches(routeKey);
            if (updateItemDefinitionMatches.Count == 1)
            {
                string workspaceId = updateItemDefinitionMatches[0].Groups["workspaceId"].Value;
                string itemId = updateItemDefinitionMatches[0].Groups["itemId"].Value;
                return this.UpdateItemDefinition(workspaceId, itemId, requestPayload);
            }

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("okay"),
            };
            response.Headers.Add("x-ms-random", "1234");

            return response;
        }

        private HttpResponseMessage ListItems(string workspaceId, string itemType)
        {
            IEnumerable<JObject> matchingItems = this.storedItems.Where(
                i => i.SelectToken("$.workspaceId").ToString() == workspaceId &&
                        i.SelectToken("$.type").ToString() == itemType);

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

            this.events.Add($"LIST {itemType}");

            return response;
        }

        private HttpResponseMessage CreateItem(string workspaceId, string requestPayload)
        {
            PublicApiCreateItemRequestModel createItemPayload =
                JsonConvert.DeserializeObject<PublicApiCreateItemRequestModel>(requestPayload);

            if (this.writeCreateFile)
            {
                File.WriteAllText("D:\\UploadMe.txt", requestPayload);
            }

            if (createItemPayload.Definition.Parts.Count < 1)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (createItemPayload.Definition.Parts[0].PayloadType != "InlineBase64")
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (this.reservedDisplayNames.Contains(createItemPayload.DisplayName))
            {
                JObject errorResponse = new JObject()
                {
                    ["requestId"] = Guid.NewGuid().ToString(),
                    ["errorCode"] = "ItemDisplayNameAlreadyInUse",
                    ["message"] = $"Requested '{createItemPayload.DisplayName}' is already in use",
                };

                this.events.Add($"CREATE {createItemPayload.ItemType} '{createItemPayload.DisplayName}' DisplayNameAlreadyInUse");

                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(errorResponse.ToString()),
                };
            }

            Guid newItemId = this.GenerateNextGuid();

            JObject itemModel = new JObject();
            itemModel["type"] = createItemPayload.ItemType.ToString();
            itemModel["displayName"] = createItemPayload.DisplayName;
            itemModel["description"] = createItemPayload.Description;
            itemModel["workspaceId"] = workspaceId;
            itemModel["id"] = newItemId.ToString();

            this.storedItems.Add(itemModel);

            string itemDefinition64 = createItemPayload.Definition.Parts[0].Payload;
            byte[] itemDefinitionBytes = System.Convert.FromBase64String(itemDefinition64);
            string itemDefinition = System.Text.Encoding.UTF8.GetString(itemDefinitionBytes);
            this.storedItemDefinitions[newItemId.ToString()] = JObject.Parse(itemDefinition);

            this.events.Add($"CREATE {createItemPayload.ItemType} '{createItemPayload.DisplayName}' => {newItemId}");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(UpgradeSerialization.Serialize(itemModel)),
            };
        }

        private HttpResponseMessage UpdateItem(
            string workspaceId,
            string itemId,
            string requestPayload)
        {
            JObject payloadObject = JObject.Parse(requestPayload);
            string displayName = payloadObject.SelectToken("$.displayName")?.ToString();
            string description = payloadObject.SelectToken($"description")?.ToString();

            IEnumerable<JObject> matchingItems = this.storedItems.Where(
                i => i.SelectToken("$.workspaceId").ToString() == workspaceId &&
                        i.SelectToken("$.id").ToString() == itemId);

            if (matchingItems.Count() != 1)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            JObject matchingItem = matchingItems.First();

            matchingItem["displayName"] = displayName;
            matchingItem["description"] = description;

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore,
            };

            string responsePayload = JsonConvert.SerializeObject(matchingItem, serializerSettings);

            this.events.Add($"UPDATE ITEM {matchingItem.SelectToken("type")} '{displayName}' @ {itemId}");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload),
            };
        }

        private HttpResponseMessage UpdateItemDefinition(
            string workspaceId,
            string itemId,
            string requestPayload)
        {
            JObject payloadObject = JObject.Parse(requestPayload);

            JObject definitionPart = (JObject)((JArray)payloadObject.SelectToken("$.definition.parts"))[0];

            if (definitionPart.SelectToken("$.payloadType").ToString() != "InlineBase64")
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            string itemDefinition64 = definitionPart.SelectToken("$.payload").ToString();

            byte[] itemDefinitionBytes = System.Convert.FromBase64String(itemDefinition64);
            string itemDefinition = System.Text.Encoding.UTF8.GetString(itemDefinitionBytes);
            this.storedItemDefinitions[itemId.ToString()] = JObject.Parse(itemDefinition);

            this.events.Add($"UPDATE ITEM DEFINITION {itemId}");

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private class PublicApiCreateItemRequestModel
        {
            [JsonProperty("type")]
            public string ItemType { get; set; }

            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
            public string Description { get; set; }

            [JsonProperty("definition")]
            public PublicApiItemDefinitionModel Definition { get; } = new PublicApiItemDefinitionModel();
        }

        private class PublicApiItemDefinitionModel
        {
            [JsonProperty("parts")]
            public List<PublicApiItemDefinitionPartModel> Parts { get; } = new List<PublicApiItemDefinitionPartModel>();
        }

        private class PublicApiItemDefinitionPartModel
        {
            [JsonProperty("path")]
            public string Path { get; set; }

            [JsonProperty("payloadType")]
            public string PayloadType { get; set; }

            [JsonProperty("payload")]
            public string Payload { get; set; }
        }
    }

}

// <copyright file="PublicApiClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using FabricUpgradeCmdlet.Exceptions;
using FabricUpgradeCmdlet.Utilities;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet
{
    /// <summary>
    /// This client interacts with the Public API endpoints.
    /// </summary>
    /// <remarks>
    /// This class can be used by a system that wants to send HTTP requests to an Public API endpoint.
    /// </remarks>
    public class PublicApiClient
    {
        // This "Path" component is sent as part of a Create or Update call.
        // Different Artifacts will use different Paths.
        // You can find these "paths" in the appropriate ArtifactAlmHandler.
        private static readonly Dictionary<FabricUpgradeResourceTypes, string> ArtifactAlmPaths = new Dictionary<FabricUpgradeResourceTypes, string>()
        {
            { FabricUpgradeResourceTypes.DataPipeline, "pipeline-content.json" },
        };

        // To prevent port exhaustion, we need to reuse the HttpMessageHandler that we use in our HttpClients.
        // This static object will be used to create the HttpClient that we use to make HTTP calls,
        // so that we use the same HttpMessageHandler each time.
        private static readonly HttpClientHandler ReusableClientHandler = new HttpClientHandler()
        {
            CheckCertificateRevocationList = true,
        };

        private readonly string cluster;
        private readonly string workspaceId;
        private readonly string pbiAadToken;

        public PublicApiClient(
            string cluster,
            string workspaceId,
            string pbiAadToken)
        {
            this.cluster = cluster;
            this.workspaceId = workspaceId;
            this.pbiAadToken = pbiAadToken;
        }

        /// <inheritdoc/>
        public async Task<string> CreateOrUpdateArtifactAsync(
            FabricUpgradeResourceTypes artifactType,
            string displayName,
            string description,
            JObject payload,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!ArtifactAlmPaths.ContainsKey(artifactType))
                {
                    throw new Exception("Cannot upgrade artifact of this type");
                }

                Guid? existingArtifactId = await this.GetExistingArtifactIdAsync(
                    artifactType,
                    displayName,
                    cancellationToken).ConfigureAwait(false);

                if (existingArtifactId == null)
                {
                    return await this.CreateArtifactAsync(
                        artifactType,
                        displayName,
                        description,
                        payload,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return await this.UpdateArtifactAsync(
                        existingArtifactId.Value,
                        artifactType,
                        displayName,
                        description,
                        payload,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
            }
        }

        /// <summary>
        /// List all the Artifacts of the specified type.
        /// </summary>
        /// <param name="artifactType">The type of Artifact to list.</param>
        /// <param name="cancellationToken">The cancellationToken.</param>
        /// <returns>A JArray of the Artifacts.</returns>
        private async Task<JArray> ListArtifactsAsync(
            FabricUpgradeResourceTypes artifactType,
            CancellationToken cancellationToken)
        {
            try
            {
                HttpClient httpClient = this.BuildPublicApiHttpClient();

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"workspaces/{this.workspaceId}/items?type={artifactType}");
                request.Headers.Add("Authorization", $"Bearer {this.pbiAadToken}");

                HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessages = this.BuildErrorMessages(
                        "ListItems",
                        artifactType,
                        null,
                        response.StatusCode,
                        responsePayload);

                    throw new Exception(errorMessages.ExceptionMessage);
                }

                try
                {
                    JObject payloadObject = JObject.Parse(responsePayload);
                    return (JArray)payloadObject.SelectToken("$.value") ?? new JArray();
                }
                catch (JsonException)
                {
                    throw new Exception($"Received unparseable response payload\n{responsePayload}\nfrom PublicApi.ListItems");
                }
            }
            finally
            {
            }
        }

        /// <summary>
        /// Determine if an Artifact of this type and display name already exists.
        /// If it does, return its GUID; otherwise return null.
        /// </summary>
        /// <remarks>
        /// This method is impeded by the fact that the Public API does not let us
        /// read the "definition" of the Artifact.
        /// </remarks>
        /// <param name="artifactType">The type of the Artifact.</param>
        /// <param name="displayName">The displayName of the Artifact.</param>
        /// <param name="cancellationToken">The CancellationToken.</param>
        /// <returns>The GUID of the existing Artifact; null if no such Artifact exists.</returns>
        private async Task<Guid?> GetExistingArtifactIdAsync(
            FabricUpgradeResourceTypes artifactType,
            string displayName,
            CancellationToken cancellationToken)
        {
            try
            {
                JArray existingArtifacts = await this.ListArtifactsAsync(artifactType, cancellationToken).ConfigureAwait(false);

                foreach (JToken existingArtifactToken in existingArtifacts)
                {
                    if (existingArtifactToken.Type != JTokenType.Object)
                    {
                        continue;
                    }

                    JObject existingArtifact = (JObject)existingArtifactToken;

                    string existingArtifactName = existingArtifact.SelectToken("$.displayName")?.ToString();
                    if (existingArtifactName == displayName)
                    {
                        string existingArtifactId = existingArtifact.SelectToken("$.id")?.ToString();
                        if (string.IsNullOrEmpty(existingArtifactId) || !Guid.TryParse(existingArtifactId, out Guid existingArtifactGuid))
                        {
                            throw new Exception($"Failed to parse GUID '{existingArtifactId ?? "<null>"}' in response from PublicApi.ListItems");
                        }

                        return existingArtifactGuid;
                    }
                }

                return null;
            }
            finally
            {
            }
        }

        /// <summary>
        /// Create an artifact.
        /// </summary>
        /// <param name="artifactType">The type of artifact to create.</param>
        /// <param name="displayName">The display name of the artifact.</param>
        /// <param name="description">The description of the artifact.</param>
        /// <param name="payload">The payload to apply to the artifact.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response from the PublicAPI.</returns>
        private async Task<string> CreateArtifactAsync(
            FabricUpgradeResourceTypes artifactType,
            string displayName,
            string description,
            JObject payload,
            CancellationToken cancellationToken)
        {
            try
            {
                HttpClient httpClient = this.BuildPublicApiHttpClient();

                HttpRequestMessage request = this.BuildCreateItemRequestMessage(artifactType, displayName, description, payload);

                HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessages = this.BuildErrorMessages(
                        "CreateItem",
                        artifactType,
                        displayName,
                        response.StatusCode,
                        responsePayload);

                    if (errorMessages.ErrorCode == PublicApiErrorModel.ItemDisplayNameAlreadyInUse)
                    {
                        // TODO: Find a better way of handling this?
                        // Maybe try a different display name, and alert the caller?
                        throw new ItemDisplayNameAlreadyInUseException(displayName);
                    }

                    throw new Exception(errorMessages.ExceptionMessage);
                }

                return responsePayload;
            }
            finally
            {
            }
        }

        /// <summary>
        /// Update an artifact.
        /// </summary>
        /// <remarks>
        /// There are two steps: UpdateItem and UpdateItemDescription.
        /// </remarks>
        /// <param name="existingArtifactId">The ID of the artifact to update.</param>
        /// <param name="artifactType">The type of artifact to update (just for logging).</param>
        /// <param name="displayName">The display name of the artifact.</param>
        /// <param name="description">The description of the artifact.</param>
        /// <param name="payload">The payload to apply to the artifact.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response from the PublicAPI.</returns>
        private async Task<string> UpdateArtifactAsync(
            Guid existingArtifactId,
            FabricUpgradeResourceTypes artifactType,
            string displayName,
            string description,
            JObject payload,
            CancellationToken cancellationToken)
        {
            try
            {
                HttpClient httpClient = this.BuildPublicApiHttpClient();

                string itemModel = await this.UpdateItemAsync(
                    httpClient,
                    artifactType,
                    existingArtifactId,
                    displayName,
                    description,
                    cancellationToken).ConfigureAwait(false);

                await this.UpdateItemDefinitionAsync(
                    httpClient,
                    artifactType,
                    existingArtifactId,
                    displayName,
                    payload,
                    cancellationToken).ConfigureAwait(false);

                return itemModel;
            }
            finally
            {
            }
        }

        /// <summary>
        /// Update an Item via Public API.
        /// </summary>
        /// <remarks>
        /// This updates only the displayName and description of the item.
        /// </remarks>
        /// <param name="httpClient">The HttpClient to use.</param>
        /// <param name="existingArtifactId">The ID of the artifact.</param>
        /// <param name="artifactType">The type of the artifact.</param>
        /// <param name="displayName">The displayName of the artifact.</param>
        /// <param name="description">The description of the artifact.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response from the PublicAPI.</returns>
        private async Task<string> UpdateItemAsync(
            HttpClient httpClient,
            FabricUpgradeResourceTypes artifactType,
            Guid existingArtifactId,
            string displayName,
            string description,
            CancellationToken cancellationToken)
        {
            HttpRequestMessage updateItemRequest = this.BuildUpdateItemRequestMessage(artifactType, existingArtifactId, displayName, description);

            HttpResponseMessage updateItemResponse = await httpClient.SendAsync(updateItemRequest, cancellationToken).ConfigureAwait(false);
            string updateItemResponsePayload = await updateItemResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!updateItemResponse.IsSuccessStatusCode)
            {
                var errorMessages = this.BuildErrorMessages(
                    "UpdateItem",
                    artifactType,
                    displayName,
                    updateItemResponse.StatusCode,
                    updateItemResponsePayload);

                throw new Exception(errorMessages.ExceptionMessage);
            }

            return updateItemResponsePayload;
        }

        /// <summary>
        /// Update an ItemDefinition via Public API.
        /// </summary>
        /// <remarks>
        /// This updates only the payload of the item.
        /// </remarks>
        /// <param name="httpClient">The HttpClient to use.</param>
        /// <param name="artifactType">The type of the artifact.</param>
        /// <param name="existingArtifactId">The ID of the artifact.</param>
        /// <param name="displayName">The displayName of the artifact.</param>
        /// <param name="payload">The payload to apply to the artifact.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The ID of the updated artifact.</returns>
        private async Task UpdateItemDefinitionAsync(
            HttpClient httpClient,
            FabricUpgradeResourceTypes artifactType,
            Guid existingArtifactId,
            string displayName,
            JObject payload,
            CancellationToken cancellationToken)
        {
            HttpRequestMessage updateItemDefinitionRequest = this.BuildUpdateItemDefinitionRequestMessage(
                artifactType,
                existingArtifactId,
                payload);

            HttpResponseMessage updateItemDefinitionResponse = await httpClient.SendAsync(updateItemDefinitionRequest, cancellationToken).ConfigureAwait(false);

            if (!updateItemDefinitionResponse.IsSuccessStatusCode)
            {
                var errorMessages = this.BuildErrorMessages(
                    "UpdateItemDefinition",
                    artifactType,
                    displayName,
                    updateItemDefinitionResponse.StatusCode,
                    null);

                throw new Exception(errorMessages.ExceptionMessage);
            }
        }

        private HttpRequestMessage BuildCreateItemRequestMessage(
            FabricUpgradeResourceTypes artifactType,
            string displayName,
            string description,
            JObject payload)
        {
            string payload64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToString()));

            PublicApiCreateItemRequestModel createItemPayload = new PublicApiCreateItemRequestModel()
            {
                ItemType = artifactType,
                DisplayName = displayName,
                Description = description,
            };

            createItemPayload.Definition.Parts.Add(new PublicApiItemDefinitionPartModel()
            {
                Path = ArtifactAlmPaths[artifactType],
                PayloadType = "InlineBase64",
                Payload = payload64,
            });

            HttpMethod httpMethod = HttpMethod.Post;
            string relativeUrl = $"workspaces/{this.workspaceId}/items";

            HttpRequestMessage request = new HttpRequestMessage(httpMethod, relativeUrl)
            {
                Content = new StringContent(createItemPayload.ToString(), encoding: Encoding.UTF8, mediaType: "application/json"),
            };

            request.Headers.Add("Authorization", $"Bearer {this.pbiAadToken}");

            return request;
        }

        private HttpRequestMessage BuildUpdateItemRequestMessage(
            FabricUpgradeResourceTypes artifactType,
            Guid existingArtifactId,
            string displayName,
            string description)
        {
            PublicApiUpdateItemRequestModel updateItemPayload = new PublicApiUpdateItemRequestModel()
            {
                DisplayName = displayName,
                Description = description,
            };

            HttpMethod httpMethod = new HttpMethod("PATCH");
            string relativeUrl = $"workspaces/{this.workspaceId}/items/{existingArtifactId}";

            HttpRequestMessage request = new HttpRequestMessage(httpMethod, relativeUrl)
            {
                Content = new StringContent(updateItemPayload.ToString(), encoding: Encoding.UTF8, mediaType: "application/json"),
            };

            request.Headers.Add("Authorization", $"Bearer {this.pbiAadToken}");

            return request;
        }

        private HttpRequestMessage BuildUpdateItemDefinitionRequestMessage(
            FabricUpgradeResourceTypes artifactType,
            Guid existingArtifactId,
            JObject payload)
        {
            string payload64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToString()));

            PublicApiUpdateItemDefinitionRequestModel updateItemDefinitionPayload = new PublicApiUpdateItemDefinitionRequestModel();

            updateItemDefinitionPayload.Definition.Parts.Add(new PublicApiItemDefinitionPartModel()
            {
                Path = ArtifactAlmPaths[artifactType],
                PayloadType = "InlineBase64",
                Payload = payload64,
            });

            HttpMethod httpMethod = HttpMethod.Post;
            string relativeUrl = $"workspaces/{this.workspaceId}/items/{existingArtifactId}/updateDefinition";

            HttpRequestMessage request = new HttpRequestMessage(httpMethod, relativeUrl)
            {
                Content = new StringContent(updateItemDefinitionPayload.ToString(), encoding: Encoding.UTF8, mediaType: "application/json"),
            };

            request.Headers.Add("Authorization", $"Bearer {this.pbiAadToken}");

            return request;
        }

        private PublicApiItemModel ParseItemModelPayload(
            string responsePayload,
            string operationName)
        {
            try
            {
                return SafeJsonConvert.DeserializeObject<PublicApiItemModel>(responsePayload);
            }
            catch (JsonException)
            {
                throw new Exception($"Received unparseable response payload\n{responsePayload}\nfrom PublicApi.{operationName}");
            }
        }

        /// <summary>
        /// Build an HTTP client that talks to the Public API.
        /// </summary>
        /// <returns>The HttpClient.</returns>
        private HttpClient BuildPublicApiHttpClient()
        {
            IHttpClientFactory httpClientFactory = Services.HttpClientFactory;

            // Protect our HttpClientHandler by wrapping it in a SkipDisposeDelegatingHandler.
            // This will prevent the HttpClient from disposing our HttpClientHandler when we dispose the HttpClient.
            // Also, add a retry handler that retries SocketExceptions.
            HttpClient httpClient = httpClientFactory.CreateHttpClient();

            string publicApiBaseUrl = this.ComputePublicApiBaseUrl();

            httpClient.BaseAddress = new Uri(publicApiBaseUrl);

            return httpClient;
        }

        /// <summary>
        /// Find the PublicApiBaseUrl from the WorkloadParameters.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>the PublicApiBaseUrl for this region.</returns>
        private string ComputePublicApiBaseUrl()
        {
            return this.cluster switch
            {
                "daily" => "https://dailyapi.fabric.microsoft.com/v1/",
                "dxt" => "https://dxtapi.fabric.microsoft.com/v1",
                "msit" => "https://msitapi.fabric.microsoft.com/v1",
                "prod" => "https://api.fabric.microsoft.com/v1",
                _ => "https://api.fabric.microsoft.com/v1",
            };
        }

        /// <summary>
        /// Extract from a PublicApi error message a log message, an exception message, and the error code.
        /// </summary>
        /// <remarks>
        /// Rather than logging and throwing directly, this method just builds the strings to log and throw.
        /// This ensures that:
        /// the log message is from the point of failure,
        /// the callstack shows the point of failure,
        /// the caller can handle different error codes differently.
        /// </remarks>
        /// <param name="operation">What is the caller doing.</param>
        /// <param name="artifactType">The type of artifact.</param>
        /// <param name="displayName">The displayName of the artifact, if there is one.</param>
        /// <param name="statusCode">The status code from PublicApi.</param>
        /// <param name="responsePayload">The response payload from PublicApi, if there is one.</param>
        /// <returns>The error code, a log message, and an exception message.</returns>
        private (string ErrorCode, string LogMessage, string ExceptionMessage) BuildErrorMessages(
            string operation,
            FabricUpgradeResourceTypes artifactType,
            string displayName,
            HttpStatusCode statusCode,
            string responsePayload)
        {
            PublicApiErrorModel errorModel = PublicApiErrorModel.FromString(responsePayload);
            string errorCode = errorModel?.ErrorCode;

            // Build the log message
            string logMessage = $"{operation}({artifactType}" +
                (displayName == null ? string.Empty : $", {displayName}") +
                $") returned status code {statusCode}" +
                (errorCode == null ? string.Empty : $" and error code {errorCode}");

            // Build the exception message.
            string exceptionMessage = $"{operation} for {artifactType}" +
                (displayName == null ? string.Empty : $" '{displayName}'") +
                $" returned unexpected status code {statusCode}" +
                (errorCode == null ? string.Empty : $" with error code {errorCode}");

            return (errorCode, logMessage, exceptionMessage);
        }

        /// <summary>
        /// This is the model for the error response from PublicApi endpoints.
        /// </summary>
        /// <remarks>
        /// Strangely, this error model does not seem to be exposed anywhere in code.
        /// Maybe the authors assumed that the PublicApi would be used only by external clients.
        /// </remarks>
        public class PublicApiErrorModel
        {
            // From Microsoft.PowerBI.ServiceContracts.Api.Public.Trident.TridentPublicApiErrorCodes
            public const string ItemDisplayNameAlreadyInUse = "ItemDisplayNameAlreadyInUse";

            [JsonProperty("requestId")]
            public string RequestId { get; set; }

            [JsonProperty("errorCode")]
            public string ErrorCode { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            public static PublicApiErrorModel FromString(string errorString)
            {
                if (errorString == null)
                {
                    return null;
                }

                try
                {
                    return SafeJsonConvert.DeserializeObject<PublicApiErrorModel>(errorString);
                }
                catch (JsonException)
                {
                    return new PublicApiErrorModel()
                    {
                        Message = errorString,
                    };
                }
            }
        }

        /// <summary>
        /// The Public API model for the CreateItem request payload.
        /// </summary>
        /// <remarks>
        /// https://learn.microsoft.com/en-us/rest/api/fabric/core/items/create-item?tabs=HTTP#request-body.
        /// </remarks>
        private class PublicApiCreateItemRequestModel
        {
            [JsonProperty("type")]
            public FabricUpgradeResourceTypes ItemType { get; set; }

            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
            public string Description { get; set; }

            [JsonProperty("definition")]
            public PublicApiItemDefinitionModel Definition { get; } = new PublicApiItemDefinitionModel();

            public override string ToString()
            {
                return SafeJsonConvert.SerializeObject(this);
            }
        }

        /// <summary>
        /// The Public API model for the UpdateItem request payload.
        /// </summary>
        /// <remarks>
        /// https://learn.microsoft.com/en-us/rest/api/fabric/core/items/update-item?tabs=HTTP#request-body
        /// Note that UpdateItem and UpdateItemDescription are two separate calls.
        /// </remarks>
        private class PublicApiUpdateItemRequestModel
        {
            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
            public string Description { get; set; }

            public override string ToString()
            {
                return SafeJsonConvert.SerializeObject(this);
            }
        }

        /// <summary>
        /// The Public API model for the UpdateItemDefinition request payload.
        /// </summary>
        /// <remarks>
        /// https://learn.microsoft.com/en-us/rest/api/fabric/core/items/update-item-definition?tabs=HTTP#itemdefinition.
        /// </remarks>
        private class PublicApiUpdateItemDefinitionRequestModel
        {
            [JsonProperty("definition")]
            public PublicApiItemDefinitionModel Definition { get; } = new PublicApiItemDefinitionModel();

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        /// <summary>
        /// The Public API model for an ItemDefinition.
        /// </summary>
        /// <remarks>
        /// This is part of the payload in a CreateItem request.
        /// https://learn.microsoft.com/en-us/rest/api/fabric/core/items/create-item?tabs=HTTP#itemdefinition.
        /// This is the entire payload in an UpdateItemDefinition request.
        /// https://learn.microsoft.com/en-us/rest/api/fabric/core/items/update-item-definition?tabs=HTTP#itemdefinition.
        /// </remarks>
        private class PublicApiItemDefinitionModel
        {
            [JsonProperty("parts")]
            public List<PublicApiItemDefinitionPartModel> Parts { get; } = new List<PublicApiItemDefinitionPartModel>();

            /*
            [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
            public string Format { get; set; }
            */
        }

        /// <summary>
        /// The Public API model for an ItemDefinitionPart.
        /// </summary>
        /// <remarks>
        /// https://learn.microsoft.com/en-us/rest/api/fabric/core/items/create-item?tabs=HTTP#itemdefinitionpart.
        /// https://learn.microsoft.com/en-us/rest/api/fabric/core/items/update-item-definition?tabs=HTTP#itemdefinitionpart.
        /// </remarks>
        private class PublicApiItemDefinitionPartModel
        {
            [JsonProperty("path")]
            public string Path { get; set; }

            [JsonProperty("payloadType")]
            public string PayloadType { get; set; }

            [JsonProperty("payload")]
            public string Payload { get; set; }
        }

        /// <summary>
        /// The Public API model for the item returned in ListItems and CreateItem.
        /// </summary>
        /// <remarks>
        /// https://learn.microsoft.com/en-us/rest/api/fabric/core/items/create-item?tabs=HTTP#item.
        /// </remarks>
        private class PublicApiItemModel
        {
            [JsonProperty(PropertyName = "type", Order = 1)]
            public string ItemType { get; set; }

            [JsonProperty(PropertyName = "displayName", Order = 2)]
            public string DisplayName { get; set; }

            [JsonProperty(PropertyName = "description", Order = 3)]
            public string Description { get; set; }

            [JsonProperty(PropertyName = "workspaceId", Order = 4)]
            public string WorkspaceId { get; set; }

            [JsonProperty(PropertyName = "id", Order = 5)]
            public string ItemId { get; set; }
        }
    }
}

// <copyright file="PublicApiClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading;

namespace FabricUpgradeCmdlet
{
    public class PublicApiClient
    {
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

        public async Task<string> CreateOrUpdatePipelineAsync(
            JObject pipelineObject,
            CancellationToken cancellationToken)
        {
            // TODO: If the pipeline already exists, then Update it instead!
            return await this.CreatePipelineAsync(pipelineObject, cancellationToken);
        }

        private async Task<string> CreatePipelineAsync(
            JObject pipelineObject,
            CancellationToken cancellationToken)
        {
            string publicApiBaseUrl = ComputePublicApiBaseUrl(this.cluster);

            string pipelineName = pipelineObject.SelectToken("$.name")?.ToString();
            string pipelineDescription = pipelineObject.SelectToken("$.properties.description")?.ToString();

            HttpRequestMessage request = this.BuildCreateItemRequestMessage(
                publicApiBaseUrl,
                FabricUpgradeResourceTypes.DataPipeline.ToString(),
                pipelineName,
                pipelineDescription,
                pipelineObject);

            HttpResponseMessage response = await CreateHttpClient().SendAsync(request, cancellationToken).ConfigureAwait(false);
            string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return responsePayload;
        }

        private HttpRequestMessage BuildCreateItemRequestMessage(
            string publicApiBaseUrl,
            string artifactType,
            string displayName,
            string description,
            JObject payload)
        {
            HttpMethod httpMethod = HttpMethod.Post;
            string relativeUrl = publicApiBaseUrl + $"workspaces/{this.workspaceId}/items";

            HttpRequestMessage request = new HttpRequestMessage(httpMethod, relativeUrl)
            {
                Content = new StringContent(
                    this.BuildCreateItemRequestPayload(
                        artifactType,
                        displayName,
                        description,
                        payload),
                    encoding: Encoding.UTF8,
                    mediaType: "application/json"),
            };

            request.Headers.Add("Authorization", $"Bearer {this.pbiAadToken}");

            return request;
        }

        private string BuildCreateItemRequestPayload(
            string artifactType,
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
                Path = "pipeline-content.json",
                PayloadType = "InlineBase64",
                Payload = payload64,
            });

            return createItemPayload.ToString();
        }

        private static string ComputePublicApiBaseUrl(string cluster)
        {
            return cluster switch
            {
                "daily" => "https://dailyapi.fabric.microsoft.com/v1/",
                "dxt" => "https://dxtapi.fabric.microsoft.com/v1",
                "msit" => "https://msitapi.fabric.microsoft.com/v1",
                "prod" => "https://api.fabric.microsoft.com/v1",
                _ => "http://localhost",
            };
        }

        private static HttpClient CreateHttpClient()
        {
            return Services.HttpClientFactory.CreateHttpClient();
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
            public string ItemType { get; set; }

            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
            public string Description { get; set; }

            [JsonProperty("definition")]
            public PublicApiItemDefinitionModel Definition { get; } = new PublicApiItemDefinitionModel();

            public override string ToString()
            {
                return UpgradeSerialization.Serialize(this);
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
                return UpgradeSerialization.Serialize(this);
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
                return UpgradeSerialization.Serialize(this);
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
            [JsonProperty("id")]
            public string ItemId { get; set; }

            /*
            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            [JsonProperty("type")]
            public string ItemType { get; set; }

            [JsonProperty("workspaceId")]
            public string WorkspaceId { get; set; }
            */
        }

    }
}

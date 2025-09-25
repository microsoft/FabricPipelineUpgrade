// <copyright file="AdfApiClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule
{
    /// <summary>
    /// This client interacts with Azure Data Factory REST API endpoints.
    /// </summary>
    public class AdfApiClient : IApiClient
    {
        private readonly string subscriptionId;
        private readonly string resourceGroupName;
        private readonly string factoryName;
        private readonly string accessToken;
        private readonly HttpClient httpClient;

        private const string BaseUrl = "https://management.azure.com";
        private const string ApiVersion = "2018-06-01";

        public AdfApiClient(
            string subscriptionId,
            string resourceGroupName,
            string factoryName,
            string accessToken)
        {
            this.subscriptionId = subscriptionId;
            this.resourceGroupName = resourceGroupName;
            this.factoryName = factoryName;
            this.accessToken = accessToken;
            
            this.httpClient = new HttpClient();
            this.httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        }

        /// <summary>
        /// Get all pipelines from the Azure Data Factory.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A dictionary of pipeline names to pipeline definitions.</returns>
        public async Task<Dictionary<string, JObject>> GetPipelinesAsync(CancellationToken cancellationToken = default)
        {
            var pipelines = new Dictionary<string, JObject>();
            string url = $"{BaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}/pipelines?api-version={ApiVersion}";

            try
            {
                await FetchAllResourcesAsync(url, async (pipelineInfo) =>
                {
                    string pipelineName = pipelineInfo["name"].ToString();
                    // Use the pipeline definition directly from the list API response
                    // No need to call GetPipelineAsync again since list API returns complete definitions
                    pipelines[pipelineName] = pipelineInfo;
                }, cancellationToken).ConfigureAwait(false);

                return pipelines;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving pipelines from ADF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get all datasets from the Azure Data Factory.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A dictionary of dataset names to dataset definitions.</returns>
        public async Task<Dictionary<string, JObject>> GetDatasetsAsync(CancellationToken cancellationToken = default)
        {
            var datasets = new Dictionary<string, JObject>();
            string url = $"{BaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}/datasets?api-version={ApiVersion}";

            try
            {
                await FetchAllResourcesAsync(url, async (datasetInfo) =>
                {
                    string datasetName = datasetInfo["name"].ToString();
                    // Use the dataset definition directly from the list API response
                    // No need to call GetDatasetAsync again since list API returns complete definitions
                    datasets[datasetName] = datasetInfo;
                }, cancellationToken).ConfigureAwait(false);

                return datasets;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving datasets from ADF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get a specific dataset from the Azure Data Factory.
        /// </summary>
        /// <param name="datasetName">The name of the dataset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The dataset definition.</returns>
        public async Task<JObject> GetDatasetAsync(string datasetName, CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}/datasets/{datasetName}?api-version={ApiVersion}";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get dataset '{datasetName}': {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(responseContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving dataset '{datasetName}' from ADF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get all linked services from the Azure Data Factory.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A dictionary of linked service names to linked service definitions.</returns>
        public async Task<Dictionary<string, JObject>> GetLinkedServicesAsync(CancellationToken cancellationToken = default)
        {
            var linkedServices = new Dictionary<string, JObject>();
            string url = $"{BaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}/linkedservices?api-version={ApiVersion}";

            try
            {
                await FetchAllResourcesAsync(url, async (linkedServiceInfo) =>
                {
                    string linkedServiceName = linkedServiceInfo["name"].ToString();
                    // Use the linked service definition directly from the list API response
                    // No need to call GetLinkedServiceAsync again since list API returns complete definitions
                    linkedServices[linkedServiceName] = linkedServiceInfo;
                }, cancellationToken).ConfigureAwait(false);

                return linkedServices;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving linked services from ADF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get a specific linked service from the Azure Data Factory.
        /// </summary>
        /// <param name="linkedServiceName">The name of the linked service.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The linked service definition.</returns>
        public async Task<JObject> GetLinkedServiceAsync(string linkedServiceName, CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}/linkedservices/{linkedServiceName}?api-version={ApiVersion}";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get linked service '{linkedServiceName}': {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(responseContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving linked service '{linkedServiceName}' from ADF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get all triggers from the Azure Data Factory.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A dictionary of trigger names to trigger definitions.</returns>
        public async Task<Dictionary<string, JObject>> GetTriggersAsync(CancellationToken cancellationToken = default)
        {
            var triggers = new Dictionary<string, JObject>();
            string url = $"{BaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}/triggers?api-version={ApiVersion}";

            try
            {
                await FetchAllResourcesAsync(url, async (triggerInfo) =>
                {
                    string triggerName = triggerInfo["name"].ToString();
                    // Use the trigger definition directly from the list API response
                    // No need to call GetTriggerAsync again since list API returns complete definitions
                    triggers[triggerName] = triggerInfo;
                }, cancellationToken).ConfigureAwait(false);

                return triggers;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving triggers from ADF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get a specific trigger from the Azure Data Factory.
        /// </summary>
        /// <param name="triggerName">The name of the trigger.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The trigger definition.</returns>
        public async Task<JObject> GetTriggerAsync(string triggerName, CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}/triggers/{triggerName}?api-version={ApiVersion}";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get trigger '{triggerName}': {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(responseContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving trigger '{triggerName}' from ADF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get the Data Factory information.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The Data Factory information.</returns>
        public async Task<JObject> GetDataFactoryAsync(CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}?api-version={ApiVersion}";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get data factory: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(responseContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving data factory information: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Dispose of the HTTP client.
        /// </summary>
        public void Dispose()
        {
            httpClient?.Dispose();
        }

        /// <summary>
        /// Helper method to fetch all resources from a paginated API endpoint.
        /// Handles continuation tokens automatically.
        /// </summary>
        /// <param name="initialUrl">The initial URL to start fetching from.</param>
        /// <param name="processResourceFunc">Function to process each resource item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that completes when all resources have been fetched and processed.</returns>
        private async Task FetchAllResourcesAsync(
            string initialUrl, 
            Func<JObject, Task> processResourceFunc, 
            CancellationToken cancellationToken = default)
        {
            string currentUrl = initialUrl;
            
            do
            {
                HttpResponseMessage response = await httpClient.GetAsync(currentUrl, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to fetch resources from {currentUrl}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject responseObj = JObject.Parse(responseContent);
                
                // Process all items in the current page
                JArray resourceList = (JArray)responseObj["value"];
                if (resourceList != null)
                {
                    foreach (JObject resource in resourceList)
                    {
                        await processResourceFunc(resource).ConfigureAwait(false);
                    }
                }

                // Check for continuation token (nextLink)
                currentUrl = responseObj["nextLink"]?.ToString();
                
            } while (!string.IsNullOrEmpty(currentUrl));
        }

        /// <summary>
        /// Get a specific pipeline from the Azure Data Factory.
        /// </summary>
        /// <param name="pipelineName">The name of the pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The pipeline definition.</returns>
        public async Task<JObject> GetPipelineAsync(string pipelineName, CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}/pipelines/{pipelineName}?api-version={ApiVersion}";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get pipeline '{pipelineName}': {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(responseContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving pipeline '{pipelineName}' from ADF: {ex.Message}", ex);
            }
        }
    }
}
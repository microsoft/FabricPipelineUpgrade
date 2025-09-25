// Copyright (c) Microsoft. All rights reserved.
// SynapseApiClient.cs
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace FabricUpgradePowerShellModule
{
    /// <summary>
    /// Client for Synapse workspace artifact APIs (pipelines, datasets, linked services, triggers).
    /// NOTE: Synapse artifact (authoring) endpoints are NOT exposed via management.azure.com like ADF.
    /// They are accessed through the workspace dev endpoint: https://{workspace}.dev.azuresynapse.net.
    /// ARM (management.azure.com) is still used only for workspace resource metadata (location, etc.).
    /// </summary>
    public class SynapseApiClient : IApiClient, IDisposable
    {
        private readonly string subscriptionId;
        private readonly string resourceGroupName;
        private readonly string workspaceName;
        private readonly string synapseToken; // Token for Synapse dev endpoint
        private readonly string armToken; // Token for ARM APIs (optional - can be same as synapseToken)
        private readonly HttpClient httpClient;

        private readonly string devBaseUrl; // https://{workspace}.dev.azuresynapse.net

        private const string ArmBaseUrl = "https://management.azure.com"; // for workspace metadata only
        private const string DevBaseUrlFormat = "https://{0}.dev.azuresynapse.net"; // for artifacts
        private const string ApiVersion = "2020-12-01"; // Synapse artifact API version

        /// <summary>
        /// Initialize Synapse API client with separate tokens for ARM and Synapse endpoints.
        /// </summary>
        /// <param name="subscriptionId">Azure subscription ID</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="workspaceName">Synapse workspace name</param>
        /// <param name="synapseToken">Token for Synapse dev endpoint APIs</param>
        /// <param name="armToken">Token for ARM APIs (optional - defaults to synapseToken if not provided)</param>
        public SynapseApiClient(string subscriptionId, string resourceGroupName, string workspaceName, string synapseToken, string armToken = null)
        {
            this.subscriptionId = subscriptionId;
            this.resourceGroupName = resourceGroupName;
            this.workspaceName = workspaceName;
            this.synapseToken = synapseToken;
            this.armToken = armToken ?? synapseToken; // Use synapseToken as fallback for ARM if not provided
            this.httpClient = new HttpClient();
            this.devBaseUrl = string.Format(DevBaseUrlFormat, workspaceName);
        }

        private string WorkspaceArmScope => $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Synapse/workspaces/{workspaceName}";

        /// <summary>
        /// List pipelines from Synapse dev endpoint.
        /// </summary>
        public async Task<Dictionary<string, JObject>> GetPipelinesAsync(CancellationToken ct = default)
        {
            var dict = new Dictionary<string, JObject>();
            string url = $"{devBaseUrl}/pipelines?api-version={ApiVersion}";
            await FetchAllAsync(url, async item =>
            {
                string name = item["name"].ToString();
                // Synapse list may not include full definition; fetch full pipeline for consistency.
                try
                {
                    JObject full = await GetPipelineAsync(name, ct).ConfigureAwait(false);
                    dict[name] = full;
                }
                catch
                {
                    dict[name] = item; // fallback to list item
                }
            }, ct, useSynapseToken: true).ConfigureAwait(false);
            return dict;
        }

        public async Task<JObject> GetPipelineAsync(string pipelineName, CancellationToken ct = default)
        {
            string url = $"{devBaseUrl}/pipelines/{pipelineName}?api-version={ApiVersion}";
            return await GetResourceAsync(url, $"pipeline '{pipelineName}'", ct, useSynapseToken: true).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, JObject>> GetDatasetsAsync(CancellationToken ct = default)
        {
            var dict = new Dictionary<string, JObject>();
            string url = $"{devBaseUrl}/datasets?api-version={ApiVersion}";
            await FetchAllAsync(url, async item =>
            {
                string name = item["name"].ToString();
                // Attempt to get full dataset definition
                try
                {
                    JObject full = await GetDatasetAsync(name, ct).ConfigureAwait(false);
                    dict[name] = full;
                }
                catch
                {
                    dict[name] = item;
                }
            }, ct, useSynapseToken: true).ConfigureAwait(false);
            return dict;
        }

        public async Task<JObject> GetDatasetAsync(string datasetName, CancellationToken ct = default)
        {
            string url = $"{devBaseUrl}/datasets/{datasetName}?api-version={ApiVersion}";
            return await GetResourceAsync(url, $"dataset '{datasetName}'", ct, useSynapseToken: true).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, JObject>> GetLinkedServicesAsync(CancellationToken ct = default)
        {
            var dict = new Dictionary<string, JObject>();
            string url = $"{devBaseUrl}/linkedservices?api-version={ApiVersion}";
            await FetchAllAsync(url, async item =>
            {
                string name = item["name"].ToString();
                try
                {
                    JObject full = await GetLinkedServiceAsync(name, ct).ConfigureAwait(false);
                    dict[name] = full;
                }
                catch
                {
                    dict[name] = item;
                }
            }, ct, useSynapseToken: true).ConfigureAwait(false);
            return dict;
        }

        public async Task<JObject> GetLinkedServiceAsync(string name, CancellationToken ct = default)
        {
            string url = $"{devBaseUrl}/linkedservices/{name}?api-version={ApiVersion}";
            return await GetResourceAsync(url, $"linked service '{name}'", ct, useSynapseToken: true).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, JObject>> GetTriggersAsync(CancellationToken ct = default)
        {
            var dict = new Dictionary<string, JObject>();
            string url = $"{devBaseUrl}/triggers?api-version={ApiVersion}";
            await FetchAllAsync(url, async item =>
            {
                string name = item["name"].ToString();
                try
                {
                    JObject full = await GetTriggerAsync(name, ct).ConfigureAwait(false);
                    dict[name] = full;
                }
                catch
                {
                    dict[name] = item;
                }
            }, ct, useSynapseToken: true).ConfigureAwait(false);
            return dict;
        }

        public async Task<JObject> GetTriggerAsync(string triggerName, CancellationToken ct = default)
        {
            string url = $"{devBaseUrl}/triggers/{triggerName}?api-version={ApiVersion}";
            return await GetResourceAsync(url, $"trigger '{triggerName}'", ct, useSynapseToken: true).ConfigureAwait(false);
        }

        /// <summary>
        /// Get workspace ARM metadata (location, name) using ARM token.
        /// </summary>
        public async Task<JObject> GetWorkspaceAsync(CancellationToken ct = default)
        {
            string url = $"{ArmBaseUrl}{WorkspaceArmScope}?api-version=2021-06-01"; // use ARM synapse workspace API
            return await GetResourceAsync(url, "workspace metadata", ct, useSynapseToken: false).ConfigureAwait(false);
        }

        private async Task<JObject> GetResourceAsync(string url, string friendly, CancellationToken ct, bool useSynapseToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Use appropriate token based on endpoint
            string token = useSynapseToken ? synapseToken : armToken;
            request.Headers.Add("Authorization", $"Bearer {token}");

            HttpResponseMessage resp = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get {friendly}: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()} ");
            }
            string content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JObject.Parse(content);
        }

        private async Task FetchAllAsync(string initialUrl, Func<JObject, Task> process, CancellationToken ct, bool useSynapseToken)
        {
            string current = initialUrl;
            while (!string.IsNullOrEmpty(current))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);

                // Use appropriate token based on endpoint
                string token = useSynapseToken ? synapseToken : armToken;
                request.Headers.Add("Authorization", $"Bearer {token}");

                HttpResponseMessage resp = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to fetch resources from {current}: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()} ");
                }
                string content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject obj = JObject.Parse(content);
                var value = (JArray)obj["value"]; if (value != null)
                {
                    foreach (JObject item in value)
                    {
                        await process(item).ConfigureAwait(false);
                    }
                }
                current = obj["nextLink"]?.ToString();
            }
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}

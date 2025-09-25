// <copyright file="FabricAdminApiClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace FabricUpgradePowerShellModule
{
    /// <summary>
    /// This client interacts with the Fabric Admin API endpoints for workspace management.
    /// </summary>
    public class FabricAdminApiClient
    {
        // The region of the user's workspace.
        private readonly string region;

        // A Fabric user access token that authenticates/authorizes workspace management operations.
        private readonly string fabricToken;

        private readonly bool verbose;

        public FabricAdminApiClient(string region, string fabricToken, bool verbose = false)
        {
            this.region = region;
            this.fabricToken = fabricToken;
            this.verbose = verbose;
            if (!this.fabricToken.StartsWith("Bearer "))
            {
                this.fabricToken = "Bearer " + this.fabricToken;
            }
        }

        /// <summary>
        /// Create a new workspace using the Fabric Admin API.
        /// </summary>
        /// <param name="workspaceName">The name for the new workspace.</param>
        /// <param name="capacityGuid">The Fabric capacity GUID to associate with the workspace.</param>
        /// <param name="description">Optional description for the workspace.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The workspace ID of the created workspace.</returns>
        public async Task<string> CreateWorkspaceAsync(
            string workspaceName,
            string capacityGuid,
            string description = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                HttpClient httpClient = this.BuildFabricAdminHttpClient();

                var createWorkspacePayload = new
                {
                    displayName = workspaceName,
                    description = description ?? $"Workspace created for ADF to Fabric migration: {workspaceName}",
                    capacityId = capacityGuid
                };

                string jsonPayload = JsonConvert.SerializeObject(createWorkspacePayload, Formatting.Indented);
                
                if (this.verbose)
                {
                    Console.WriteLine("=== WORKSPACE CREATION REQUEST ===");
                    Console.WriteLine($"Creating Fabric workspace: '{workspaceName}'");
                    Console.WriteLine($"Using Fabric Capacity GUID: {capacityGuid}");
                    Console.WriteLine($"Target Region: {this.region}");
                    Console.WriteLine("JSON Payload being sent to Fabric Admin API:");
                    Console.WriteLine(jsonPayload);
                    Console.WriteLine("===================================");
                }

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "workspaces")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("Authorization", this.fabricToken);

                string fabricApiBaseUrl = this.ComputeFabricAdminBaseUrl();
                
                if (this.verbose)
                {
                    Console.WriteLine($"Request URL: POST {fabricApiBaseUrl}workspaces");
                }

                HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (this.verbose)
                {
                    Console.WriteLine("=== WORKSPACE CREATION RESPONSE ===");
                    Console.WriteLine($"Status Code: {response.StatusCode}");
                    Console.WriteLine("Response Payload:");
                    try
                    {
                        // Try to format the response JSON for better readability
                        JObject formattedResponse = JObject.Parse(responsePayload);
                        Console.WriteLine(formattedResponse.ToString(Formatting.Indented));
                    }
                    catch
                    {
                        // If parsing fails, just log the raw response
                        Console.WriteLine(responsePayload);
                    }
                    Console.WriteLine("====================================");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to create workspace '{workspaceName}'. Status: {response.StatusCode}, Response: {responsePayload}");
                }

                try
                {
                    JObject workspaceResponse = JObject.Parse(responsePayload);
                    string workspaceId = workspaceResponse.SelectToken("$.id")?.ToString();
                    
                    if (string.IsNullOrEmpty(workspaceId))
                    {
                        throw new Exception($"Failed to parse workspace ID from response: {responsePayload}");
                    }

                    if (this.verbose)
                    {
                        Console.WriteLine($"✓ Successfully created workspace with ID: {workspaceId}");
                    }
                    
                    return workspaceId;
                }
                catch (JsonException)
                {
                    throw new Exception($"Received unparseable response payload from CreateWorkspace: {responsePayload}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// List all capacities available to the user.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A JArray of available capacities.</returns>
        public async Task<JArray> ListCapacitiesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                HttpClient httpClient = this.BuildFabricAdminHttpClient();

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "capacities");
                request.Headers.Add("Authorization", this.fabricToken);

                string fabricApiBaseUrl = this.ComputeFabricAdminBaseUrl();
                
                if (this.verbose)
                {
                    Console.WriteLine($"Request URL: GET {fabricApiBaseUrl}capacities");
                }

                HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to list capacities from Fabric Admin API. Status: {response.StatusCode}, Response: {responsePayload}");
                }

                try
                {
                    JObject payloadObject = JObject.Parse(responsePayload);
                    JArray capacities = (JArray)payloadObject.SelectToken("$.value") ?? new JArray();
                    
                    if (this.verbose)
                    {
                        Console.WriteLine($"Retrieved {capacities.Count} capacities from Fabric Admin API");
                    }
                    
                    return capacities;
                }
                catch (JsonException)
                {
                    throw new Exception($"Received unparseable response payload from ListCapacities: {responsePayload}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// List all workspaces available to the user.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A JArray of available workspaces.</returns>
        public async Task<JArray> ListWorkspacesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                HttpClient httpClient = this.BuildFabricAdminHttpClient();

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "workspaces");
                request.Headers.Add("Authorization", this.fabricToken);

                string fabricApiBaseUrl = this.ComputeFabricAdminBaseUrl();
                
                if (this.verbose)
                {
                    Console.WriteLine($"Request URL: GET {fabricApiBaseUrl}workspaces");
                }

                HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to list workspaces from Fabric Admin API. Status: {response.StatusCode}, Response: {responsePayload}");
                }

                try
                {
                    JObject workspaceResponse = JObject.Parse(responsePayload);
                    JArray workspaces = workspaceResponse.SelectToken("$.value") as JArray;

                    if (workspaces == null)
                    {
                        throw new Exception($"Failed to parse workspaces from response: {responsePayload}");
                    }

                    if (this.verbose)
                    {
                        Console.WriteLine($"Retrieved {workspaces.Count} workspaces from Fabric Admin API");
                    }

                    return workspaces;
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    throw new Exception($"Received unparseable response payload from ListWorkspaces: {responsePayload}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Get a capacity by display name by listing all capacities and searching.
        /// </summary>
        /// <param name="displayName">The display name of the capacity to find.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>JObject containing capacity information.</returns>
        public async Task<JObject> GetCapacityByNameAsync(string displayName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (this.verbose)
                {
                    Console.WriteLine($"=== CAPACITY LOOKUP BY NAME ===");
                    Console.WriteLine($"Searching for capacity: {displayName}");
                    Console.WriteLine("Retrieving all capacities from Fabric Admin API...");
                }

                // Get all capacities
                var capacities = await this.ListCapacitiesAsync(cancellationToken).ConfigureAwait(false);

                if (this.verbose)
                {
                    Console.WriteLine("Searching for matching capacity by name...");
                }

                // Find the capacity with the matching name
                foreach (JObject capacity in capacities.Cast<JObject>())
                {
                    string fabricCapacityName = capacity.SelectToken("$.displayName")?.ToString() 
                                             ?? capacity.SelectToken("$.name")?.ToString();

                    if (string.Equals(fabricCapacityName, displayName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (this.verbose)
                        {
                            string capacityId = capacity.SelectToken("$.id")?.ToString();
                            string capacityState = capacity.SelectToken("$.state")?.ToString();
                            Console.WriteLine($"✓ Found matching capacity: '{fabricCapacityName}' (ID: {capacityId}, State: {capacityState})");
                            Console.WriteLine("===============================");
                        }
                        return capacity;
                    }
                }

                throw new Exception($"Could not find Fabric capacity with name '{displayName}'. " +
                    "The capacity may not be visible to the current user, or it may still be provisioning. " +
                    "Please wait a few minutes and try again, or verify the capacity exists and is accessible.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve Fabric capacity with name '{displayName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Build an HTTP client that talks to the Fabric Admin API.
        /// </summary>
        /// <returns>The HttpClient.</returns>
        private HttpClient BuildFabricAdminHttpClient()
        {
            IHttpClientFactory httpClientFactory = Services.HttpClientFactory;
            HttpClient httpClient = httpClientFactory.CreateHttpClient();
            string fabricAdminBaseUrl = this.ComputeFabricAdminBaseUrl();
            httpClient.BaseAddress = new Uri(fabricAdminBaseUrl);

            httpClient.DefaultRequestHeaders.UserAgent.Clear();

            // Prefer structured ProductInfoHeaderValue when possible
            var version = this.GetType().Assembly.GetName().Version?.ToString() ?? "DefaultVersion";
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FabricUpgradePowerShellModule", version));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/microsoft/FabricPipelineUpgrade)"));

            return httpClient;
        }

        /// <summary>
        /// Compute the Fabric Admin API base URL from the region name.
        /// </summary>
        /// <returns>The Fabric Admin API base URL for this region.</returns>
        private string ComputeFabricAdminBaseUrl()
        {
            return this.region switch
            {
                "daily" => "https://dailyapi.fabric.microsoft.com/v1/",
                "dxt" => "https://dxtapi.fabric.microsoft.com/v1/",
                "msit" => "https://msitapi.fabric.microsoft.com/v1/",
                "prod" => "https://api.fabric.microsoft.com/v1/",
                _ => "https://api.fabric.microsoft.com/v1/",
            };
        }
    }
}
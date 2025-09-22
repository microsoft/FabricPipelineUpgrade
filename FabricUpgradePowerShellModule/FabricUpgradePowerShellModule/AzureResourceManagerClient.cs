// <copyright file="AzureResourceManagerClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule
{
    /// <summary>
    /// This client interacts with Azure Resource Manager APIs for capacity management.
    /// </summary>
    public class AzureResourceManagerClient
    {
        private readonly string subscriptionId;
        private readonly string azureToken;

        public AzureResourceManagerClient(string subscriptionId, string azureToken)
        {
            this.subscriptionId = subscriptionId;
            this.azureToken = azureToken;
            if (!this.azureToken.StartsWith("Bearer "))
            {
                this.azureToken = "Bearer " + this.azureToken;
            }
        }

        /// <summary>
        /// Create a new Fabric capacity using Azure Resource Manager API.
        /// </summary>
        /// <param name="resourceGroupName">The resource group name where the capacity will be created.</param>
        /// <param name="capacityName">The name for the new capacity.</param>
        /// <param name="location">The Azure region for the capacity (e.g., "East US").</param>
        /// <param name="skuName">The SKU name for the capacity (e.g., "F2").</param>
        /// <param name="adminMembers">List of admin user emails for the capacity.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The capacity resource ID.</returns>
        public async Task<string> CreateFabricCapacityAsync(
            string resourceGroupName,
            string capacityName,
            string location,
            string skuName = "F2",
            List<string> adminMembers = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Ensure we have at least one administrator
                var effectiveAdminMembers = await this.EnsureCapacityAdministratorAsync(adminMembers).ConfigureAwait(false);

                HttpClient httpClient = this.BuildArmHttpClient();

                var capacityPayload = new
                {
                    location = location,
                    sku = new
                    {
                        name = skuName,
                        tier = "Fabric"
                    },
                    properties = new
                    {
                        administration = new
                        {
                            members = effectiveAdminMembers
                        }
                    },
                    tags = new
                    {
                        CreatedBy = "FabricUpgradePowerShellModule",
                        Purpose = "ADF-to-Fabric-Migration"
                    }
                };

                string jsonPayload = JsonConvert.SerializeObject(capacityPayload, Formatting.Indented);
                
                // Log the JSON payload being sent to Azure
                Console.WriteLine("=== CAPACITY CREATION REQUEST ===");
                Console.WriteLine($"Creating Fabric capacity: {capacityName}");
                Console.WriteLine($"Resource Group: {resourceGroupName}");
                Console.WriteLine($"Location: {location}");
                Console.WriteLine($"SKU: {skuName}");
                Console.WriteLine($"Administrators: {string.Join(", ", effectiveAdminMembers)}");
                Console.WriteLine("JSON Payload being sent to Azure ARM API:");
                Console.WriteLine(jsonPayload);
                Console.WriteLine("====================================");

                string resourcePath = $"subscriptions/{this.subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Fabric/capacities/{capacityName}";
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, $"{resourcePath}?api-version=2023-11-01")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("Authorization", this.azureToken);

                Console.WriteLine($"Request URL: PUT https://management.azure.com/{resourcePath}?api-version=2023-11-01");

                HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Console.WriteLine("=== CAPACITY CREATION RESPONSE ===");
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
                Console.WriteLine("===================================");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to create Fabric capacity '{capacityName}'. Status: {response.StatusCode}, Response: {responsePayload}");
                }

                try
                {
                    JObject capacityResponse = JObject.Parse(responsePayload);
                    string capacityResourceId = capacityResponse.SelectToken("$.id")?.ToString();

                    if (string.IsNullOrEmpty(capacityResourceId))
                    {
                        throw new Exception($"Failed to parse capacity resource ID from response: {responsePayload}");
                    }

                    Console.WriteLine($"✓ Successfully created Azure capacity with Resource ID: {capacityResourceId}");
                    Console.WriteLine($"Capacity Name: {capacityName}");
                    Console.WriteLine($"Now retrieving Fabric capacity GUID for workspace creation...");
                    
                    // Return the resource ID and capacity name - we'll get the Fabric GUID separately
                    return $"{capacityResourceId}|{capacityName}";
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    throw new Exception($"Received unparseable response payload from CreateCapacity: {responsePayload}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Get the location of a resource group.
        /// </summary>
        /// <param name="resourceGroupName">The resource group name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The location of the resource group.</returns>
        public async Task<string> GetResourceGroupLocationAsync(string resourceGroupName, CancellationToken cancellationToken = default)
        {
            try
            {
                HttpClient httpClient = this.BuildArmHttpClient();

                string resourcePath = $"subscriptions/{this.subscriptionId}/resourceGroups/{resourceGroupName}";
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{resourcePath}?api-version=2021-04-01");
                request.Headers.Add("Authorization", this.azureToken);

                HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get resource group '{resourceGroupName}'. Status: {response.StatusCode}, Response: {responsePayload}");
                }

                try
                {
                    JObject rgResponse = JObject.Parse(responsePayload);
                    string location = rgResponse.SelectToken("$.location")?.ToString();

                    if (string.IsNullOrEmpty(location))
                    {
                        throw new Exception($"Failed to parse location from resource group response: {responsePayload}");
                    }

                    return location;
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    throw new Exception($"Received unparseable response payload from GetResourceGroup: {responsePayload}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Validate that a Fabric capacity exists.
        /// </summary>
        /// <param name="resourceGroupName">The resource group name where the capacity should exist.</param>
        /// <param name="capacityName">The name of the capacity to validate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the capacity exists, false otherwise.</returns>
        public async Task<bool> ValidateFabricCapacityExistsInAzureAsync(
            string resourceGroupName,
            string capacityName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                HttpClient httpClient = this.BuildArmHttpClient();

                string resourcePath = $"subscriptions/{this.subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Fabric/capacities/{capacityName}";
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{resourcePath}?api-version=2023-11-01");
                request.Headers.Add("Authorization", this.azureToken);

                HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }
                else
                {
                    string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception($"Failed to validate Fabric capacity '{capacityName}'. Status: {response.StatusCode}, Response: {responsePayload}");
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("Failed to validate")))
            {
                // If it's a network/auth error, we should throw it
                // If it's our own validation error, re-throw as-is
                throw new Exception($"Error while validating capacity '{capacityName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get the provisioning state of a Fabric capacity.
        /// </summary>
        /// <param name="resourceGroupName">The resource group name where the capacity exists.</param>
        /// <param name="capacityName">The name of the capacity.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The provisioning state of the capacity.</returns>
        public async Task<string> GetCapacityProvisioningStateAsync(
            string resourceGroupName,
            string capacityName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                HttpClient httpClient = this.BuildArmHttpClient();

                string resourcePath = $"subscriptions/{this.subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Fabric/capacities/{capacityName}";
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{resourcePath}?api-version=2023-11-01");
                request.Headers.Add("Authorization", this.azureToken);

                HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get capacity provisioning state for '{capacityName}'. Status: {response.StatusCode}, Response: {responsePayload}");
                }

                try
                {
                    JObject capacityResponse = JObject.Parse(responsePayload);
                    
                    // Try to get provisioning state from properties
                    string provisioningState = capacityResponse.SelectToken("$.properties.provisioningState")?.ToString() 
                                            ?? capacityResponse.SelectToken("$.properties.state")?.ToString() 
                                            ?? "Unknown";

                    return provisioningState;
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    throw new Exception($"Failed to parse provisioning state from response: {responsePayload}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Build an HTTP client that talks to Azure Resource Manager.
        /// </summary>
        /// <returns>The HttpClient.</returns>
        private HttpClient BuildArmHttpClient()
        {
            IHttpClientFactory httpClientFactory = Services.HttpClientFactory;
            HttpClient httpClient = httpClientFactory.CreateHttpClient();
            httpClient.BaseAddress = new Uri("https://management.azure.com/");
            return httpClient;
        }

        /// <summary>
        /// Ensure that we have at least one capacity administrator.
        /// If no administrators are provided, extract the current user from the Azure token.
        /// </summary>
        /// <param name="adminMembers">The provided list of admin members.</param>
        /// <returns>A list with at least one administrator.</returns>
        private async Task<List<string>> EnsureCapacityAdministratorAsync(List<string> adminMembers)
        {
            // If admin members are already provided, use them
            if (adminMembers != null && adminMembers.Count > 0)
            {
                return adminMembers;
            }

            // No admin members provided - extract user from Azure token
            try
            {
                string userEmail = this.ExtractUserEmailFromTokenAsync();
                if (!string.IsNullOrEmpty(userEmail))
                {
                    return new List<string> { userEmail };
                }
            }
            catch (Exception ex)
            {
                // If we can't extract user info, throw a helpful error
                throw new InvalidOperationException(
                    "At least one capacity administrator is required. " +
                    "Either provide AdminMembers parameter or ensure the Azure token contains valid user information. " +
                    $"Token parsing error: {ex.Message}");
            }

            // Fallback error if we couldn't determine a user
            throw new InvalidOperationException(
                "At least one capacity administrator is required. " +
                "Either provide AdminMembers parameter or use an Azure token that contains user identity information.");
        }

        /// <summary>
        /// Extract the user email from the Azure JWT token.
        /// </summary>
        /// <returns>The user email address from the token.</returns>
        private string ExtractUserEmailFromTokenAsync()
        {
            try
            {
                // Remove "Bearer " prefix if present
                string tokenValue = this.azureToken.StartsWith("Bearer ") 
                    ? this.azureToken.Substring("Bearer ".Length)
                    : this.azureToken;

                // Split JWT token (header.payload.signature)
                var parts = tokenValue.Split('.');
                if (parts.Length != 3)
                {
                    throw new ArgumentException("Invalid JWT token format");
                }

                // Decode the payload (second part)
                string payload = parts[1];
                
                // Add padding if needed for base64 decoding
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                // Decode base64 payload
                byte[] payloadBytes = Convert.FromBase64String(payload);
                string payloadJson = Encoding.UTF8.GetString(payloadBytes);

                // Parse the JWT payload
                using (JsonDocument doc = JsonDocument.Parse(payloadJson))
                {
                    var root = doc.RootElement;

                    // Try different claim names for user email
                    string[] emailClaims = { "upn", "email", "preferred_username", "unique_name" };
                    
                    foreach (string claim in emailClaims)
                    {
                        if (root.TryGetProperty(claim, out JsonElement element))
                        {
                            string value = element.GetString();
                            if (!string.IsNullOrEmpty(value) && value.Contains("@"))
                            {
                                return value;
                            }
                        }
                    }

                    // If no email found, try to get from 'oid' (object ID) + tenant info
                    // This is a fallback that might not always work
                    throw new InvalidOperationException("No email address found in Azure token claims");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse user information from Azure token: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create a resource group if it doesn't exist.
        /// </summary>
        /// <param name="resourceGroupName">The resource group name.</param>
        /// <param name="location">The location for the resource group.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        public async Task CreateResourceGroupIfNotExistsAsync(
            string resourceGroupName,
            string location,
            CancellationToken cancellationToken = default)
        {
            try
            {
                HttpClient httpClient = this.BuildArmHttpClient();

                string resourcePath = $"subscriptions/{this.subscriptionId}/resourceGroups/{resourceGroupName}";
                
                // First check if the resource group exists
                HttpRequestMessage checkRequest = new HttpRequestMessage(HttpMethod.Get, $"{resourcePath}?api-version=2021-04-01");
                checkRequest.Headers.Add("Authorization", this.azureToken);

                HttpResponseMessage checkResponse = await httpClient.SendAsync(checkRequest, cancellationToken).ConfigureAwait(false);
                
                if (checkResponse.StatusCode == HttpStatusCode.OK)
                {
                    // Resource group already exists, no need to create
                    return;
                }
                else if (checkResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    // Resource group doesn't exist, create it
                    var resourceGroupPayload = new
                    {
                        location = location,
                        tags = new
                        {
                            CreatedBy = "FabricUpgradePowerShellModule",
                            Purpose = "ADF-to-Fabric-Migration"
                        }
                    };

                    string jsonPayload = JsonConvert.SerializeObject(resourceGroupPayload, Formatting.Indented);
                    
                    Console.WriteLine("=== RESOURCE GROUP CREATION ===");
                    Console.WriteLine($"Creating resource group: {resourceGroupName}");
                    Console.WriteLine($"Location: {location}");
                    Console.WriteLine("JSON Payload:");
                    Console.WriteLine(jsonPayload);
                    Console.WriteLine("================================");

                    HttpRequestMessage createRequest = new HttpRequestMessage(HttpMethod.Put, $"{resourcePath}?api-version=2021-04-01")
                    {
                        Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                    };
                    createRequest.Headers.Add("Authorization", this.azureToken);

                    Console.WriteLine($"Request URL: PUT https://management.azure.com/{resourcePath}?api-version=2021-04-01");

                    HttpResponseMessage createResponse = await httpClient.SendAsync(createRequest, cancellationToken).ConfigureAwait(false);
                    string createResponsePayload = await createResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Console.WriteLine("=== RESOURCE GROUP CREATION RESPONSE ===");
                    Console.WriteLine($"Status Code: {createResponse.StatusCode}");
                    Console.WriteLine("Response Payload:");
                    try
                    {
                        JObject formattedResponse = JObject.Parse(createResponsePayload);
                        Console.WriteLine(formattedResponse.ToString(Formatting.Indented));
                    }
                    catch
                    {
                        Console.WriteLine(createResponsePayload);
                    }
                    Console.WriteLine("=========================================");

                    if (!createResponse.IsSuccessStatusCode)
                    {
                        throw new Exception($"Failed to create resource group '{resourceGroupName}'. Status: {createResponse.StatusCode}, Response: {createResponsePayload}");
                    }

                    Console.WriteLine($"✓ Successfully created resource group '{resourceGroupName}' in location '{location}'");
                }
                else
                {
                    string errorPayload = await checkResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception($"Failed to check resource group '{resourceGroupName}'. Status: {checkResponse.StatusCode}, Response: {errorPayload}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
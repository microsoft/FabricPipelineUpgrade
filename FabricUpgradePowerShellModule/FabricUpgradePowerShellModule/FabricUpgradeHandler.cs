// <copyright file="FabricUpgradeHandler.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.ExportMachines;
using FabricUpgradePowerShellModule.Importers;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.UpgradeMachines;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule
{
    /// <summary>
    /// This class does all of the actual "work" exposed by the PowerShell Module.
    /// By separating the exposed commands from the implementation, we can test.
    /// </summary>
    public class FabricUpgradeHandler
    {
        /// <summary>
        /// This AlertCollector accumulates the Alerts generated during an Import/Upgrade/Export process.
        /// </summary>
        private AlertCollector alerts = new AlertCollector();

        /// <summary>
        /// Whether verbose logging is enabled for this handler instance.
        /// </summary>
        private readonly bool verbose;

        public FabricUpgradeHandler(bool verbose = false) 
        { 
            this.verbose = verbose;
        }

        /// <summary>
        /// Import an ADF Support File (zip file).
        /// </summary>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <param name="fileName">The name of the ADF support file to import.</param>
        /// <param name="includeUnusedResources">Whether to include datasets and linked services that are not used by any pipelines.</param>
        /// <returns>A FabricUpgradeProgress that contains the unzipped contents of the ADF Support File.</returns>
        public FabricUpgradeProgress ImportAdfSupportFile(
            string progressString,
            string fileName,
            bool includeUnusedResources,
            CancellationToken cancellationToken = default)
        {
            if (!this.CheckProgress(progressString, out FabricUpgradeProgress progress))
            {
                return progress;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                }
                .WithAlert(
                    new FabricUpgradeAlert()
                    {
                        Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                        Details = "Filename is required for ADF Support File import.",
                    });
            }

            AdfSupportFileImporter fileImporter = new AdfSupportFileImporter(progress, fileName, this.alerts);
            return fileImporter.Import(includeUnusedResources);
        }

        /// <summary>
        /// Import ADF resources directly from Azure Data Factory using REST APIs.
        /// </summary>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <param name="subscriptionId">Azure subscription ID for ADF API access.</param>
        /// <param name="resourceGroupName">Resource group name for ADF API access.</param>
        /// <param name="factoryName">Data factory name for ADF API access.</param>
        /// <param name="adfToken">The ADF token used for authentication.</param>
        /// <param name="pipelineResourceId">Optional specific pipeline resource Id to import.</param>
        /// <param name="includeUnusedResources">Whether to include datasets and linked services that are not used by any pipelines.</param>
        /// <returns>A FabricUpgradeProgress that contains the imported ADF resources.</returns>
        public async Task<FabricUpgradeProgress> ImportAdfFactoryAsync(
            string progressString,
            string subscriptionId,
            string resourceGroupName,
            string factoryName,
            string adfToken,
            string pipelineResourceId,
            bool includeUnusedResources,
            CancellationToken cancellationToken = default)
        {
            if (!this.CheckProgress(progressString, out FabricUpgradeProgress progress))
            {
                return progress;
            }

            if (string.IsNullOrEmpty(subscriptionId) || 
                string.IsNullOrEmpty(resourceGroupName) || 
                string.IsNullOrEmpty(factoryName) || 
                string.IsNullOrEmpty(adfToken))
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                }
                .WithAlert(
                    new FabricUpgradeAlert()
                    {
                        Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                        Details = "SubscriptionId, ResourceGroupName, FactoryName, and AdfToken are all required for ADF API import.",
                    });
            }

            AdfApiImporter apiImporter = new AdfApiImporter(
                progress, 
                subscriptionId, 
                resourceGroupName, 
                factoryName, 
                adfToken, 
                pipelineResourceId, 
                this.alerts);

            return await apiImporter.ImportAsync(includeUnusedResources, cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Accept a Progress that includes the result of Import-AdfSupportFile and
        /// upgrade it to a set of Fabric Resource descriptions that can be exported
        /// by Export-FabricResources.
        /// </summary>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <returns>A FabricUpgradeProgress that contains 'instructions' to Export-FabricResources.</returns>
        public FabricUpgradeProgress ConvertToFabricResources(
            string progressString)
        {
            if (!this.CheckProgress(progressString, out FabricUpgradeProgress progress))
            {
                return progress;
            }

            if (!progress.Result.ContainsKey(FabricUpgradeProgress.ImportedResourcesKey))
            {
                this.alerts.AddPermanentError("ConvertTo-FabricResources expects imported ADF resources.");
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.alerts.ToList(),
                };
            }

            JToken adfResourcesToken = progress.Result[FabricUpgradeProgress.ImportedResourcesKey];

            UpgradePackage upgradePackage = UpgradePackage.FromJToken(adfResourcesToken);

            if (upgradePackage.Type == UpgradePackage.UpgradePackageType.AdfSupportFile)
            {
                AdfSupportFileUpgradeMachine machine = new AdfSupportFileUpgradeMachine(
                    (JObject)adfResourcesToken,
                    progress.Resolutions,
                    this.alerts);

                FabricUpgradeProgress convertResult = machine.Upgrade();

                return convertResult;
            }

            return new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Failed,
            }
            .WithAlert(new FabricUpgradeAlert()
            {
                Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                Details = $"FabricUpgrade does not support package type '{upgradePackage.Type}'.",
            });
        }

        /// <summary>
        /// Accept a Progress that includes the result of ConvertTo-FabricResources and
        /// selects only alerts and state
        /// </summary>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <returns>A FabricUpgradeProgress that contains state and alerts.</returns>
        public FabricUpgradeProgress SelectWhatIf(
            string progressString)
        {
            if (!this.CheckValidJSON(progressString, out FabricUpgradeProgress previousProgress))
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.alerts.ToList(),
                };
            }

            List<FabricUpgradeAlert> alerts = new List<FabricUpgradeAlert>();
            foreach (FabricUpgradeAlert alert in previousProgress.Alerts)
            {
                alerts.Add(alert);
            }
            return new FabricUpgradeProgress()
            {
                State = previousProgress.State,
                Alerts = alerts.ToList(),
            };
        }

        /// <summary>
        /// Prepend the resolutions in the file to the resolutions we already have.
        /// </summary>
        /// <remarks>
        /// Newer resolutions will take precendence over older resolutions.
        /// </remarks>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <param name="resolutionsFilename">The filename to load.</param>
        /// <returns>A FabricUpgradeProgress that includes the new resolutions.</returns>
        public FabricUpgradeProgress ImportFabricResolutions(
            string progressString,
            string resolutionsFilename)
        {
            if (!this.CheckProgress(progressString, out FabricUpgradeProgress progress))
            {
                return progress;
            }

            string detailsIfFail = null;
            try
            {
                detailsIfFail = $"Failed to load resolutions file '{resolutionsFilename}'.";
                string resolutionsFileData = File.ReadAllText(resolutionsFilename);

                detailsIfFail = $"Failed to parse contents of '{resolutionsFilename}'.";
                List<FabricUpgradeResolution> newResolutions = JsonConvert.DeserializeObject<List<FabricUpgradeResolution>>(resolutionsFileData);

                List<FabricUpgradeResolution> resolutions = newResolutions;
                resolutions.AddRange(progress.Resolutions);

                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                    Alerts = this.alerts.ToList(),
                    Result = progress.Result,
                    Resolutions = resolutions,
                };
            }
            catch (Exception)
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                }
                .WithAlert(
                    new FabricUpgradeAlert()
                    {
                        Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                        Details = detailsIfFail,
                    });
            }
        }

        /// <summary>
        /// Prepend one resolutions to the resolutions we already have.
        /// </summary>
        /// <remarks>
        /// Newer resolutions will take precendence over older resolutions.
        /// </remarks>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <param name="resolution">The resolution to add.</param>
        /// <returns>A FabricUpgradeProgress that includes the new resolution.</returns>
        public FabricUpgradeProgress AddFabricResolution(
            string progressString,
            string resolution)
        {
            if (!this.CheckProgress(progressString, out FabricUpgradeProgress progress))
            {
                return progress;
            }

            FabricUpgradeResolution newResolution = JsonConvert.DeserializeObject<FabricUpgradeResolution>(resolution);
            // TODO: Handle parsing error

            progress.Resolutions.Add(newResolution);

            return progress;
        }

        /// <summary>
        /// Export the Fabric Resources by following the instructions in the progress.
        /// </summary>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <param name="region">The region of the user's workspace.</param>
        /// <param name="workspace">The workspace ID (GUID) or name. If GUID, uses existing workspace; if name, searches for existing workspace or creates new one.</param>
        /// <param name="fabricToken">The PowerBI AAD token to authenticate/authorize access to the workspace.</param>
        /// <param name="cancellationToken"/>
        /// <returns>A FabricUpgradeProgress that describes the created/updated resources.</returns>
        public async Task<FabricUpgradeProgress> ExportFabricResourcesAsync(
            string progressString,
            string region,
            string workspace,
            string fabricToken,
            CancellationToken cancellationToken = default)
        {
            string workspaceId = workspace;

            // Check if workspace parameter is a name (not a GUID) and resolve to workspace ID
            // Skip this for obvious test values to avoid API calls in test scenarios
            if (!string.IsNullOrEmpty(workspace) && !IsValidGuid(workspace) && !IsTestValue(workspace))
            {
                if (this.verbose)
                {
                    Console.WriteLine($"Workspace parameter '{workspace}' appears to be a name rather than an ID. Resolving to workspace ID...");
                }

                try
                {
                    var fabricAdminClient = new FabricAdminApiClient(region, fabricToken, this.verbose);
                    string resolvedWorkspaceId = await this.GetWorkspaceIdByNameAsync(fabricAdminClient, workspace, cancellationToken).ConfigureAwait(false);
                    
                    if (this.verbose)
                    {
                        Console.WriteLine($"✓ Resolved workspace name '{workspace}' to ID: {resolvedWorkspaceId}");
                    }
                    
                    workspaceId = resolvedWorkspaceId;
                }
                catch (Exception ex)
                {
                    return new FabricUpgradeProgress()
                    {
                        State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                        Alerts = new List<FabricUpgradeAlert>
                        {
                            new FabricUpgradeAlert()
                            {
                                Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                                Details = $"Failed to resolve workspace name '{workspace}' to workspace ID: {ex.Message}"
                            }
                        }
                    };
                }
            }
            else if (!string.IsNullOrEmpty(workspace) && this.verbose && !IsTestValue(workspace))
            {
                Console.WriteLine($"Using workspace ID directly: {workspace}");
            }

            if (!this.CheckProgress(progressString, out FabricUpgradeProgress progress))
            {
                return progress;
            }

            if (!progress.Result.ContainsKey(FabricUpgradeProgress.ExportableFabricResourcesKey))
            {
                this.alerts.AddPermanentError("Export-FabricResources expects exportable Fabric resources.");
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.alerts.ToList(),
                };
            }

            FabricExportMachine machine = new FabricExportMachine(
                    progress.Result,
                    region,
                    workspaceId,
                    fabricToken,
                    progress.Resolutions,
                    this.alerts,
                    this.verbose);

            return await machine.ExportAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Create a new workspace with capacity if needed, then export the Fabric Resources.
        /// </summary>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <param name="region">The region of the workspace.</param>
        /// <param name="workspace">The workspace ID (GUID) or name. If GUID, uses existing workspace; if name, searches for existing workspace or creates new one with that name.</param>
        /// <param name="fabricToken">The Fabric user access token.</param>
        /// <param name="factoryResourceId">Azure Resource ID of the source Data Factory (e.g., '/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.DataFactory/factories/{name}'). Used for determining subscription, resource group, and factory name for capacity and workspace creation.</param>
        /// <param name="azureToken">Azure access token for capacity operations (required when creating new capacity).</param>
        /// <param name="capacityName">Name of existing capacity to use, or name for new capacity to create (optional, defaults to auto-generated name for new capacity).</param>
        /// <param name="skuName">SKU for the new capacity (optional, defaults to F2).</param>
        /// <param name="adminMembers">Admin members for the capacity (optional).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A FabricUpgradeProgress that describes the created/updated resources.</returns>
        public async Task<FabricUpgradeProgress> ExportFabricResourcesWithWorkspaceCreationAsync(
            string progressString,
            string region,
            string workspace,
            string fabricToken,
            string factoryResourceId,
            string azureToken = null,
            string capacityName = null,
            string skuName = "F2",
            List<string> adminMembers = null,
            CancellationToken cancellationToken = default)
        {
            if (!this.CheckProgress(progressString, out FabricUpgradeProgress progress))
            {
                return progress;
            }

            if (!progress.Result.ContainsKey(FabricUpgradeProgress.ExportableFabricResourcesKey))
            {
                this.alerts.AddPermanentError("Export-FabricResources expects exportable Fabric resources.");
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.alerts.ToList(),
                };
            }

            // Parse factory resource ID to extract subscription, resource group, and factory name
            if (string.IsNullOrEmpty(factoryResourceId))
            {
                this.alerts.AddPermanentError("FactoryResourceId is required for workspace creation.");
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.alerts.ToList(),
                };
            }

            var factoryInfo = ParseFactoryResourceId(factoryResourceId);
            if (factoryInfo == null)
            {
                this.alerts.AddPermanentError($"Invalid FactoryResourceId format: '{factoryResourceId}'. Expected format: '/subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.DataFactory/factories/{{factoryName}}'");
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.alerts.ToList(),
                };
            }

            string finalWorkspaceId = null;
            string workspaceName = null;

            // Determine if workspace parameter is a GUID (existing workspace) or name
            if (!string.IsNullOrEmpty(workspace))
            {
                if (IsValidGuid(workspace))
                {
                    // It's a GUID - use existing workspace
                    finalWorkspaceId = workspace;
                    
                    if (this.verbose)
                    {
                        Console.WriteLine($"Using existing workspace ID: {workspace}");
                    }
                }
                else
                {
                    // It's a name - try to find existing workspace first
                    if (this.verbose)
                    {
                        Console.WriteLine($"Workspace parameter '{workspace}' appears to be a name. Searching for existing workspace...");
                    }

                    try
                    {
                        var fabricAdminClient = new FabricAdminApiClient(region, fabricToken, this.verbose);
                        finalWorkspaceId = await this.GetWorkspaceIdByNameAsync(fabricAdminClient, workspace, cancellationToken).ConfigureAwait(false);

                        if (this.verbose)
                        {
                            Console.WriteLine($"✓ Found existing workspace: '{workspace}' (ID: {finalWorkspaceId})");
                        }
                    }
                    catch (Exception)
                    {
                        // Workspace not found - we'll create a new one with this name
                        workspaceName = workspace;
                        
                        if (this.verbose)
                        {
                            Console.WriteLine($"Workspace '{workspace}' not found. Will create new workspace with this name.");
                        }
                    }
                }
            }

            // Create workspace if no existing workspace was found/specified
            if (string.IsNullOrEmpty(finalWorkspaceId))
            {
                try
                {
                    if (this.verbose)
                    {
                        if (!string.IsNullOrEmpty(workspaceName))
                        {
                            Console.WriteLine($"Creating new workspace with name: '{workspaceName}'");
                        }
                        else
                        {
                            Console.WriteLine("Creating new workspace (name will be auto-generated)");
                        }
                    }

                    if (this.verbose)
                    {
                        Console.WriteLine($"Using factory resource information:");
                        Console.WriteLine($"  Factory Name: {factoryInfo.FactoryName}");
                        Console.WriteLine($"  Subscription ID: {factoryInfo.SubscriptionId}");
                        Console.WriteLine($"  Resource Group: {factoryInfo.ResourceGroupName}");
                        Console.WriteLine($"  Capacity will be created in the same subscription/resource group");
                    }

                    finalWorkspaceId = await this.CreateWorkspaceWithCapacityAsync(
                        factoryInfo.SubscriptionId,
                        factoryInfo.ResourceGroupName,
                        azureToken,
                        fabricToken,
                        region,
                        workspaceName, // This will be null if workspace parameter was a GUID, or the name if it was a string
                        capacityName,
                        skuName,
                        adminMembers,
                        factoryInfo,
                        cancellationToken).ConfigureAwait(false);

                    if (this.verbose)
                    {
                        Console.WriteLine($"Created new workspace with ID: {finalWorkspaceId}");
                    }
                }
                catch (Exception ex)
                {
                    this.alerts.AddPermanentError($"Failed to create workspace: {ex.Message}");
                    return new FabricUpgradeProgress()
                    {
                        State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                        Alerts = this.alerts.ToList(),
                    };
                }
            }

            // Now export to the workspace
            return await this.ExportFabricResourcesAsync(
                progressString,
                region,
                finalWorkspaceId,
                fabricToken,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Create a new workspace with a new or existing capacity.
        /// </summary>
        /// <param name="subscriptionId">Azure subscription ID where capacity is located or will be created.</param>
        /// <param name="resourceGroupName">Resource group name where capacity is located or will be created.</param>
        /// <param name="azureToken">Azure access token (required for both new capacity creation and existing capacity validation).</param>
        /// <param name="fabricToken">Fabric access token.</param>
        /// <param name="region">Fabric region.</param>
        /// <param name="workspaceName">Optional workspace name.</param>
        /// <param name="capacityName">Name of existing capacity to use, or name for new capacity to create (optional).</param>
        /// <param name="skuName">Capacity SKU name (for new capacity).</param>
        /// <param name="adminMembers">Capacity admin members (for new capacity).</param>
        /// <param name="factoryInfo">Factory information parsed from resource ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The workspace ID.</returns>
        private async Task<string> CreateWorkspaceWithCapacityAsync(
            string subscriptionId,
            string resourceGroupName,
            string azureToken,
            string fabricToken,
            string region,
            string workspaceName,
            string capacityName,
            string skuName,
            List<string> adminMembers,
            FactoryInfo factoryInfo,
            CancellationToken cancellationToken)
        {
            // Generate workspace name using factory name
            workspaceName = WorkspaceCreationHelper.GenerateWorkspaceName(factoryInfo.FactoryName, workspaceName);
            
            // Always log the workspace name that will be created
            Console.WriteLine($"Creating Fabric workspace: {workspaceName}");
            
            string fabricCapacityGuid = null;
            bool usingExistingCapacity = false;

            // Determine if we should use existing capacity or create new one
            if (!string.IsNullOrEmpty(capacityName))
            {
                // Try to use existing capacity with the provided name
                if (this.verbose)
                {
                    Console.WriteLine($"Attempting to use existing capacity: '{capacityName}'");
                    Console.WriteLine("Capacity search in the same subscription and resource group as source ADF");
                }

                try
                {
                    // Validate that the capacity exists in Azure
                    var armClient = new AzureResourceManagerClient(subscriptionId, azureToken);
                    bool capacityExists = await armClient.ValidateFabricCapacityExistsInAzureAsync(
                        resourceGroupName,
                        capacityName,
                        cancellationToken).ConfigureAwait(false);

                    if (capacityExists)
                    {
                        usingExistingCapacity = true;
                        
                        if (this.verbose)
                        {
                            Console.WriteLine($"✓ Found existing capacity: '{capacityName}'");
                            Console.WriteLine($"✓ Validated existing capacity exists in Azure");
                            Console.WriteLine($"Now retrieving Fabric capacity GUID using Fabric Admin API...");
                        }

                        // Get the Fabric capacity GUID using Fabric Admin API
                        var fabricAdminClient = new FabricAdminApiClient(region, fabricToken, this.verbose);
                        fabricCapacityGuid = await this.GetFabricCapacityGuidAsync(fabricAdminClient, capacityName, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        if (this.verbose)
                        {
                            Console.WriteLine($"Capacity '{capacityName}' not found in Azure. Will create new capacity with this name.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (this.verbose)
                    {
                        Console.WriteLine($"Failed to find existing capacity '{capacityName}': {ex.Message}");
                        Console.WriteLine($"Will create new capacity with this name.");
                    }
                }
            }

            if (!usingExistingCapacity)
            {
                // Create new capacity (using provided name or auto-generated)
                capacityName = WorkspaceCreationHelper.GenerateCapacityName(factoryInfo.FactoryName, capacityName);
                skuName = WorkspaceCreationHelper.ValidateAndGetDefaultSku(skuName);

                if (this.verbose)
                {
                    Console.WriteLine($"Generated/Using capacity name: '{capacityName}'");
                    
                    // Show administrator information
                    if (adminMembers != null && adminMembers.Count > 0)
                    {
                        Console.WriteLine($"Capacity administrators: {string.Join(", ", adminMembers)}");
                    }
                    else
                    {
                        Console.WriteLine("No capacity administrators specified - will use current user from Azure token");
                    }
                }

                // Determine capacity location - use user region parameter for mapping
                string capacityLocation = this.MapFabricRegionToAzureRegion(region);
                
                if (this.verbose)
                {
                    Console.WriteLine($"Using user region for capacity location: User Region='{region}' -> Azure Region='{capacityLocation}'");
                    Console.WriteLine($"Creating capacity in the same subscription and resource group as source ADF");
                }

                if (this.verbose)
                {
                    Console.WriteLine($"Creating new capacity: '{capacityName}'");
                    Console.WriteLine($"Using SKU: '{skuName}'");
                    Console.WriteLine($"Capacity location: '{capacityLocation}'");
                    Console.WriteLine("========================================");
                    Console.WriteLine("INITIATING AZURE FABRIC CAPACITY CREATION");
                    Console.WriteLine("========================================");
                }

                // Create capacity using the determined location
                var armClient = new AzureResourceManagerClient(subscriptionId, azureToken);

                // Ensure resource group exists before creating capacity
                if (this.verbose)
                {
                    Console.WriteLine($"Ensuring resource group '{resourceGroupName}' exists...");
                }
                
                await armClient.CreateResourceGroupIfNotExistsAsync(resourceGroupName, capacityLocation, cancellationToken).ConfigureAwait(false);

                if (this.verbose)
                {
                    Console.WriteLine($"✓ Resource group '{resourceGroupName}' is ready");
                }

                string capacityResult = await armClient.CreateFabricCapacityAsync(
                    resourceGroupName,
                    capacityName,
                    capacityLocation,
                    skuName,
                    adminMembers,
                    cancellationToken).ConfigureAwait(false);

                if (this.verbose)
                {
                    Console.WriteLine($"✓ Successfully created Fabric capacity in Azure");
                    Console.WriteLine($"✓ Capacity Name: {capacityName}");
                    Console.WriteLine($"  Location: {capacityLocation}");
                    Console.WriteLine($"Waiting for capacity provisioning to complete...");
                }

                // Wait for capacity to reach terminal provisioning state
                await this.WaitForCapacityProvisioningAsync(armClient, resourceGroupName, capacityName, cancellationToken).ConfigureAwait(false);

                if (this.verbose)
                {
                    Console.WriteLine($"✓ Capacity provisioning completed successfully");
                    Console.WriteLine($"Now retrieving Fabric capacity GUID using Fabric Admin API...");
                }

                // Get the Fabric capacity GUID using Fabric Admin API
                var fabricAdminClient = new FabricAdminApiClient(region, fabricToken, this.verbose);
                fabricCapacityGuid = await this.GetFabricCapacityGuidAsync(fabricAdminClient, capacityName, cancellationToken).ConfigureAwait(false);
            }

            if (this.verbose)
            {
                Console.WriteLine($"✓ Found Fabric capacity GUID: {fabricCapacityGuid}");
            }

            // Create workspace using Fabric Admin API
            if (this.verbose)
            {
                Console.WriteLine($"Creating Fabric workspace '{workspaceName}'...");
                Console.WriteLine("========================================");
                Console.WriteLine("INITIATING FABRIC WORKSPACE CREATION");
                Console.WriteLine("========================================");
            }

            var fabricAdminClientForWorkspace = new FabricAdminApiClient(region, fabricToken, this.verbose);
            string workspaceDescription = $"Workspace created for migrating Azure Data Factory '{factoryInfo.FactoryName}' to Microsoft Fabric - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
                
            string workspaceId = await fabricAdminClientForWorkspace.CreateWorkspaceAsync(
                workspaceName,
                fabricCapacityGuid,
                workspaceDescription,
                cancellationToken).ConfigureAwait(false);

            if (this.verbose)
            {
                Console.WriteLine($"✓ Successfully created Fabric workspace");
                Console.WriteLine($"  Workspace ID: {workspaceId}");
                Console.WriteLine($"  Workspace Name: '{workspaceName}'");
                Console.WriteLine($"  Associated Capacity: '{capacityName}' ({fabricCapacityGuid})");
                Console.WriteLine($"Ready to export Fabric resources to the new workspace.");
            }

            // Always log workspace ID and browser link (regardless of verbose setting)
            Console.WriteLine($"Workspace ID: {workspaceId}");
            Console.WriteLine($"Navigate to the Workspace URL: {this.BuildWorkspaceUrl(region, workspaceId)}");

            return workspaceId;
        }

        /// <summary>
        /// Get the Fabric capacity GUID by listing all capacities and finding the one with matching name.
        /// </summary>
        /// <param name="fabricAdminClient">The Fabric Admin API client.</param>
        /// <param name="capacityName">The name of the capacity to find.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The Fabric capacity GUID.</returns>
        private async Task<string> GetFabricCapacityGuidAsync(
            FabricAdminApiClient fabricAdminClient,
            string capacityName,
            CancellationToken cancellationToken)
        {
            try
            {
                if (this.verbose)
                {
                    Console.WriteLine("=== RETRIEVING FABRIC CAPACITY GUID ===");
                    Console.WriteLine($"Searching for capacity: {capacityName}");
                    Console.WriteLine("Calling Fabric Admin API to list all capacities...");
                }

                // Use the optimized capacity lookup method
                JObject matchingCapacity = await fabricAdminClient.GetCapacityByNameAsync(capacityName, cancellationToken).ConfigureAwait(false);

                // Extract the capacity GUID
                string capacityGuid = matchingCapacity.SelectToken("$.id")?.ToString();
                if (string.IsNullOrEmpty(capacityGuid))
                {
                    if (this.verbose)
                    {
                        Console.WriteLine("Capacity details:");
                        Console.WriteLine(matchingCapacity.ToString(Formatting.Indented));
                    }
                    throw new Exception($"Found capacity '{capacityName}' but could not extract its ID from the response.");
                }

                if (this.verbose)
                {
                    Console.WriteLine($"✓ Successfully retrieved Fabric capacity GUID: {capacityGuid}");
                    Console.WriteLine("=======================================");
                }

                return capacityGuid;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve Fabric capacity GUID for '{capacityName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get workspace ID by searching for a workspace with the given name.
        /// </summary>
        /// <param name="fabricAdminClient">The Fabric Admin API client.</param>
        /// <param name="workspaceName">The name of the workspace to find.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The workspace ID.</returns>
        private async Task<string> GetWorkspaceIdByNameAsync(
            FabricAdminApiClient fabricAdminClient,
            string workspaceName,
            CancellationToken cancellationToken)
        {
            try
            {
                if (this.verbose)
                {
                    Console.WriteLine("=== RETRIEVING WORKSPACE ID BY NAME ===");
                    Console.WriteLine($"Searching for workspace: {workspaceName}");
                    Console.WriteLine("Calling Fabric Admin API to list all workspaces...");
                }

                // List all workspaces available to the user
                var workspaces = await fabricAdminClient.ListWorkspacesAsync(cancellationToken).ConfigureAwait(false);

                if (this.verbose)
                {
                    Console.WriteLine($"Found {workspaces.Count} total workspaces in Fabric");
                    Console.WriteLine("Searching for matching workspace by name...");
                }

                // Find the workspace with the matching name
                JObject matchingWorkspace = null;
                foreach (JObject workspace in workspaces.Cast<JObject>())
                {
                    string fabricWorkspaceName = workspace.SelectToken("$.displayName")?.ToString() 
                                               ?? workspace.SelectToken("$.name")?.ToString();

                    if (this.verbose)
                    {
                        string wsId = workspace.SelectToken("$.id")?.ToString();
                        Console.WriteLine($"  - Workspace: '{fabricWorkspaceName}' (ID: {wsId})");
                    }

                    if (string.Equals(fabricWorkspaceName, workspaceName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingWorkspace = workspace;
                        if (this.verbose)
                        {
                            Console.WriteLine($"✓ Found matching workspace: '{fabricWorkspaceName}'");
                        }
                        break;
                    }
                }

                if (matchingWorkspace == null)
                {
                    throw new Exception($"Could not find Fabric workspace with name '{workspaceName}'. " +
                        "The workspace may not be visible to the current user, or it may not exist. " +
                        "Please verify the workspace name is correct and that you have access to it.");
                }

                // Extract the workspace ID
                string workspaceId = matchingWorkspace.SelectToken("$.id")?.ToString();
                if (string.IsNullOrEmpty(workspaceId))
                {
                    if (this.verbose)
                    {
                        Console.WriteLine("Workspace details:");
                        Console.WriteLine(matchingWorkspace.ToString(Formatting.Indented));
                    }
                    throw new Exception($"Found workspace '{workspaceName}' but could not extract its ID from the response.");
                }

                if (this.verbose)
                {
                    Console.WriteLine($"✓ Successfully retrieved workspace ID: {workspaceId}");
                    Console.WriteLine("======================================");
                }

                return workspaceId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve workspace ID for '{workspaceName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Information parsed from a Data Factory Resource ID.
        /// </summary>
        private class FactoryInfo
        {
            public string SubscriptionId { get; set; }
            public string ResourceGroupName { get; set; }
            public string FactoryName { get; set; }
        }

        /// <summary>
        /// Parse a Data Factory Resource ID into its components.
        /// </summary>
        /// <param name="factoryResourceId">The factory resource ID (e.g., '/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.DataFactory/factories/{name}').</param>
        /// <returns>Parsed factory information, or null if the format is invalid.</returns>
        private static FactoryInfo ParseFactoryResourceId(string factoryResourceId)
        {
            if (string.IsNullOrEmpty(factoryResourceId))
                return null;

            // Expected format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DataFactory/factories/{factoryName}
            var parts = factoryResourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // Should have at least 8 parts: [subscriptions, {sub}, resourceGroups, {rg}, providers, Microsoft.DataFactory, factories, {name}]
            if (parts.Length < 8)
                return null;

            try
            {
                if (!string.Equals(parts[0], "subscriptions", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(parts[2], "resourceGroups", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(parts[4], "providers", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(parts[5], "Microsoft.DataFactory", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(parts[6], "factories", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return new FactoryInfo
                {
                    SubscriptionId = parts[1],
                    ResourceGroupName = parts[3],
                    FactoryName = parts[7]
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Map Fabric region names to Azure region names.
        /// </summary>
        /// <param name="fabricRegion">The Fabric region name.</param>
        /// <returns>The corresponding Azure region name.</returns>
        private string MapFabricRegionToAzureRegion(string fabricRegion)
        {
            if (string.IsNullOrEmpty(fabricRegion))
            {
                return fabricRegion;
            }

            // Special mapping for msit region
            if (string.Equals(fabricRegion, "msit", StringComparison.OrdinalIgnoreCase))
            {
                if (this.verbose)
                {
                    Console.WriteLine($"Mapping Fabric region '{fabricRegion}' to Azure region 'westcentralus'");
                }
                return "westcentralus";
            }

            // For other regions, use as-is (they should map directly)
            return fabricRegion;
        }

        /// <summary>
        /// Build the Fabric workspace browser URL based on the region and workspace ID.
        /// </summary>
        /// <param name="region">The Fabric region.</param>
        /// <param name="workspaceId">The workspace ID (GUID).</param>
        /// <returns>The browser URL for the workspace.</returns>
        private string BuildWorkspaceUrl(string region, string workspaceId)
        {
            string baseUrl = region switch
            {
                "daily" => "https://dailyapi.fabric.microsoft.com",
                "dxt" => "https://dxt.fabric.microsoft.com",
                "msit" => "https://msit.fabric.microsoft.com",
                "prod" => "https://fabric.microsoft.com",
                _ => "https://fabric.microsoft.com", // Default to prod
            };

            return $"{baseUrl}/groups/{workspaceId}/";
        }

        /// <summary>
        /// Wait for the capacity to reach a terminal provisioning state.
        /// </summary>
        /// <param name="armClient">The Azure Resource Manager client.</param>
        /// <param name="resourceGroupName">The resource group name.</param>
        /// <param name="capacityName">The capacity name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        private async Task WaitForCapacityProvisioningAsync(
            AzureResourceManagerClient armClient,
            string resourceGroupName,
            string capacityName,
            CancellationToken cancellationToken)
        {
            const int maxWaitTimeMinutes = 15; // Maximum wait time
            const int pollIntervalSeconds = 5; // Poll every 5 seconds
            
            var startTime = DateTime.UtcNow;
            var maxWaitTime = TimeSpan.FromMinutes(maxWaitTimeMinutes);

            if (this.verbose)
            {
                Console.WriteLine($"Waiting for capacity '{capacityName}' to reach terminal provisioning state...");
                Console.WriteLine($"Will poll every {pollIntervalSeconds} seconds for up to {maxWaitTimeMinutes} minutes");
            }

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                try
                {
                    var provisioningState = await armClient.GetCapacityProvisioningStateAsync(
                        resourceGroupName, 
                        capacityName, 
                        cancellationToken).ConfigureAwait(false);

                    if (this.verbose)
                    {
                        Console.WriteLine($"Capacity provisioning state: {provisioningState}");
                    }

                    // Terminal states (both success and failure)
                    if (string.Equals(provisioningState, "Succeeded", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(provisioningState, "Failed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(provisioningState, "Canceled", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(provisioningState, "Succeeded", StringComparison.OrdinalIgnoreCase))
                        {
                            if (this.verbose)
                            {
                                Console.WriteLine($"✓ Capacity provisioning succeeded after {DateTime.UtcNow - startTime:mm\\:ss}");
                            }
                            return; // Success
                        }
                        else
                        {
                            throw new Exception($"Capacity provisioning failed with state: {provisioningState}");
                        }
                    }

                    // Still provisioning - wait and check again
                    if (this.verbose)
                    {
                        Console.WriteLine($"Capacity still provisioning, waiting {pollIntervalSeconds} seconds...");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw cancellation
                }
                catch (Exception ex)
                {
                    if (this.verbose)
                    {
                        Console.WriteLine($"Error checking provisioning state: {ex.Message}");
                        Console.WriteLine($"Will retry in {pollIntervalSeconds} seconds...");
                    }
                    
                    // Continue polling even if there's an error getting the state
                    await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), cancellationToken).ConfigureAwait(false);
                }
            }

            // Timeout reached
            throw new TimeoutException($"Capacity provisioning did not complete within {maxWaitTimeMinutes} minutes. " +
                "The capacity may still be provisioning in the background. Please check the Azure portal for status.");
        }

        /// <summary>
        /// Inspect the progress "so far" to see if we should continue.
        /// </summary>
        /// <remarks>
        /// This method also copies the Alerts from the previous progress to this.alerts.
        /// </remarks>
        /// <param name="previousResponse">The string sent by the client to represent the progress "so far."</param>
        /// <param name="currentProgress">An out parameter that holds the parsed progress.</param>
        /// <returns>True if and only if the previous progress is acceptable for continuing.</returns>
        private bool CheckProgress(
            string previousResponse,
            out FabricUpgradeProgress currentProgress)
        {
            if (!this.CheckValidJSON(previousResponse, out FabricUpgradeProgress previousProgress))
            {
                currentProgress = new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.alerts.ToList(),
                };
                return false;
            }
            foreach (FabricUpgradeAlert alert in previousProgress.Alerts)
            {
                this.alerts.AddAlert(alert);
            }
            currentProgress = previousProgress;
            return currentProgress.State == FabricUpgradeProgress.FabricUpgradeState.Succeeded;
        }

        private bool CheckValidJSON(string previousResponse, out FabricUpgradeProgress previousProgress)
        {
            try
            {
                previousProgress = FabricUpgradeProgress.FromString(previousResponse);
                return true;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                this.alerts.AddPermanentError("Input is not a valid JSON FabricUpgradeProgress.");
                previousProgress = null;
                return false;
            }
        }

        /// <summary>
        /// Check if a string is a valid GUID format.
        /// </summary>
        /// <param name="input">The string to check.</param>
        /// <returns>True if the string is a valid GUID format, false otherwise.</returns>
        public static bool IsValidGuid(string input)
        {
            return Guid.TryParse(input, out _);
        }

        /// <summary>
        /// Check if a value appears to be a test value that should not trigger real API calls.
        /// </summary>
        /// <param name="input">The string to check.</param>
        /// <returns>True if the string appears to be a test value, false otherwise.</returns>
        private static bool IsTestValue(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // Common test values that should not trigger API calls
            string[] testValues = { "wsId", "token", "test", "fake", "mock", "dummy" };
            
            return testValues.Any(testValue => 
                string.Equals(input, testValue, StringComparison.OrdinalIgnoreCase) ||
                input.ToLowerInvariant().Contains(testValue));
        }
    }
}

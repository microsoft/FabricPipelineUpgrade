// <copyright file="FabricExportMachine.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradePowerShellModule.Exceptions;
using FabricUpgradePowerShellModule.Exporters;
using FabricUpgradePowerShellModule.Models;
using FabricUpgradePowerShellModule.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.ExportMachines
{
    public class FabricExportMachine : ExportMachine
    {
        private List<ResourceExporter> exporters = new List<ResourceExporter>();
        private JObject exportResults = new JObject();

        public FabricExportMachine(
            JObject toExport,
            string region,
            string workspaceId,
            string fabricToken,
            List<FabricUpgradeResolution> resolutions,
            AlertCollector alerts,
            bool verbose = false)
            : base(toExport, workspaceId, resolutions, alerts)
        {
            this.Region = region;
            this.FabricToken = fabricToken;
            this.Verbose = verbose;
        }

        // The region of the user's workspace.
        protected string Region { get; private set; }

        // The user's PowerBI AAD token.
        protected string FabricToken { get; private set; }

        // Whether verbose logging is enabled.
        public bool Verbose { get; private set; }

        /// <inheritdoc/>
        public override async Task<FabricUpgradeProgress> ExportAsync(CancellationToken cancellationToken)
        {
            try
            {
                this.BuildAllExporters();
                this.CheckAllExportersBeforeExport();
                JObject exportResult = await this.ExportAllExportersAsync(cancellationToken).ConfigureAwait(false);
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                    Alerts = this.Alerts.ToList(),
                    Result = exportResult,
                };
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation - return failed state with cancellation info
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.Alerts.ToList(),
                    Result = this.BuildResultObject(),
                };
            }
            catch (UpgradeFailureException)
            {
                return new FabricUpgradeProgress()
                {
                    State = FabricUpgradeProgress.FabricUpgradeState.Failed,
                    Alerts = this.Alerts.ToList(),
                    Result = this.BuildResultObject(),
                };
            }
        }

        /// <summary>
        /// Build all of the Exporters described by the ExportObject.
        /// </summary>
        /// <exception cref="UpgradeFailureException"></exception>
        private void BuildAllExporters()
        {
            JArray toExport = (JArray)this.ExportObject.SelectToken(FabricUpgradeProgress.ExportableFabricResourcesKey);
            if (toExport == null)
            {
                this.Alerts.AddPermanentError("Cannot find fabricResources to export");
                throw new UpgradeFailureException("Construct");
            }

            foreach (var exportable in toExport)
            {
                ResourceExporter exporter = ResourceExporter.CreateResourceExporter(exportable, this);
                this.exporters.Add(exporter);
            }
        }

        /// <summary>
        /// Invoke CheckBeforeExports on all of the Exporters.
        /// </summary>
        /// <exception cref="UpgradeFailureException"></exception>
        private void CheckAllExportersBeforeExport()
        {
            // Show progress for pending pipeline exports
            int totalPipelines = this.exporters.Count(e => e.ResourceType == FabricUpgradeResourceTypes.DataPipeline);
            if (totalPipelines > 0)
            {
                Console.WriteLine($"Preparing to export {totalPipelines} pipeline(s) to Fabric workspace");
            }

            foreach (ResourceExporter exporter in this.exporters)
            {
                exporter.CheckBeforeExports(this.Alerts);
            }

            if (this.AlertsIndicateFailure())
            {
                throw new UpgradeFailureException("PreCheck");
            }
        }

        /// <summary>
        /// Invoke ExportAsync() on all of the Exporters.
        /// </summary>
        /// <remarks>
        /// This method also collects the ID of each Fabric Resource, so that later
        /// Exporters can resolve those values before Creating/Updating their resources.
        /// </remarks>
        /// <param name="cancellationToken"/>
        /// <returns>A JObject that is the Result in the FabricUpgradeProgress returned to the client.</returns>
        /// <exception cref="UpgradeFailureException"></exception>
        private async Task<JObject> ExportAllExportersAsync(CancellationToken cancellationToken)
        {
            // Track pipeline export progress
            int totalPipelines = this.exporters.Count(e => e.ResourceType == FabricUpgradeResourceTypes.DataPipeline);
            int exportedPipelines = 0;
            int failedPipelines = 0;
            int cancelledPipelines = 0;
            var failedPipelineNames = new List<string>();
            var skippedPipelineNames = new List<string>();
            var cancelledPipelineNames = new List<string>();
            bool operationCancelled = false;

            // Build dependency map for skipping dependent pipelines when a dependency fails
            var dependencyMap = BuildPipelineDependencyMap();

            foreach (ResourceExporter exporter in this.exporters)
            {
                // If cancellation was requested, stop processing remaining exporters immediately
                if (cancellationToken.IsCancellationRequested)
                {
                    operationCancelled = true;
                    
                    // Count remaining pipelines as cancelled
                    if (exporter.ResourceType == FabricUpgradeResourceTypes.DataPipeline)
                    {
                        cancelledPipelines++;
                        cancelledPipelineNames.Add(exporter.Name);
                    }
                    continue;
                }

                // Skip if this pipeline depends on a failed pipeline
                if (exporter.ResourceType == FabricUpgradeResourceTypes.DataPipeline && 
                    ShouldSkipDueToDependency(exporter.Name, failedPipelineNames, dependencyMap))
                {
                    skippedPipelineNames.Add(exporter.Name);
                    this.Alerts.AddAlert(new FabricUpgradeAlert()
                    {
                        Severity = FabricUpgradeAlert.AlertSeverity.Warning,
                        Details = $"Pipeline '{exporter.Name}' skipped due to failed dependency pipeline(s).",
                        SourcePipelineName = exporter.Name
                    });
                    
                    Console.WriteLine($"⏭  Pipeline '{exporter.Name}' skipped due to dependency failure");
                    continue;
                }

                try
                {
                    // Track the number of permanent errors before export to detect new failures
                    int permanentErrorsBefore = this.Alerts.Count(a => a.Severity == FabricUpgradeAlert.AlertSeverity.Permanent);

                    JObject uploadResult = await exporter.ExportAsync(
                        this.Region,
                        this.WorkspaceId,
                        this.FabricToken,
                        this.Alerts,
                        cancellationToken).ConfigureAwait(false);

                    if (this.Verbose)
                    {
                        Console.WriteLine("Export result:" + uploadResult.ToString());
                    }

                    // Check if new permanent errors were added (indicating failure)
                    int permanentErrorsAfter = this.Alerts.Count(a => a.Severity == FabricUpgradeAlert.AlertSeverity.Permanent);
                    bool exportFailed = permanentErrorsAfter > permanentErrorsBefore;

                    // Track pipeline export progress
                    if (exporter.ResourceType == FabricUpgradeResourceTypes.DataPipeline)
                    {
                        if (exportFailed)
                        {
                            failedPipelines++;
                            failedPipelineNames.Add(exporter.Name);
                        }
                        else
                        {
                            exportedPipelines++;
                        }
                        
                        if (totalPipelines > 1)
                        {
                            Console.WriteLine($"Pipeline export progress: {exportedPipelines + failedPipelines}/{totalPipelines} pipelines completed");
                        }
                    }

                    // Only add to export results and resolutions if export actually succeeded
                    if (!exportFailed)
                    {
                        this.exportResults[$"{exporter.Name}"] = uploadResult;

                        // Keep track of the Fabric Resource ID of each Fabric Resource that we create,
                        // so that later Exporters can resolve this value.
                        var newResolution = new FabricUpgradeResolution()
                        {
                            Type = FabricUpgradeResolution.ResolutionType.AdfResourceNameToFabricResourceId,
                            Key = $"{exporter.ResourceType}:{exporter.Name}",
                            Value = uploadResult.SelectToken("$.id")?.ToString(),
                        };
                        this.Resolutions.Add(newResolution);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Handle cancellation specifically - stop processing immediately
                    if (exporter.ResourceType == FabricUpgradeResourceTypes.DataPipeline)
                    {
                        cancelledPipelines++;
                        cancelledPipelineNames.Add(exporter.Name);
                        Console.WriteLine($"⚠  Pipeline '{exporter.Name}' export was cancelled");
                    }
                    
                    operationCancelled = true;
                    
                    // Count remaining pipelines as cancelled (those that haven't been processed yet)
                    var currentIndex = this.exporters.IndexOf(exporter);
                    var remainingPipelines = this.exporters.Skip(currentIndex + 1)
                        .Where(e => e.ResourceType == FabricUpgradeResourceTypes.DataPipeline);
                    
                    foreach (var remainingExporter in remainingPipelines)
                    {
                        cancelledPipelines++;
                        cancelledPipelineNames.Add(remainingExporter.Name);
                    }
                    
                    if (remainingPipelines.Any())
                    {
                        Console.WriteLine($"⚠  Export operation cancelled - {remainingPipelines.Count()} remaining pipeline(s) will not be processed");
                    }
                    break; // Stop processing immediately
                }
                catch (Exception ex)
                {
                    // Handle other failures - continue with others but track the failure
                    if (exporter.ResourceType == FabricUpgradeResourceTypes.DataPipeline)
                    {
                        failedPipelines++;
                        failedPipelineNames.Add(exporter.Name);
                        
                        this.Alerts.AddAlert(new FabricUpgradeAlert()
                        {
                            Severity = FabricUpgradeAlert.AlertSeverity.Permanent,
                            Details = $"Pipeline '{exporter.Name}' failed to export: {ex.Message}",
                            SourcePipelineName = exporter.Name
                        });

                        // Note: We continue processing other pipelines instead of throwing
                        Console.WriteLine($"✗ Pipeline '{exporter.Name}' failed to export: {ex.Message}");
                        Console.WriteLine($"⚠  Continuing with remaining pipelines...");

                        // Update progress counter to include failed pipelines
                        if (totalPipelines > 1)
                        {
                            Console.WriteLine($"Pipeline export progress: {exportedPipelines + failedPipelines}/{totalPipelines} pipelines completed");
                        }
                    }
                    else
                    {
                        // For non-pipeline resources, we might still want to fail fast
                        // since they're typically dependencies for pipelines
                        this.Alerts.AddPermanentError($"Failed to export {exporter.ResourceType} '{exporter.Name}': {ex.Message}");
                        throw new UpgradeFailureException("Export");
                    }
                }
            }

            // Show final completion status with proper handling of cancellation
            if (totalPipelines > 0)
            {
                int successfulPipelines = exportedPipelines;
                int totalSkipped = skippedPipelineNames.Count;

                if (operationCancelled)
                {
                    Console.WriteLine($"Pipeline export was cancelled:");
                    Console.WriteLine($"  ✓ {successfulPipelines} pipeline(s) exported successfully");
                    if (failedPipelines > 0)
                        Console.WriteLine($"  ✗ {failedPipelines} pipeline(s) failed to export");
                    if (totalSkipped > 0)
                        Console.WriteLine($"  ⏭  {totalSkipped} pipeline(s) skipped due to dependencies");
                    Console.WriteLine($"  ⚠  {cancelledPipelines} pipeline(s) cancelled");
                    
                    // Only show detailed pipeline lists in verbose mode
                    if (this.Verbose)
                    {
                        if (failedPipelineNames.Any())
                            Console.WriteLine($"  Failed pipelines: {string.Join(", ", failedPipelineNames)}");
                        if (skippedPipelineNames.Any())
                            Console.WriteLine($"  Skipped pipelines: {string.Join(", ", skippedPipelineNames)}");
                        if (cancelledPipelineNames.Any())
                            Console.WriteLine($"  Cancelled pipelines: {string.Join(", ", cancelledPipelineNames)}");
                    }
                }
                else if (failedPipelines > 0 || totalSkipped > 0)
                {
                    Console.WriteLine($"Pipeline export completed with issues:");
                    Console.WriteLine($"  ✓ {successfulPipelines} pipeline(s) exported successfully");
                    if (failedPipelines > 0)
                        Console.WriteLine($"  ✗ {failedPipelines} pipeline(s) failed to export");
                    if (totalSkipped > 0)
                        Console.WriteLine($"  ⏭  {totalSkipped} pipeline(s) skipped due to dependencies");
                    
                    // Only show detailed pipeline lists in verbose mode
                    if (this.Verbose)
                    {
                        if (failedPipelineNames.Any())
                            Console.WriteLine($"  Failed pipelines: {string.Join(", ", failedPipelineNames)}");
                        if (skippedPipelineNames.Any())
                            Console.WriteLine($"  Skipped pipelines: {string.Join(", ", skippedPipelineNames)}");
                    }
                }
                else
                {
                    Console.WriteLine($"✓ All {totalPipelines} pipeline(s) exported successfully to Fabric workspace");
                }
            }

            // If operation was cancelled, throw to ensure proper handling up the stack
            if (operationCancelled)
            {
                throw new OperationCanceledException("Pipeline export operation was cancelled by user request.", cancellationToken);
            }

            // If any pipelines failed, throw to ensure proper handling up the stack
            if (failedPipelines > 0)
            {
                throw new UpgradeFailureException("Export");
            }

            return this.BuildResultObject();
        }

        /// <summary>
        /// Check the Alerts; if any are worse than a Warning, then the Export has failed.
        /// </summary>
        /// <returns>True if the Export has failed; False otherwise.</returns>
        private bool AlertsIndicateFailure()
        {
            return this.Alerts.Any(f => f.Severity != FabricUpgradeAlert.AlertSeverity.Warning);
        }

        private JObject BuildResultObject()
        {
            JObject result = new JObject();
            if (this.exportResults != null && this.exportResults.Count > 0)
            {
                result[FabricUpgradeProgress.ExportedFabricResourcesKey] = this.exportResults;
            }
            return result;
        }

        /// <summary>
        /// Build a dependency map to track which pipelines depend on which other pipelines
        /// </summary>
        private Dictionary<string, List<string>> BuildPipelineDependencyMap()
        {
            var dependencyMap = new Dictionary<string, List<string>>();

            foreach (var exporter in this.exporters.Where(e => e.ResourceType == FabricUpgradeResourceTypes.DataPipeline))
            {
                var dependencies = new List<string>();

                // Look for ExecutePipeline/InvokePipeline activities in the export instruction
                if (exporter is PipelineExporter pipelineExporter)
                {
                    try
                    {
                        // Access the export instruction to find pipeline references
                        var exportInstructionField = typeof(PipelineExporter).GetField("exportInstruction", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (exportInstructionField?.GetValue(pipelineExporter) is PipelineExportInstruction instruction)
                        {
                            // Check for pipeline references in resolve steps
                            foreach (var resolve in instruction.Resolves)
                            {
                                if (resolve.Type == FabricUpgradeResolution.ResolutionType.AdfResourceNameToFabricResourceId &&
                                    resolve.Key.StartsWith($"{FabricUpgradeResourceTypes.DataPipeline}:"))
                                {
                                    // Extract the pipeline name from the key (format: "DataPipeline:PipelineName")
                                    string referencedPipeline = resolve.Key.Substring($"{FabricUpgradeResourceTypes.DataPipeline}:".Length);
                                    if (!string.IsNullOrEmpty(referencedPipeline) && !dependencies.Contains(referencedPipeline))
                                    {
                                        dependencies.Add(referencedPipeline);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // If we can't determine dependencies, err on the side of caution
                        // and don't add any dependencies (pipeline will still be processed)
                    }
                }

                dependencyMap[exporter.Name] = dependencies;
            }

            return dependencyMap;
        }

        /// <summary>
        /// Check if a pipeline should be skipped due to failed dependencies
        /// </summary>
        private bool ShouldSkipDueToDependency(string pipelineName, List<string> failedPipelineNames, Dictionary<string, List<string>> dependencyMap)
        {
            if (!dependencyMap.ContainsKey(pipelineName))
                return false;

            var dependencies = dependencyMap[pipelineName];
            return dependencies.Any(dep => failedPipelineNames.Contains(dep));
        }
    }
}

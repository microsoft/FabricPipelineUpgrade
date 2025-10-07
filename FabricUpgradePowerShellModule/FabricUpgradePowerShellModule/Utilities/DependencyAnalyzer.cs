// <copyright file="DependencyAnalyzer.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule.Utilities
{
    /// <summary>
    /// Analyzes dependencies between ADF resources to determine which resources are actually used.
    /// </summary>
    public static class DependencyAnalyzer
    {
        /// <summary>
        /// Analyze ADF resources to find which datasets and linked services are actually used by pipelines.
        /// </summary>
        /// <param name="pipelines">Dictionary of pipeline definitions.</param>
        /// <param name="datasets">Dictionary of dataset definitions.</param>
        /// <param name="usedDatasets">Set to populate with dataset names that are used.</param>
        /// <param name="usedLinkedServices">Set to populate with linked service names that are used.</param>
        private static void AnalyzeResourceDependencies(
            Dictionary<string, JObject> pipelines,
            Dictionary<string, JObject> datasets,
            HashSet<string> usedDatasets,
            HashSet<string> usedLinkedServices)
        {
            // First, analyze pipeline dependencies to find used datasets and direct linked service references
            AnalyzePipelineDependencies(pipelines, usedDatasets, usedLinkedServices);

            // Then, analyze dataset dependencies to find linked services used by datasets
            AnalyzeDatasetDependencies(datasets, usedDatasets, usedLinkedServices);
        }

        /// <summary>
        /// Analyze pipeline activities to find which datasets and linked services are referenced.
        /// </summary>
        /// <param name="pipelines">Dictionary of pipeline definitions.</param>
        /// <param name="usedDatasets">Set to populate with dataset names that are used.</param>
        /// <param name="usedLinkedServices">Set to populate with linked service names that are used.</param>
        private static void AnalyzePipelineDependencies(
            Dictionary<string, JObject> pipelines,
            HashSet<string> usedDatasets,
            HashSet<string> usedLinkedServices)
        {
            foreach (var pipelineEntry in pipelines)
            {
                JToken pipelineToken = pipelineEntry.Value;
                JArray activities = pipelineToken.SelectToken("properties.activities") as JArray;

                if (activities != null)
                {
                    foreach (JObject activity in activities)
                    {
                        AnalyzeActivityDependencies(activity, usedDatasets, usedLinkedServices);
                    }
                }
            }
        }

        /// <summary>
        /// Analyze a single activity to find dataset and linked service dependencies.
        /// </summary>
        /// <param name="activity">The activity to analyze.</param>
        /// <param name="usedDatasets">Set to populate with dataset names that are used.</param>
        /// <param name="usedLinkedServices">Set to populate with linked service names that are used.</param>
        private static void AnalyzeActivityDependencies(JObject activity, HashSet<string> usedDatasets, HashSet<string> usedLinkedServices)
        {
            // Check for dataset references in inputs and outputs
            var inputs = activity.SelectToken("inputs") as JArray;
            var outputs = activity.SelectToken("outputs") as JArray;

            if (inputs != null)
            {
                foreach (JObject input in inputs)
                {
                    string datasetName = input.SelectToken("referenceName")?.ToString();
                    if (!string.IsNullOrEmpty(datasetName))
                    {
                        usedDatasets.Add(datasetName);
                    }
                }
            }

            if (outputs != null)
            {
                foreach (JObject output in outputs)
                {
                    string datasetName = output.SelectToken("referenceName")?.ToString();
                    if (!string.IsNullOrEmpty(datasetName))
                    {
                        usedDatasets.Add(datasetName);
                    }
                }
            }

            // Check for dataset references in activity type properties
            string activityType = activity.SelectToken("type")?.ToString();

            var typeProperties = activity.SelectToken("typeProperties");
            if (typeProperties != null)
            {
                // Check for datasetId references (used in some converted activities)
                string datasetId = typeProperties.SelectToken("datasetId")?.ToString();
                if (!string.IsNullOrEmpty(datasetId))
                {
                    usedDatasets.Add(datasetId);
                }

                // Lookup Activity: dataset is referenced at typeProperties.dataset.referenceName in ADF JSON
                if (activityType == "Lookup")
                {
                    string lookupDataset = typeProperties.SelectToken("dataset.referenceName")?.ToString();
                    if (!string.IsNullOrEmpty(lookupDataset))
                    {
                        usedDatasets.Add(lookupDataset);
                    }
                }

                // Generic scan of dataset object if present
                string genericDatasetRef = typeProperties.SelectToken("dataset.referenceName")?.ToString();
                if (!string.IsNullOrEmpty(genericDatasetRef))
                {
                    usedDatasets.Add(genericDatasetRef);
                }

                // Check for dataset references in source/sink
                var source = typeProperties.SelectToken("source");
                var sink = typeProperties.SelectToken("sink");

                CheckForDatasetReferences(source, usedDatasets);
                CheckForDatasetReferences(sink, usedDatasets);
            }

            // Activity-specific linked service references are handled here
            // For example, AzureFunctionActivity and AzureDataExplorerCommand
            string linkedServiceName = activity.SelectToken("linkedServiceName.referenceName")?.ToString();
            if (!string.IsNullOrEmpty(linkedServiceName))
            {
                usedLinkedServices.Add(linkedServiceName);
            }

            // Check for direct linked service references (e.g., WebActivity, WebHook)
            linkedServiceName = activity.SelectToken("typeProperties.linkedServiceName.referenceName")?.ToString();
            if (!string.IsNullOrEmpty(linkedServiceName))
            {
                usedLinkedServices.Add(linkedServiceName);
            }

            // Check for script linked service references in the activity (e.g., ScopeActivity)
            linkedServiceName = activity.SelectToken("typeProperties.scriptLinkedService.referenceName")?.ToString();
            if (!string.IsNullOrEmpty(linkedServiceName))
            {
                usedLinkedServices.Add(linkedServiceName);
            }

        }

        /// <summary>
        /// Check a JToken for any dataset references.
        /// </summary>
        /// <param name="token">The token to check.</param>
        /// <param name="usedDatasets">Set to populate with dataset names.</param>
        private static void CheckForDatasetReferences(JToken token, HashSet<string> usedDatasets)
        {
            if (token == null) return;

            // Look for any properties that might contain dataset references
            foreach (var property in token.Children<JProperty>())
            {
                if (property.Name.ToLower().Contains("dataset") && property.Value?.Type == JTokenType.String)
                {
                    string datasetName = property.Value.ToString();
                    if (!string.IsNullOrEmpty(datasetName))
                    {
                        usedDatasets.Add(datasetName);
                    }
                }
            }
        }

        /// <summary>
        /// Analyze datasets to find their linked service dependencies.
        /// </summary>
        /// <param name="datasets">Dictionary of dataset definitions.</param>
        /// <param name="usedDatasets">Set of dataset names that are used.</param>
        /// <param name="usedLinkedServices">Set to populate with linked service names that are used.</param>
        private static void AnalyzeDatasetDependencies(
            Dictionary<string, JObject> datasets,
            HashSet<string> usedDatasets,
            HashSet<string> usedLinkedServices)
        {
            foreach (var datasetEntry in datasets)
            {
                if (usedDatasets.Contains(datasetEntry.Key))
                {
                    string linkedServiceName = datasetEntry.Value.SelectToken("properties.linkedServiceName.referenceName")?.ToString();
                    if (!string.IsNullOrEmpty(linkedServiceName))
                    {
                        usedLinkedServices.Add(linkedServiceName);
                    }
                }
            }
        }

        /// <summary>
        /// Filter datasets and linked services to include only those used by pipelines.
        /// </summary>
        /// <param name="pipelines">Dictionary of pipeline definitions.</param>
        /// <param name="allDatasets">Dictionary of all available dataset definitions.</param>
        /// <param name="allLinkedServices">Dictionary of all available linked service definitions.</param>
        /// <param name="alerts">Alert collector for logging messages.</param>
        /// <param name="verbose">Whether to output informational messages about filtering process.</param>
        /// <returns>A tuple containing filtered datasets and linked services.</returns>
        public static (Dictionary<string, JObject> FilteredDatasets, Dictionary<string, JObject> FilteredLinkedServices) 
            FilterUnusedResources(
                Dictionary<string, JObject> pipelines,
                Dictionary<string, JObject> allDatasets,
                Dictionary<string, JObject> allLinkedServices,
                AlertCollector alerts,
                bool verbose = false)
        {
            var usedDatasets = new HashSet<string>();
            var usedLinkedServices = new HashSet<string>();

            // Analyze dependencies to find which resources are actually used
            AnalyzeResourceDependencies(pipelines, allDatasets, usedDatasets, usedLinkedServices);

            // Count original resources for logging
            int originalDatasetCount = allDatasets.Count;
            int originalLinkedServiceCount = allLinkedServices.Count;

            // Filter datasets - keep only those that are used
            var filteredDatasets = new Dictionary<string, JObject>();
            foreach (var datasetEntry in allDatasets)
            {
                if (usedDatasets.Contains(datasetEntry.Key))
                {
                    filteredDatasets[datasetEntry.Key] = datasetEntry.Value;
                }
                else if (verbose)
                {
                    Console.WriteLine($"Excluding unused dataset '{datasetEntry.Key}' from import");
                }
            }

            // Filter linked services - keep only used ones
            var filteredLinkedServices = new Dictionary<string, JObject>();
            foreach (var linkedServiceEntry in allLinkedServices)
            {
                bool isUsed = usedLinkedServices.Contains(linkedServiceEntry.Key);

                if (isUsed)
                {
                    // Only include used linked services
                    filteredLinkedServices[linkedServiceEntry.Key] = linkedServiceEntry.Value;
                }
                else if (verbose)
                {
                    string linkedServiceType = linkedServiceEntry.Value.SelectToken("properties.type")?.ToString();
                    Console.WriteLine($"Excluding unused linked service '{linkedServiceEntry.Key}' of type '{linkedServiceType}' from import");
                }
            }

            // Log summary in verbose mode
            if (verbose)
            {
                Console.WriteLine($"Dependency analysis completed:");
                Console.WriteLine($"  Datasets: {originalDatasetCount} total → {filteredDatasets.Count} used ({originalDatasetCount - filteredDatasets.Count} excluded)");
                Console.WriteLine($"  Linked Services: {originalLinkedServiceCount} total → {filteredLinkedServices.Count} used ({originalLinkedServiceCount - filteredLinkedServices.Count} excluded)");
            }

            return (filteredDatasets, filteredLinkedServices);
        }
    }
}
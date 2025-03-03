// File: PipelineDependencyUpdater.cs
// Location: FabricUpgradePowerShellModule/Utilities

using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace FabricUpgradePowerShellModule.Utilities
{
    /// <summary>
    /// Utility class for updating dependency references in a Fabric pipeline JSON.
    /// </summary>
    public static class PipelineDependencyUpdater
    {
        /// <summary>
        /// Updates the "dependsOn" activity references in the given pipeline JSON based on a mapping
        /// from original activity names to new Fabric activity names.
        /// </summary>
        /// <param name="pipeline">The pipeline JSON as a JObject.</param>
        /// <param name="nameMapping">A dictionary mapping original ADF activity names to Fabric activity names.</param>
        public static void UpdateActivityDependencies(JObject pipeline, Dictionary<string, string> nameMapping)
        {
            // Get the array of activities from the pipeline.
            var activities = pipeline.SelectToken("properties.activities") as JArray;
            if (activities == null)
            {
                return;
            }

            foreach (JObject activity in activities)
            {
                var dependsOn = activity["dependsOn"] as JArray;
                if (dependsOn != null)
                {
                    foreach (JObject dependency in dependsOn)
                    {
                        // Read the original dependency activity name.
                        string originalActivityName = dependency.Value<string>("activity");
                        if (!string.IsNullOrEmpty(originalActivityName) && nameMapping.TryGetValue(originalActivityName, out string newActivityName))
                        {
                            // Update the dependency to use the new Fabric activity name.
                            dependency["activity"] = newActivityName;
                        }
                    }
                }
            }
        }
    }
}

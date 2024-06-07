using FabricUpgradeCmdlet.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using static FabricUpgradeCmdlet.Models.FabricUpgradeAlert;

namespace FabricUpgradeCmdlet.Utilities
{
    public class AlertCollector : IEnumerable<FabricUpgradeAlert>
    {
        private readonly List<FabricUpgradeAlert> alerts = new List<FabricUpgradeAlert>();

        public int Count { get => this.alerts.Count; }

        public IEnumerator<FabricUpgradeAlert> GetEnumerator()
        {
            return this.alerts.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.alerts.GetEnumerator();
        }

        public AlertCollector AddAlert(FabricUpgradeAlert alert)
        {
            this.alerts.Add(alert);
            return this;
        }

        public AlertCollector AddPermanentError(string details)
        {
            return this.AddAlert(FailureSeverity.Permanent, details);
        }

        public AlertCollector AddWarning(string details)
        {
            return this.AddAlert(FailureSeverity.Warning, details);
        }

        public AlertCollector AddUnsupportedResourceAlert(string details)
        {
            return this.AddAlert(FailureSeverity.UnsupportedResource, details);
        }

        /// <summary>
        /// Create an Alert requesting the user to add a Connection to the Resolutions.
        /// </summary>
        /// <param name="resolutionType">What kind of Resolution to add.</param>
        /// <param name="resolutionKey">The key of the desired Resolution.</param>
        /// <returns>this, for chaining.</returns>
        public AlertCollector AlertMissingConnectionResolution(
            FabricUpgradeResolution.ResolutionType resolutionType,
            string resolutionKey,
            FabricUpgradeConnectionHint connectionHints)
        {
            string requestString = BuildResolutionString(resolutionType, resolutionKey, "<connection GUID>");
            return this.AddAlert(
                FailureSeverity.RequiresUserAction,
                $"Please add {requestString} to resolutions",
                connectionHints);
        }

        /// <summary>
        /// Create an Alert for an invalid Resolution.
        /// </summary>
        /// <param name="alerts">Add the new alert to this list.</param>
        /// <param name="resolutionType">What kind of Resolution to add.</param>
        /// <param name="resolutionKey">The key of the desired Resolution.</param>
        /// <returns>this, for chaining.</returns>
        public AlertCollector AlertConnectionIdIsNotGuid(
            FabricUpgradeResolution.ResolutionType resolutionType,
            string resolutionKey)
        {
            string requestString = BuildResolutionString(resolutionType, resolutionKey, "<connection GUID>");
            return this.AddAlert(
                FailureSeverity.RequiresUserAction,
                $"Please ensure that the resolution {requestString} has a GUID value");
        }

        /// <summary>
        /// Some common code to build a string example of a Resolution.
        /// </summary>
        /// <param name="resolutionType">What kind of Resolution to add.</param>
        /// <param name="resolutionKey">The key of the desired Resolution.</param>
        /// <param name="resolutionValue">The example value of the Resolution.</param>
        /// <returns>An example string showing a Resolution.</returns>
        private static string BuildResolutionString(
            FabricUpgradeResolution.ResolutionType resolutionType,
            string resolutionKey,
            string resolutionValue)
        {
            FabricUpgradeResolution resolution = new FabricUpgradeResolution()
            {
                Type = resolutionType,
                Key = resolutionKey,
                Value = resolutionValue,
            };

            JToken request = UpgradeSerialization.ToJToken(resolution);
            return request.ToString(Formatting.None)
                .Replace("{\"", "{ \"")
                .Replace("\"}", "\" }")
                .Replace("\",\"", "\", \"")
                .Replace("\":\"", "\": \"");
        }

        /// <summary>
        /// Build a FabricUpgradeAlert and add it to the end of the running alerts list.
        /// </summary>
        /// <param name="severity">The severity of the new alert.</param>
        /// <param name="details">The details of the new alert.</param>
        /// <returns>this, for chaining.</returns>
        private AlertCollector AddAlert(
            FailureSeverity severity,
            string details,
            FabricUpgradeConnectionHint connectionHints = null)
        {
            this.alerts.Add(new FabricUpgradeAlert()
            {
                Severity = severity,
                Details = details,
                ConnectionHints = connectionHints,
            });

            return this;
        }
    }
}

// <copyright file="CopyActivityUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    /// <summary>
    /// This class Upgrades an ADF Copy Activity to a Fabric Copy Activity.
    /// </summary>
    public class CopyActivityUpgrader : ActivityUpgrader
    {
        private const string inputDatasetNamePath = "inputs[0].referenceName";
        private const string sourceDatasetSettingsPath = "typeProperties.source.datasetSettings";

        private const string outputDatasetNamePath = "outputs[0].referenceName";
        private const string sinkDatasetSettingsPath = "typeProperties.sink.datasetSettings";

        private const string adfStagingSettingsPath = "typeProperties.stagingSettings";
        private const string adfStagingSettingsLinkedServiceNamePath = adfStagingSettingsPath + ".linkedServiceName.referenceName";

        private const string fabricStagingSettingsPath = "typeProperties.stagingSettings";
        private const string fabricStagingSettingsConnectionIdPath = fabricStagingSettingsPath + ".externalReferences.connection";

        private const string adfLogSettingsPath = "typeProperties.logSettings";
        private const string adfLogSettingsLinkedServiceNamePath = adfLogSettingsPath + ".logLocationSettings.linkedServiceName.referenceName";

        private const string fabricLogSettingsPath = "typeProperties.logSettings";
        private const string fabricLogSettingsConnectionIdPath = fabricLogSettingsPath + ".logLocationSettings.externalReferences.connection";

        private readonly List<string> requiredAdfProperties =
        [
            inputDatasetNamePath,
            outputDatasetNamePath
        ];

        private readonly Dictionary<string, string> datasetSettingsMap = new Dictionary<string, string>()
        {
            { inputDatasetNamePath, sourceDatasetSettingsPath },
            { outputDatasetNamePath, sinkDatasetSettingsPath },
        };

        private Upgrader inputDatasetUpgrader;
        private Upgrader outputDatasetUpgrader;

        public CopyActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.Copy, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);
        }

        /// <inheritdoc/>
        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreLink(allUpgraders, alerts);

            // Find the Upgraders for the ADF Datasets, and mark them as prerequisites.
            // We use that to sort the Upgraders, so that we Export them in the correct order.

            this.inputDatasetUpgrader = this.FindOtherUpgrader(
                allUpgraders,
                FabricUpgradeResourceTypes.Dataset,
                inputDatasetNamePath,
                alerts);

            if (this.inputDatasetUpgrader != null)
            {
                this.DependsOn.Add(this.inputDatasetUpgrader);
            }

            this.outputDatasetUpgrader = this.FindOtherUpgrader(
                allUpgraders,
                FabricUpgradeResourceTypes.Dataset,
                outputDatasetNamePath,
                alerts);

            if (this.outputDatasetUpgrader != null)
            {
                this.DependsOn.Add(this.outputDatasetUpgrader);
            }

            Upgrader stagingLinkedServiceUpgrader = this.FindOtherUpgrader(
                allUpgraders,
                FabricUpgradeResourceTypes.LinkedService,
                adfStagingSettingsLinkedServiceNamePath,
                alerts);

            if (stagingLinkedServiceUpgrader != null)
            {
                this.DependsOn.Add(stagingLinkedServiceUpgrader);
            }

            Upgrader logSettingsLinkedServiceUpgrader = this.FindOtherUpgrader(
                allUpgraders,
                FabricUpgradeResourceTypes.LinkedService,
                adfLogSettingsLinkedServiceNamePath,
                alerts);

            if (logSettingsLinkedServiceUpgrader != null)
            {
                this.DependsOn.Add(logSettingsLinkedServiceUpgrader);
            }
        }

        /// <inheritdoc/>
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                return this.BuildExportLinksSymbol(parameters, alerts);
            }

            if (symbolName == Symbol.CommonNames.Activity)
            {
                return this.BuildActivitySymbol(parameters, alerts);
            }

            return base.ResolveExportedSymbol(symbolName, parameters, alerts);
        }

        /// <inheritdoc/>
        protected override Symbol BuildExportLinksSymbol(
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            List<FabricExportLink> links = new List<FabricExportLink>();

            this.AddDatasetSettingsConnectionIdLink(links, this.inputDatasetUpgrader, sourceDatasetSettingsPath, alerts);
            this.AddDatasetSettingsConnectionIdLink(links, this.outputDatasetUpgrader, sinkDatasetSettingsPath, alerts);

            this.AddStagingSettingsLink(links);
            this.AddLogSettingsLink(links);

            return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(links)));
        }

        /// <inheritdoc/>
        protected override Symbol BuildActivitySymbol(
            Dictionary<string, JToken> parameters,
            AlertCollector alerts)
        {
            Symbol activitySymbol = base.ResolveExportedSymbol(Symbol.CommonNames.Activity, parameters, alerts);

            if (activitySymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            JObject fabricActivityObject = (JObject)activitySymbol.Value;

            PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);

            copier.Copy("description");
            copier.Copy("policy");
            copier.Copy("typeProperties.parallelCopies", copyIfNull: false);
            copier.Copy("typeProperties.dataIntegrationUnits", copyIfNull: false);

            foreach (string dataset in new List<string> { "source", "sink" })
            {
                copier.Copy($"typeProperties.{dataset}.type");
                copier.Copy($"typeProperties.{dataset}.storeSettings", copyIfNull: false);
                copier.Copy($"typeProperties.{dataset}.formatSettings", copyIfNull: false);
            }

            this.AddDatasetSettings(copier, "inputs", this.inputDatasetUpgrader, sourceDatasetSettingsPath, alerts);
            this.AddDatasetSettings(copier, "outputs", this.outputDatasetUpgrader, sinkDatasetSettingsPath, alerts);

            this.AddStagingSettings(copier, alerts);
            this.AddLogSettings(copier, alerts);

            copier.Copy("typeProperties.translator", copyIfNull: false);

            return Symbol.ReadySymbol(fabricActivityObject);
        }

        private string GetStagingSettingsLinkedServiceName()
        {
            return this.AdfResourceToken.SelectToken(adfStagingSettingsLinkedServiceNamePath)?.ToString();
        }

        private string GetLogSettingsLinkedServiceName()
        {
            return this.AdfResourceToken.SelectToken(adfLogSettingsLinkedServiceNamePath)?.ToString();
        }

        /// <summary>
        /// Ask a Dataset Upgrader to build an ExportLink to its LinkedService.
        /// </summary>
        /// <remarks>
        /// Since the Dataset is not a Fabric Resource, the Dataset's links become this Activity's links.
        /// And, of course, this Activity's links will become its Pipeline's links.
        /// </remarks>
        /// <param name="links">Add the Dataset's ExportLinks to this list.</param>
        /// <param name="datasetUpgrader">The Upgrader to query.</param>
        /// <param name="targetDatasetSettings">The JSON path to the dataset.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void AddDatasetSettingsConnectionIdLink(
            List<FabricExportLink> links,
            Upgrader datasetUpgrader,
            string targetDatasetSettings,
            AlertCollector alerts)
        {
            Symbol datasetLinksSymbol = datasetUpgrader.ResolveExportedSymbol(Symbol.CommonNames.ExportLinks, alerts);
            if (datasetLinksSymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }

            if (datasetLinksSymbol.Value != null)
            {
                foreach (JToken requiredLink in (JArray)datasetLinksSymbol.Value)
                {
                    FabricExportLink link = FabricExportLink.FromJToken(requiredLink);
                    link.TargetPath = $"{targetDatasetSettings}.{link.TargetPath}";
                    links.Add(link);
                }
            }
        }

        private void AddStagingSettingsLink(
            List<FabricExportLink> links)
        {
            string linkedServiceName = this.GetStagingSettingsLinkedServiceName();
            if (linkedServiceName == null)
            {
                return;
            }

            FabricExportLink link = new FabricExportLink(
                $"{FabricUpgradeResourceTypes.Connection}:{linkedServiceName}",
                fabricStagingSettingsConnectionIdPath);

            links.Add(link);
        }

        private void AddLogSettingsLink(
            List<FabricExportLink> links)
        {
            string linkedServiceName = this.GetLogSettingsLinkedServiceName();
            if (linkedServiceName == null)
            {
                return;
            }

            FabricExportLink link = new FabricExportLink(
                $"{FabricUpgradeResourceTypes.Connection}:{linkedServiceName}",
                fabricLogSettingsConnectionIdPath);

            links.Add(link);
        }

        /// <summary>
        /// Query a Dataset Upgrader for its datasetSettings Symbol, and insert it into this Activity.
        /// </summary>
        /// <param name="copier">The PropertyCopier that will insert the datasetSettings.</param>
        /// <param name="datasetPath">Where in the AdfResourceToken to find the Dataset and its parameters.</param>
        /// <param name="datasetUpgrader">The Upgrader to query.</param>
        /// <param name="datasetSettingsPath">Where in the Fabric Resource to insert the datasetSettings.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void AddDatasetSettings(
            PropertyCopier copier,
            string datasetPath,
            Upgrader datasetUpgrader,
            string datasetSettingsPath,
            AlertCollector alerts)
        {
            string q = this.AdfResourceToken.ToString(Newtonsoft.Json.Formatting.Indented);

            JObject parametersObject = (JObject)this.AdfResourceToken.SelectToken($"{datasetPath}[0].parameters") ?? new JObject();
            Dictionary<string, JToken> parameters = parametersObject.ToObject<Dictionary<string, JToken>>();

            Symbol datasetSettingsSymbol = datasetUpgrader.ResolveExportedSymbol(
                Symbol.CommonNames.DatasetSettings,
                parameters,
                alerts);

            if (datasetSettingsSymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }
            copier.Set(datasetSettingsPath, datasetSettingsSymbol.Value);
        }

        /// <summary>
        /// If this Copy Activity includes Staging, insert the Staging properties into the Fabric Copy Activity.
        /// </summary>
        /// <remarks>
        /// The ADF Staging settings point directly at a LinkedService: there is not an intermediate Dataset.
        /// </remarks>
        /// <param name="copier">The PropertyCopier that will insert the Staging properties into the Fabric JSON.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void AddStagingSettings(
            PropertyCopier copier,
            AlertCollector alerts)
        {
            copier.Copy("typeProperties.enableStaging", copyIfNull: false);

            JToken stagingSettings = this.AdfResourceToken.SelectToken(adfStagingSettingsPath)?.DeepClone();
            if (stagingSettings != null)
            {
                // Remove the "linkedServiceName" property from the clone of the Staging settings...
                JToken toRemove = stagingSettings.SelectToken("$.linkedServiceName");
                toRemove?.Parent?.Remove();

                // ... insert the externalReference property...
                JObject externalReferences = new JObject();
                externalReferences["connection"] = Guid.Empty.ToString();

                stagingSettings["externalReferences"] = externalReferences;

                // ... and copy the result into the Fabric JSON.
                copier.Set(fabricStagingSettingsPath, stagingSettings);
            }
        }

        /// <summary>
        /// If this Copy Activity includes Logging, insert the Logging properties into the Fabric Copy Activity.
        /// </summary>
        /// <remarks>
        /// The ADF Logging settings point directly at a LinkedService: there is not an intermediate Dataset.
        /// </remarks>
        /// <param name="copier">The PropertyCopier that will insert the Logging properties into the Fabric JSON.</param>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        private void AddLogSettings(
            PropertyCopier copier,
            AlertCollector alerts)
        {
            JToken logSettings = this.AdfResourceToken.SelectToken(adfLogSettingsPath)?.DeepClone();
            if (logSettings != null)
            {
                JToken locationSettings = logSettings.SelectToken("logLocationSettings");

                // Remove the "linkedServiceName" property from the clone of the Logging settings...
                JToken toRemove = locationSettings.SelectToken("linkedServiceName");
                toRemove?.Parent?.Remove();

                // ... and insert the externalReference property.
                JObject externalReferences = new JObject();
                externalReferences["connection"] = Guid.Empty.ToString();

                locationSettings["externalReferences"] = externalReferences;

                // ... and copy the result into the Fabric JSON.
                copier.Set(fabricLogSettingsPath, logSettings);
            }
        }
    }
}

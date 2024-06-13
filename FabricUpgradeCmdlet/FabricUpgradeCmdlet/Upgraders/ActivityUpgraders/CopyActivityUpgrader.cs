using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
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

        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);
        }

        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreLink(allUpgraders, alerts);

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

        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportLinks)
            {
                List<FabricExportLink> links = new List<FabricExportLink>();

                this.AddDatasetSettingsConnectionIdLink(links, this.inputDatasetUpgrader, sourceDatasetSettingsPath, alerts);
                this.AddDatasetSettingsConnectionIdLink(links, this.outputDatasetUpgrader, sinkDatasetSettingsPath, alerts);

                this.AddStagingSettingsLink(links);
                this.AddLogSettingsLink(links);

                return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(links)));
            }

            if (symbolName == Symbol.CommonNames.Activity)
            {
                Symbol activitySymbol = base.ResolveExportedSymbol(Symbol.CommonNames.Activity, alerts);

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
                    copier.Copy($"typeProperties.{dataset}.storeSettings");
                    copier.Copy($"typeProperties.{dataset}.formatSettings");
                }

                this.AddDatasetSettings(copier, this.inputDatasetUpgrader, sourceDatasetSettingsPath, alerts);
                this.AddDatasetSettings(copier, this.outputDatasetUpgrader, sinkDatasetSettingsPath, alerts);

                this.AddStagingSettings(copier, alerts);
                this.AddLogSettings(copier, alerts);

                copier.Copy("typeProperties.translator", copyIfNull: false);

                return Symbol.ReadySymbol(fabricActivityObject);
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
        }

        private string GetStagingSettingsLinkedServiceName()
        {
            return this.AdfResourceToken.SelectToken(adfStagingSettingsLinkedServiceNamePath)?.ToString();
        }

        private string GetLogSettingsLinkedServiceName()
        {
            return this.AdfResourceToken.SelectToken(adfLogSettingsLinkedServiceNamePath)?.ToString();
        }

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


        private void AddDatasetSettings(
            PropertyCopier copier,
            Upgrader datasetUpgrader,
            string datasetSettingsPath,
            AlertCollector alerts)
        {
            Symbol datasetSettingsSymbol = datasetUpgrader.ResolveExportedSymbol(Symbol.CommonNames.DatasetSettings, alerts);
            if (datasetSettingsSymbol.State != Symbol.SymbolState.Ready)
            {
                // TODO!
            }
            copier.Set(datasetSettingsPath, datasetSettingsSymbol.Value);
        }

        private void AddStagingSettings(
            PropertyCopier copier,
            AlertCollector alerts)
        {
            copier.Copy("typeProperties.enableStaging", copyIfNull: false);

            JToken stagingSettings = this.AdfResourceToken.SelectToken(adfStagingSettingsPath)?.DeepClone();
            if (stagingSettings != null)
            {
                JToken toRemove = stagingSettings.SelectToken("$.linkedServiceName");
                toRemove?.Parent?.Remove();

                JObject externalReferences = new JObject();
                stagingSettings["externalReferences"] = externalReferences;
                externalReferences["connection"] = Guid.Empty.ToString();

                copier.Set(fabricStagingSettingsPath, stagingSettings);
            }
        }

        private void AddLogSettings(
            PropertyCopier copier,
            AlertCollector alerts)
        {
            JToken logSettings = this.AdfResourceToken.SelectToken(adfLogSettingsPath)?.DeepClone();
            if (logSettings != null)
            {
                JToken locationSettings = logSettings.SelectToken("logLocationSettings");
                JToken toRemove = locationSettings.SelectToken("linkedServiceName");
                toRemove?.Parent?.Remove();

                JObject externalReferences = new JObject();
                locationSettings["externalReferences"] = externalReferences;
                externalReferences["connection"] = Guid.Empty.ToString();

                copier.Set(fabricLogSettingsPath, logSettings);
            }
        }
    }
}

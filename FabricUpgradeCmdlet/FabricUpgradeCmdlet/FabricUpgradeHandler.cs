using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet
{
    public class FabricUpgradeHandler
    {
        private AlertCollector alerts = new AlertCollector();

        public FabricUpgradeHandler() { }

        public FabricUpgradeProgress ImportAdfSupportFile(
            string fileName)
        {
            AdfSupportFileUpgradePackageCollector collector = new AdfSupportFileUpgradePackageCollector();
            byte[] supportFileData = null;
            try
            {
                supportFileData = File.ReadAllBytes(fileName);
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
                        Severity = FabricUpgradeAlert.FailureSeverity.Permanent,
                        Details = $"Failed to load Support File '{fileName}'.",
                    });
            }

            try
            {
                UpgradeUnzipper.Unzip(supportFileData, collector);
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
                        Severity = FabricUpgradeAlert.FailureSeverity.Permanent,
                        Details = "Failed to unzip Upgrade Package.",
                    });
            }

            return new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                Result = collector.Build(),
            };

        }

        public FabricUpgradeProgress ConvertToFabricPipeline(
            string toConvert,
            string resolutionsFilename)
        {
            FabricUpgradeProgress.FabricUpgradeState previousState = this.CheckProgress(toConvert);
            if (previousState != FabricUpgradeProgress.FabricUpgradeState.Succeeded)
            {
                return new FabricUpgradeProgress()
                {
                    State = previousState,
                    Alerts = this.alerts.ToList(),
                };
            }

            FabricUpgradeProgress previousResponse = FabricUpgradeProgress.FromString(toConvert);

            AdfUpgradePackage adfUpgradePackage = AdfUpgradePackage.FromJToken(previousResponse.Result);

            if (adfUpgradePackage.Type == AdfUpgradePackage.UpgradePackageType.AdfSupportFile)
            {
                AdfSupportFilesUpgradeMachine machine = new AdfSupportFilesUpgradeMachine(
                    previousResponse.Result,
                    new List<FabricUpgradeResolution>(),
                    this.alerts);

                return machine.Upgrade();
            }

            /*
            return @"{
                ""name"": ""WaitPipeline"",
                ""properties"": {
                    ""activities"": [
                        {
                            ""name"": ""Wait10Seconds"",
                            ""type"": ""Wait"",
                            ""description"": ""This activity waits 10 seconds"",
                            ""dependsOn"": [],
                            ""typeProperties"": {
                                ""waitTimeInSeconds"": 10
                            }                            
                         }
                    ]
                }
            }";*/

            return new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Failed,
            }
            .WithAlert(new FabricUpgradeAlert()
            {
                Severity = FabricUpgradeAlert.FailureSeverity.Permanent,
                Details = $"FabricUpgrade does not support package type '{adfUpgradePackage.Type}.",
            });
        }

        public async Task<FabricUpgradeProgress> ExportFabricPipelineAsync(
            string toExport,
            string cluster,
            string workspace,
            string fabricToken)
        {
            FabricUpgradeProgress.FabricUpgradeState progressState = this.CheckProgress(toExport);
            if (progressState != FabricUpgradeProgress.FabricUpgradeState.Succeeded)
            {
                return new FabricUpgradeProgress()
                {
                    State = progressState,
                    Alerts = this.alerts.ToList(),
                };
            }

            FabricUpgradeProgress progress = FabricUpgradeProgress.FromString(toExport);

            Dictionary<string, JObject> toUpload = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(progress.Result.ToString());

            JObject results = new JObject();

            foreach (var uploadable in toUpload)
            {
                // TODO: The previous response may, one day, include connections.
                string uploadResult = await new PublicApiClient().UploadPipelineAsync(
                                uploadable.Value,
                                cluster,
                                workspace,
                                fabricToken);

                results[uploadable.Key] = uploadResult;
            }
            return new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Succeeded,
                Alerts = this.alerts.ToList(),
                Result = results,
            };
        }

        private FabricUpgradeProgress.FabricUpgradeState CheckProgress(
            string previousResponse)
        {
            try
            {
                FabricUpgradeProgress previousResponseObject = FabricUpgradeProgress.FromString(previousResponse);

                foreach (FabricUpgradeAlert alert in previousResponseObject.Alerts)
                {
                    this.alerts.AddAlert(alert);
                }

                return previousResponseObject.State;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                this.alerts.AddPermanentError("Input is not a valid JSON string.");
                return FabricUpgradeProgress.FabricUpgradeState.Failed;
            }
        }
    }
}

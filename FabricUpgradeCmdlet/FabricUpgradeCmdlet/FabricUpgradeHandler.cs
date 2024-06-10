using FabricUpgradeCmdlet.Exporters;
using FabricUpgradeCmdlet.ExportMachines;
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
            string progressString,
            string fileName)
        {
            FabricUpgradeProgress progress = FabricUpgradeProgress.FromString(progressString);

            AdfSupportFileUpgradePackageCollector collector = new AdfSupportFileUpgradePackageCollector();
            byte[] supportFileData;
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
            string progressString,
            string resolutionsFilename)
        {
            FabricUpgradeProgress.FabricUpgradeState previousState = this.CheckProgress(progressString);
            if (previousState != FabricUpgradeProgress.FabricUpgradeState.Succeeded)
            {
                return new FabricUpgradeProgress()
                {
                    State = previousState,
                    Alerts = this.alerts.ToList(),
                };
            }

            FabricUpgradeProgress progress = FabricUpgradeProgress.FromString(progressString);

            UpgradePackage upgradePackage = UpgradePackage.FromJToken(progress.Result);

            if (upgradePackage.Type == AdfUpgradePackage.UpgradePackageType.AdfSupportFile)
            {
                AdfSupportFileUpgradeMachine machine = new AdfSupportFileUpgradeMachine(
                    progress.Result,
                    new List<FabricUpgradeResolution>(),
                    this.alerts);

                return machine.Upgrade();
            }

            return new FabricUpgradeProgress()
            {
                State = FabricUpgradeProgress.FabricUpgradeState.Failed,
            }
            .WithAlert(new FabricUpgradeAlert()
            {
                Severity = FabricUpgradeAlert.FailureSeverity.Permanent,
                Details = $"FabricUpgrade does not support package type '{upgradePackage.Type}'.",
            });
        }

        public async Task<FabricUpgradeProgress> ExportFabricPipelineAsync(
            string progressString,
            string cluster,
            string workspaceId,
            string fabricToken)
        {
            FabricUpgradeProgress.FabricUpgradeState progressState = this.CheckProgress(progressString);
            if (progressState != FabricUpgradeProgress.FabricUpgradeState.Succeeded)
            {
                return new FabricUpgradeProgress()
                {
                    State = progressState,
                    Alerts = this.alerts.ToList(),
                };
            }

            FabricUpgradeProgress progress = FabricUpgradeProgress.FromString(progressString);


            FabricExportMachine machine = new FabricExportMachine(
                    progress.Result,
                    cluster,
                    workspaceId,
                    fabricToken,
                    new List<FabricUpgradeResolution>(),
                    this.alerts);

            return await machine.ExportAsync().ConfigureAwait(false);
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

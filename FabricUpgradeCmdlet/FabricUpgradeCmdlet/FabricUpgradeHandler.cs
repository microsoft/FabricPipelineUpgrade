// <copyright file="FabricUpgradeHandler.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.ExportMachines;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;

namespace FabricUpgradeCmdlet
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

        public FabricUpgradeHandler() { }

        /// <summary>
        /// Import an ADF Support File.
        /// </summary>
        /// <remarks>
        /// ADF Studio can export a "Support File" that contains a Pipeline and all of the other 
        /// ADF resources upon which that Pipeline depends (including other Pipelines!).
        /// </remarks>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <param name="fileName">The name of the ADF support file to import.</param>
        /// <returns>A FabricUpgradeProgress that contains the unzipped contents of the ADF Support File.</returns>
        public FabricUpgradeProgress ImportAdfSupportFile(
            string progressString,
            string fileName)
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
                Alerts = progress.Alerts,
                Resolutions = progress.Resolutions,
                Result = collector.Build(),
            };

        }

        /// <summary>
        /// Accept a Progress that includes the result of Import-AdfSupportFile and
        /// upgrade it to a set of Fabric Resource descriptions that can be exported
        /// by Export-FabricPipeline.
        /// </summary>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <returns>A FabricUpgradeProgress that contains 'instructions' to Export-FabricPipeline.</returns>
        public FabricUpgradeProgress ConvertToFabricPipeline(
            string progressString)
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

            if (upgradePackage.Type == UpgradePackage.UpgradePackageType.AdfSupportFile)
            {
                AdfSupportFileUpgradeMachine machine = new AdfSupportFileUpgradeMachine(
                    progress.Result,
                    progress.Resolutions,
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
                    Resolutions = resolutions,
                    Result = progress.Result,
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
                        Severity = FabricUpgradeAlert.FailureSeverity.Permanent,
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

            FabricUpgradeResolution newResolution = JsonConvert.DeserializeObject<FabricUpgradeResolution>(resolution);
            // TODO: Handle parsing error

            progress.Resolutions.Add(newResolution);

            return progress;
        }

        /// <summary>
        /// Export the Fabric Resources by following the instructions in the progress.
        /// </summary>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <param name="cluster">The cluster (aka region) of the user's workspace.</param>
        /// <param name="workspaceId">The ID of the user's workspace.</param>
        /// <param name="fabricToken">The PowerBI AAD token to authenticate/authorize access to the workspace.</param>
        /// <param name="cancellationToken"/>
        /// <returns>A FabricUpgradeProgress that describes the created/updated resources.</returns>
        public async Task<FabricUpgradeProgress> ExportFabricPipelineAsync(
            string progressString,
            string cluster,
            string workspaceId,
            string fabricToken,
            CancellationToken cancellationToken)
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
                    progress.Resolutions,
                    this.alerts);

            return await machine.ExportAsync(cancellationToken).ConfigureAwait(false);
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

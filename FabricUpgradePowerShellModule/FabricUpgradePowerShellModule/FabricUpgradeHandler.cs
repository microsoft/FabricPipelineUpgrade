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
            if (!this.CheckProgress(progressString, out FabricUpgradeProgress progress))
            {
                return progress;
            }

            AdfSupportFileImporter importer = new AdfSupportFileImporter(progress, fileName, this.alerts);

            return importer.Import();
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

                return machine.Upgrade();
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
        /// selects only the permanent errors and state
        /// </summary>
        /// <param name="progressString">The progress sent by the client.</param>
        /// <returns>A FabricUpgradeProgress that contains only the Permanent error alerts.</returns>
        public FabricUpgradeProgress SelectPermanentAlerts(
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
                if (alert.Severity == FabricUpgradeAlert.AlertSeverity.Permanent)
                {
                    alerts.Add(alert);
                }
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
        /// <param name="workspaceId">The ID of the user's workspace.</param>
        /// <param name="fabricToken">The PowerBI AAD token to authenticate/authorize access to the workspace.</param>
        /// <param name="cancellationToken"/>
        /// <returns>A FabricUpgradeProgress that describes the created/updated resources.</returns>
        public async Task<FabricUpgradeProgress> ExportFabricResourcesAsync(
            string progressString,
            string region,
            string workspaceId,
            string fabricToken,
            CancellationToken cancellationToken)
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

            FabricExportMachine machine = new FabricExportMachine(
                    progress.Result,
                    region,
                    workspaceId,
                    fabricToken,
                    progress.Resolutions,
                    this.alerts);

            return await machine.ExportAsync(cancellationToken).ConfigureAwait(false);
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
    }
}

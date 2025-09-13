// <copyright file="ExposedCommands.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;

namespace FabricUpgradePowerShellModule
{
    /// <summary>
    /// Helper class for PowerShell cancellation token support
    /// </summary>
    internal static class PowerShellCancellationHelper
    {
        public static CancellationToken CreateCancellationToken(PSCmdlet cmdlet)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            
            // Check for stopping periodically using a timer
            var timer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (cmdlet.Stopping)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }
                catch
                {
                    // Ignore any exceptions during stopping check
                }
            }, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));

            // Register cleanup when cancellation is requested
            cancellationTokenSource.Token.Register(() => timer?.Dispose());
            
            return cancellationTokenSource.Token;
        }
    }

    /// <summary>
    /// Import an ADF Support File.
    /// </summary>
    [Cmdlet(VerbsData.Import, "AdfSupportFile")]
    public class ImportAdfSupportFile : PSCmdlet
    {
        [Parameter(
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Progress { get; set; }

        [Alias("sf")]
        [Parameter(Mandatory = true)]
        public string Filename { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter EnableVerboseLogging { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                var cancellationToken = PowerShellCancellationHelper.CreateCancellationToken(this);

                var result = new FabricUpgradeHandler(this.EnableVerboseLogging).ImportAdfSupportFile(
                    this.Progress,
                    this.Filename,
                    true,
                    cancellationToken);

                WriteObject(result.ToString());
            }
            catch (OperationCanceledException)
            {
                WriteWarning("Import operation was cancelled by user request.");
                throw;
            }
        }
    }

    /// <summary>
    /// Import ADF resources directly from Azure Data Factory using REST APIs.
    /// </summary>
    [Cmdlet(VerbsData.Import, "AdfFactory")]
    public class ImportAdfFactory : PSCmdlet
    {
        [Parameter(
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Progress { get; set; }

        [Parameter(Mandatory = true)]
        public string SubscriptionId { get; set; }

        [Parameter(Mandatory = true)]
        public string ResourceGroupName { get; set; }

        [Parameter(Mandatory = true)]
        public string FactoryName { get; set; }

        // Accept string, SecureString, or objects like PSSecureAccessToken
        [Parameter(Mandatory = true, HelpMessage = "ADF access token. Accepts string, SecureString, or object with AccessToken/Token property (e.g. output of Get-AzAccessToken).")]
        public object AdfToken { get; set; }

        [Parameter(Mandatory = false)]
        public string PipelineName { get; set; }

        /// <summary>
        /// Include datasets and linked services that are not used by any pipelines.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Include datasets and linked services that are not used by any pipelines. Useful for factory-level upgrades.")]
        public SwitchParameter IncludeUnusedResources { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter EnableVerboseLogging { get; set; }

        protected override void ProcessRecord()
        {
            string plainAdfToken = TokenUnwrapper.Unwrap(AdfToken, nameof(AdfToken));

            if (EnableVerboseLogging)
            {
                Console.WriteLine($"Importing from ADF Factory: {this.FactoryName} in resource group: {this.ResourceGroupName}");
            }

            try
            {
                var cancellationToken = PowerShellCancellationHelper.CreateCancellationToken(this);

                var task = new FabricUpgradeHandler(this.EnableVerboseLogging).ImportAdfFactoryAsync(
                    this.Progress,
                    this.SubscriptionId,
                    this.ResourceGroupName,
                    this.FactoryName,
                    plainAdfToken,
                    this.PipelineName,
                    this.IncludeUnusedResources,
                    cancellationToken);

                string result = task.GetAwaiter().GetResult().ToString();
                WriteObject(result);
            }
            catch (OperationCanceledException)
            {
                WriteWarning("Import operation was cancelled by user request.");
                throw;
            }
        }
    }

    /// <summary>
    /// This cmdlet accepts a "progress" string that is generated by
    /// Import-AdfSupportFile or Import-Resolutions or Add-Resolution.
    /// It returns a new "progress" string that contains one or more
    /// Fabric Pipeline descriptions.
    /// The output from this cmdlet can be sent to Export-FabricResources.
    /// </summary>
    [Cmdlet(VerbsData.ConvertTo, "FabricResources")]
    public class ConvertToPipeline : Cmdlet
    {
        [Parameter(
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Progress { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter EnableVerboseLogging { get; set; }

        protected override void ProcessRecord()
        {
            if (EnableVerboseLogging)
            {
                Console.WriteLine("Converting ADF resources to Fabric pipeline definitions...");
            }
            WriteObject(new FabricUpgradeHandler(this.EnableVerboseLogging).ConvertToFabricResources(this.Progress).ToString());
        }
    }

    /// <summary>
    /// This cmdlet updates the "progress" field with the resolutions found in a file.
    /// If there are already resolutions, the values in this file will appear "before"
    /// the existing resolutions; therefore, the new resolutions will take precedence
    /// over the old resolutions.
    /// </summary>
    [Cmdlet(VerbsData.Import, "FabricResolutions")]
    public class ImportFabricResolutions : Cmdlet
    {
        [Parameter(
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Progress { get; set; }

        [Alias("rf")]
        [Parameter(Mandatory = false)]
        public string ResolutionsFilename { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter EnableVerboseLogging { get; set; }

        protected override void ProcessRecord()
        {
            if (EnableVerboseLogging)
            {
                Console.WriteLine($"Importing resolutions from file: {this.ResolutionsFilename}");
            }
            
            string result = new FabricUpgradeHandler(this.EnableVerboseLogging).ImportFabricResolutions(
                this.Progress,
                this.ResolutionsFilename).ToString();

            WriteObject(result);
        }
    }

    // This cmdlet takes the progress payload produced by ConvertTo-FabricResources,
    // and takes the workspace and AAD token from named parameters:
    // Import-AdfSupportFile '...' | ConvertTo-FabricResources | Export-FabricResources -Workspace ABC -Token 123
    // This cmdlet uploads the pipelines to the PublicApi endpoint to create/update the items.
    [Cmdlet(VerbsData.Export, "FabricResources")]
    public class ExportFabricPipeline : PSCmdlet
    {
        [Parameter(
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Progress { get; set; }

        [Parameter(Mandatory = false)]
        public string Region { get; set; } = "prod";

        [Alias("ws")]
        [Parameter(Mandatory = true)]
        public string Workspace { get; set; }

        // Accept string, SecureString, PSSecureAccessToken, etc.
        [Alias("ft")]
        [Parameter(Mandatory = true, HelpMessage = "Fabric user access token. Accepts string, SecureString, or object with AccessToken/Token property (e.g. Get-AzAccessToken).")]
        public object Token { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter EnableVerboseLogging { get; set; }

        protected override void ProcessRecord()
        {
            string plainFabricToken = TokenUnwrapper.Unwrap(Token, nameof(Token));
            
            if (EnableVerboseLogging)
            {
                Console.WriteLine($"Exporting Fabric resources to workspace: {this.Workspace} in region: {this.Region}");
            }
            
            try
            {
                var cancellationToken = PowerShellCancellationHelper.CreateCancellationToken(this);

                var task = new FabricUpgradeHandler(this.EnableVerboseLogging).ExportFabricResourcesAsync(
                    this.Progress,
                    this.Region,
                    this.Workspace,
                    plainFabricToken,
                    cancellationToken);

                string result = task.GetAwaiter().GetResult().ToString();
                WriteObject(result);
            }
            catch (OperationCanceledException)
            {
                WriteWarning("Export operation was cancelled by user request.");
                throw;
            }
        }
    }

    // This cmdlet takes the progress payload produced by ConvertTo-FabricResources
    // Import-AdfSupportFile '...' | ConvertTo-FabricResources | Select-WhatIf
    // This cmdlet outputs whether the conversion to Fabric resources will succeed and alerts.
    [Cmdlet("Select", "WhatIf")]
    public class SelectWhatIf : Cmdlet
    {
        [Parameter(
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Progress { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter EnableVerboseLogging { get; set; }

        protected override void ProcessRecord()
        {
            if (EnableVerboseLogging)
            {
                Console.WriteLine($"Performing what-if analysis...");
            }

            string result = new FabricUpgradeHandler(this.EnableVerboseLogging).SelectWhatIf(this.Progress).ToString();
            WriteObject(result);
        }
    }

    internal static class TokenUnwrapper
    {
        internal static string Unwrap(object supplied, string paramName)
        {
            if (supplied == null)
            {
                throw new ArgumentNullException(paramName, $"{paramName} cannot be null.");
            }

            // If a PSObject wrapper was provided, inspect its note properties first.
            if (supplied is PSObject pso)
            {
                // Try well-known property names (case-insensitive)
                object psValue = TryGetPsObjectProperty(pso, "AccessToken") ??
                                 TryGetPsObjectProperty(pso, "Token") ??
                                 TryGetPsObjectProperty(pso, "accessToken") ??
                                 TryGetPsObjectProperty(pso, "token");
                if (psValue != null)
                {
                    return Unwrap(psValue, paramName); // unwrap recursively
                }

                // Fall back to unwrapping the BaseObject if different
                if (pso.BaseObject != null && pso.BaseObject != pso)
                {
                    return Unwrap(pso.BaseObject, paramName);
                }
            }

            // String directly supplied
            if (supplied is string s && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }

            // SecureString supplied
            if (supplied is SecureString ss)
            {
                return SecureStringToString(ss);
            }

            // Try reflection for common Azure token wrapper types (e.g. PSSecureAccessToken, AccessToken, etc.)
            var type = supplied.GetType();
            PropertyInfo? tokenProp = type.GetProperty("AccessToken") ?? type.GetProperty("Token") ?? type.GetProperty("accessToken") ?? type.GetProperty("token");
            if (tokenProp != null)
            {
                object value = tokenProp.GetValue(supplied);
                if (value != null)
                {
                    return Unwrap(value, paramName); // recurse so we handle SecureString, etc.
                }
            }

            throw new ArgumentException($"Unsupported token type '{type.FullName}'. Supply a string, SecureString, PSObject with AccessToken/Token property, or an object with an AccessToken/Token string property.");
        }

        private static object TryGetPsObjectProperty(PSObject pso, string name)
        {
            var prop = pso.Properties[name];
            return prop?.Value;
        }

        private static string SecureStringToString(SecureString secure)
        {
            if (secure == null) return null;
            IntPtr bstr = IntPtr.Zero;
            try
            {
                bstr = Marshal.SecureStringToBSTR(secure);
                return Marshal.PtrToStringBSTR(bstr);
            }
            finally
            {
                if (bstr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(bstr);
                }
            }
        }
    }
}

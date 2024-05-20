# Copyright (c) Microsoft. All rights reserved.
param(
    [Parameter()] [String]$cluster,
    [Parameter()] [String]$workspace,
    # TODO: Is there some better way to get a PowerBI token with the correct scope to call the PublicAPI?
    [Parameter()] [String]$fabricToken,
    [Parameter()] [String]$mode = "Execute",
    [Parameter()] [String]$format = "AdfSupportFiles",
    [Parameter()] [String]$resolutionsFilename = $null,
    [switch]$toFile,
    [switch]$help
)

#############################################################################
# Start of Functions
#############################################################################

# Read-Host has a 1022 character limit (https://github.com/powershell/powershell/issues/16555).
# AAD Tokens can be more than 1022 characters long.
# Therefore, we need a fancy way to work around this.
# This workaround is "inspired by" the workaround in the bug report above.
# Alas, this does not allow you to Ctrl+C from one part of your PowerShell and Ctrl+V into the response
# (the Ctrl+C terminates the process).
function Read-HostAadToken ($prompt = $null) {
    if ($prompt) {
        "${prompt}: " | Write-Host -NoNewline
    }

    $str = ""
    while ($true) { 
        $key = $host.UI.RawUI.ReadKey("NoEcho, IncludeKeyDown"); 

        # Paste the clipboard on CTRL-V        
        if (($key.VirtualKeyCode -eq 0x56) -and  # 0x56 is V
            (([int]$key.ControlKeyState -band [System.Management.Automation.Host.ControlKeyStates]::LeftCtrlPressed) -or 
                ([int]$key.ControlKeyState -band [System.Management.Automation.Host.ControlKeyStates]::RightCtrlPressed))) { 
            $clipboard = Get-Clipboard
            $str += $clipboard
            Write-Host $clipboard -NoNewline
            continue
        }
         elseif ($key.VirtualKeyCode -eq 0x08) {  # 0x08 is Backspace
            if ($str.Length -gt 0) {
                $str = $str.Substring(0, $str.Length - 1)
                Write-Host "`b `b" -NoNewline    
            }
        } 
        elseif ($key.VirtualKeyCode -eq 13) {  # 13 is Enter
            Write-Host
            break 
        }
        elseif ($key.Character -ne 0) {
            $str += $key.Character
            Write-Host $key.Character -NoNewline
        }
    }

    return $str
}

# Allow the user to select the file(s) to send to the Fabric Upgrader.
function SelectUpgradePackage($initialFolder)
{
    # Prompt the user to select the file that contains the UpgradePackage
    Add-Type -AssemblyName System.Windows.Forms
    $FileBrowser = New-Object System.Windows.Forms.OpenFileDialog
    $FileBrowser.InitialDirectory = $initialFolder
    $FileBrowser.Title = "Please select the Upgrade Package"
    $FileBrowser.filter = “UpgradePackages (*_support_live.zip)| *_support_live.zip”
    $null = $FileBrowser.ShowDialog()
    $selectedFile = $FileBrowser.FileName
    if (!$selectedFile) { exit }

    return $selectedFile
}

# Allow the user to select the file(s) to send to the Fabric Upgrader.
function SelectResolutionsFile($initialFolder)
{
    # Prompt the user to select the file that contains the Resolutions
    Add-Type -AssemblyName System.Windows.Forms
    $FileBrowser = New-Object System.Windows.Forms.OpenFileDialog
    $FileBrowser.InitialDirectory = $initialFolder
    $FileBrowser.Title = "Please select your Resolutions file"
    $null = $FileBrowser.ShowDialog()
    $selectedFile = $FileBrowser.FileName
    return $selectedFile
}

# Read the resolutions file named in the command line and
# build the Resolutions component of the FabricUpgradeRequest.
function BuildResolutions()
{
    $resolutions = @()
    if ($resolutionsFilename)
    {
        $preResolutions = [System.IO.File]::ReadAllText($resolutionsFilename)
        $preResolutionsObject = ConvertFrom-Json $preResolutions
        foreach ($preres in $preResolutionsObject)
        {
            $type = $preres.type
            if (!$type) { $type = "LinkedServiceToConnection" }
            $key = $preres.key
            $val = $preres.value
    
            $resolutions += @"
{ "type": "$type", "key": "$key", "value": "$val"}
"@
        }
    }

    return $resolutions -join ","
}

# Add one Resolution to the Resolutions file.
function AddResolution($linkedServiceType, $resolutionType, $linkedServiceName, $datasourceName)
{
    if ($resolutionsFilename)
    {
        Write-Host
        Write-Host "Adding Resolution entry for $linkedServiceType '$linkedServiceName'"
        Write-Host "to file '$resolutionsFilename'."
        Write-Host "Open Resolutions file to fill in the Fabric Connection GUID."
        Write-Host

        $newResolutions = "[`r`n"
        $sep = ""

        $preResolutions = [System.IO.File]::ReadAllText($resolutionsFilename)
        $preResolutionsObject = ConvertFrom-Json $preResolutions
        foreach ($preres in $preResolutionsObject)
        {
            $preresElement = ConvertTo-Json -Depth 100 $preres
            $newResolutions += $sep
            $sep = ",`r`n"

            $newResolutions += $preresElement
        }

        $newResolutions += $sep
        
        $comments = @"
    "comments": [
        "Resolve the ADF $linkedServiceType '$linkedServiceName' to a Fabric Connection"
"@
        if (![String]::IsNullOrEmpty($datasourceName))
        {
            $comments += @"
,
        "This connection uses a datasource named '$datasourceName'"
"@
        }
        $comments += "`r`n]"
        $newResolutions += @"
{
   $comments,
   "type": "$resolutionType",
   "key": "$linkedServiceName",
   "value": "<TODO: Add your new connection GUID here>"
}
"@
        $newResolutions += "`r`n]"
        $newResolutions = $newResolutions | ConvertFrom-Json | ConvertTo-Json

        $newResolutions | Out-File $resolutionsFilename
    }    
}

# From the FabricUpgradeResponse, find the alerts that indicate a need to add a Resolution.
# For each such alert, add _most_ of a Resolution to the Resolutions file.
function UpdateResolutions($responsePayload)
{
    $responseObject = ConvertFrom-Json $responsePayload
    $alerts = $responseObject.alerts
    foreach ($alert in $alerts)
    {
        if ($alert.severity -eq "RequiresUserAction")
        {
            $cxHints = $alert.connectionHints
            AddResolution $cxHints.connectionType $cxHints.resolutionType $cxHints.linkedServiceName $cxHints.datasource
        }
    }
}

# When the code makes a call to the Public API endpoint that exposes the FabricUpgrader,
# the URL will start with this string.
function PublicApiBaseUrl()
{
    # Compute where to send this request, based on $cluster.
    $publicApiBaseUrl = switch($cluster)
    {
        "daily" { "https://dailyapi.fabric.microsoft.com/v1" }
        "dxt" { "https://dxtapi.fabric.microsoft.com/v1" }
        "msit" { "https://msitapi.fabric.microsoft.com/v1" }
        "prod" { "https://api.fabric.microsoft.com/v1" }
        default { "<exit>" }
    }

    if ($publicApiBaseUrl.Equals("<exit>"))
    {
        Write-Host "Cannot connect to the cluster" $cluster
        exit
    }

    return $publicApiBaseUrl
}

# Build the URL that invokes the Fabric Upgrader.
function UpgradePackageUrl()
{
    return "$(PublicApiBaseUrl)/workspaces/$workspace/fabricUpgrade/upgradePackage"
}

# Find out information about the current workspace.
function QueryWorkspace()
{
    $workspaceResponse = Invoke-WebRequest `
        -SkipHttpErrorCheck `
        -URI "$(PublicApiBaseUrl)/workspaces/$workspace" `
        -Method GET `
        -Headers @{Authorization="Bearer $fabricToken"} `

    return $workspaceResponse
}

# Send the upgrade request to the Fabric Upgrader.
function SendUpgradeRequest($payload)
{
    # PowerShell 7 introduced -SkipHttpErrorCheck, which we use here.
    # Otherwise, Invoke-WebRequest will throw an exception when the server returns a 400 (BadRequest).

    $response = Invoke-WebRequest `
        -SkipHttpErrorCheck `
        -URI "$(UpgradePackageUrl)" `
        -Method POST `
        -Headers @{Authorization="Bearer $fabricToken"} `
        -body $payload `
        -ContentType "application/json"

    return $response
}

# For testing during development, just put a description of the request into <workingFolder>/ToUpdate.json.
function WriteUpgradeRequestToFile($payload)
{
    Write-Host "Writing request to file $workingFolder\ToUpgrade.json"

    # Prepare this payload for legibility as a string by
    # replacing double quotes with single quotes.
    # Remember, this is just for manual inspection during development.
    $packedPayload = $payload.Replace("`"", "'")

    $fullRequest = @"
{
  "url": "$(UpgradePackageUrl)",
  "cluster": "$cluster",
  "workspaceId": "$workspace",
  "token": "$fabricToken",
  "payload": "$packedPayload"
}
"@
    Set-Content -Path "$workingFolder\ToUpgrade.json" -Value $fullRequest
    return $null
}

#############################################################################
# End of Functions
#############################################################################

#############################################################################
# Start validation of environment and parameters
#############################################################################

# We do not require the user to enter any of the command-line arguments before
# we check to see if they have the correct PowerShell version.

# Validate the current PowerShell version.
# We require at least version 7.
$powerShellVersion = $PSVersionTable.PSVersion.Major
if ($powerShellVersion -lt 7)
{
    Write-Host "Current PowerShell Version is" $powerShellVersion "but must be 7 or higher"
    exit
}

# If the user specified -help on the command line, then show the help and exit.
if ($help)
{
    $parameters = (Get-Variable -Scope:'Local' -Include:@($MyInvocation.MyCommand.Parameters.keys) |
        Select-Object Name, Attributes, Description | ForEach-Object { "-$($_.Name)"})
    Write-Host "Usage:" (Get-Command -Name $PSCmdlet.MyInvocation.InvocationName) $parameters
    Write-Host "-cluster: The name of the cluster of your Fabric Workspace."
    Write-Host "-workspace: The name of your Fabric Workspace (this is a GUID)."
    Write-Host "-fabricToken: The AAD token to access your Fabric Workspace."
    Write-Host "              You can obtain this from a browser session attached to your workspace"
    Write-Host "              by pressing F12, selecting the Console tab, and typing powerBIAccessToken."
    Write-Host "-resolutionsFilename: The full path of the file that contains the LinkedService-to-FabricConnection"
    Write-Host "                      mapping for your workspace."
    exit
}

# Populate the command-line parameters with valid values

$validClusters = "daily","dxt","msit"
while ([String]::IsNullOrWhiteSpace($cluster) -or -not($validClusters -contains $cluster))
{
    $cluster = Read-Host "Please enter the name of the cluster of your Fabric Workspace. Valid cluster names are {" (($validClusters) -join ", ") "}"
}

$discardGuid = [System.Guid]::empty
while ([String]::IsNullOrWhiteSpace($workspace) -or 
        ![System.Guid]::TryParse($workspace, [System.Management.Automation.PSReference]$discardGuid))
{
    $workspace = Read-Host "Please enter the GUID of your Fabric Workspace"
}

while([String]::IsNullOrWhiteSpace($fabricToken))
{
    $fabricToken = Read-HostAadToken "Please enter the AAD token to access your Fabric Workspace"
}

# Trim any leading and trailing single quotes from the token.
# It's easier to borrow the token from the browser with the quotes than without, so accommodate this.
if ($fabricToken.StartsWith("'")) { $fabricToken = $fabricToken.Substring(1) }
if ($fabricToken.EndsWith("'")) { $fabricToken = $fabricToken.Substring(0, $fabricToken.Length-1) }
$fabricToken = $fabricToken.Trim()

# Validate the workspaceId and token.
$queryWorkspaceResponse = QueryWorkspace
if ($queryWorkspaceResponse.StatusCode -ne 200)
{
    Write-Host "Cannot query workspace: " $queryWorkspaceResponse
    exit
}

$validModes = "Execute","WhatIf"
while (!($validModes -contains $mode))
{
    $mode = Read-Host "Please enter the Upgrade mode. Valid values are {" (($validModes) -join ", ") "}"
}

$validFormats = "AdfSupportFiles"
while (!($validFormats -contains $format))
{
    $format = Read-Host "Please enter the format of the Upgrade Package. Valid values are {" (($validFormats) -join ", ") "}"
}

if ([String]::IsNullOrWhiteSpace($resolutionsFilename))
{
    $resolutionsFilename = SelectResolutionsFile($PSScriptRoot)
}

# Trim any leading and trailing single quotes from the resolutionsFilename.
# This allows the user to include whitespace in the filename.
if ($resolutionsFilename.StartsWith("'")) { $resolutionsFilename = $resolutionsFilename.Substring(1) }
if ($resolutionsFilename.StartsWith("`"")) { $resolutionsFilename = $resolutionsFilename.Substring(1) }
if ($resolutionsFilename.EndsWith("'")) { $resolutionsFilename = $resolutionsFilename.Substring(0, $resolutionsFilename.Length-1) }
if ($resolutionsFilename.EndsWith("`"")) { $resolutionsFilename = $resolutionsFilename.Substring(0, $resolutionsFilename.Length-1) }
$resolutionsFilename = $resolutionsFilename.Trim()

$resolutionString = BuildResolutions

#############################################################################
# End validation of environment and parameters
#############################################################################

#############################################################################
# Start of Main
#############################################################################

$workspaceInfo = $queryWorkspaceResponse | Select-Object -ExpandProperty Content | ConvertFrom-Json
# Here, you can extract things like capacityId, etc., if you need them.

if ([String]::IsNullOrWhiteSpace($upgradePackage))
{
    # Start by assuming that the UpgradePackage is in Downloads.
    # If the user selects a different folder when browsing for the UpgradePackage,
    # then we will update the working folder.
    $workingFolder = (New-Object -ComObject Shell.Application).NameSpace('shell:Downloads').Self.Path
    $upgradePackage = SelectUpgradePackage $workingFolder

    $workingFolder = Split-Path -Parent $upgradePackage
}

Write-Host "Upgrading file: $upgradePackage"

# Read this file and convert its contents to base64...
$fileContents = [System.IO.File]::ReadAllBytes($upgradePackage)
$fileContentsBase64 = [System.Convert]::ToBase64String($fileContents)

# ... build the payload for the request...
$upgradeRequestPayload = @"
{
    "upgradeMode":"$mode",
    "resolutions": [ $resolutionString ],
    "packageFormat":"$format",
    "package": "$fileContentsBase64"
}
"@

# ... and send the request!
if ($toFile)
{
    $upgradeResponse = WriteUpgradeRequestToFile $upgradeRequestPayload
}
else
{
    $upgradeResponse = SendUpgradeRequest $upgradeRequestPayload
    Write-Host "StatusCode =" $upgradeResponse.StatusCode
}

Write-Host $upgradeResponse

if ($upgradeResponse.StatusCode -eq 400)
{
    # The alerts inside the message may contain descriptions of required Connection resolutions.
    # Update the Resolutions file with "stub" versions and notify the user.
    $upgradeResponsePayload = $upgradeResponse | Select-Object -ExpandProperty Content | ConvertFrom-Json
    $upgradeResponseMessage = $upgradeResponsePayload.message
    UpdateResolutions $upgradeResponseMessage
}

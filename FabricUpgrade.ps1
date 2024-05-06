# Copyright (c) Microsoft. All rights reserved.
param(
    
    [Parameter(Mandatory)] [String]$cluster,
    [Parameter(Mandatory)] [String]$workspace,
    # TODO: Is there some better way to get a PowerBI token with the correct scope to call the PublicAPI?
    [Parameter(Mandatory)] [String]$fabricToken,
    [Parameter()] [String]$mode = "Execute",
    [Parameter()] [String]$format = "AdfSupportFiles",
    [Parameter()] [String]$resolutionsFilename = $null,
    [switch]$toFile
)

#############################################################################
# Start of Functions
#############################################################################

# Allow the user to select the file(s) to send to the Fabric Upgrader.
function SelectUpgradePackage()
{
    # Prompt the user to select the file that contains the UpgradePackage
    Add-Type -AssemblyName System.Windows.Forms
    $FileBrowser = New-Object System.Windows.Forms.OpenFileDialog -Property @{ InitialDirectory = $workingFolder }
    $null = $FileBrowser.ShowDialog()
    $selectedFile = $FileBrowser.FileName
    if (!$selectedFile) { exit }
    Write-Host "Upgrading file: $selectedFile"
    $workingFolder = Split-Path -Parent $selectedFile
    # ... read this file and convert its contents to base64...
    $fileContents = [System.IO.File]::ReadAllBytes($selectedFile)
    return [System.Convert]::ToBase64String($fileContents)
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

# For now, we must use the PowerBI WebApi endpoint.
# Once the FabricUpgrader PublicApi is available, then we can use that and remove this method.
function WebApiBaseUrl()
{
    # Compute where to send this request, based on $cluster.
    $webApiBaseUrl = switch($cluster)
    {
        "daily" { "https://pbipdailyeus2euap-daily.pbidedicated.windows.net/webapi"}
        "dxt" { "https://pbipdxtcuseuap1-dxt.pbidedicated.windows.net/webapi"}
        "msit" { "https://pbipmsitwcus22-msit.pbidedicated.windows.net/webapi"}
        default { "<exit>" }
    }

    if ($webApiBaseUrl.Equals("<exit>"))
    {
        Write-Host "Cannot connect to the cluster" $cluster
        exit
    }

    return $webApiBaseUrl
}

# Build the URL that invokes the Fabric Upgrader.
function UpgradePackageUrl()
{
    # For now, we use the PowerBI webapi endpoints, so build that URL.
    # We computed the capacityId when we called QueryWorkspace, below.
    return "$(WebApiBaseUrl)/capacities/$capacityId/workloads/DI/DiService/direct/workspaces/$workspace/fabricUpgrade/upgradePackage?api-version=2022-01-01-preview"
    
    # When the FabricUpgrade PublicAPI endpoints are working, then we will build the URL for that endpoint.
    # TODO: This next line is almost certainly wrong. Find out how Public API exposes workload controllers, and fix it here.
    # return "$(PublicApiUrl)/workspaces/$workspace/fabricUpgrade/upgradePackage"
}

# Find out information about the current workspace.
# We currently need this because we need to extract the CapacityId to build the UpgradePackageUrl.
# We will continue to want this, because we will use it to validate the workspace.
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
# Start of Main
#############################################################################

# Validate command-line parameters.
$allParametersAreValid = $true

$validClusters = "daily","dxt","msit"
if (!($validClusters -contains $cluster))
{
    Write-Host "-cluster option must be one of {" (($validClusters) -join ", ") "}"
    $allParametersAreValid = $false
}

$discardGuid = [System.Guid]::empty
if (![System.Guid]::TryParse($workspace, [System.Management.Automation.PSReference]$discardGuid))
{
    Write-Host "-workspace option must be a GUID"
    $allParametersAreValid = $false
}

$validModes = "Execute","WhatIf"
if (!($validModes -contains $mode))
{
    Write-Host "-mode option must be one of {" (($validModes) -join ", ") "}"
    $allParametersAreValid = $false
}

$validFormats = "AdfSupportFiles"
if (!($validFormats -contains $format))
{
    Write-Host "-format option must be one of {" (($validFormats) -join ", ") "}"
    $allParametersAreValid = $false
}

if (!$allParametersAreValid)
{
    exit
}

# Validate the workspaceId and token, and extract the capacityId from a valid response.
$queryWorkspaceResponse = QueryWorkspace
if ($queryWorkspaceResponse.StatusCode -ne 200)
{
    Write-Host "Cannot query workspace: " $queryWorkspaceResponse
    exit
}

$workspaceInfo = $queryWorkspaceResponse | Select-Object -ExpandProperty Content | ConvertFrom-Json
$capacityId = $workspaceInfo.capacityId

# Trim any leading and trailing single quotes from the token.
# It's easier to borrow the token from the browser with the quotes than without, so accommodate this.
if ($fabricToken.StartsWith("'")) { $fabricToken = $fabricToken.Substring(1) }
if ($fabricToken.EndsWith("'")) { $fabricToken = $fabricToken.Substring(0, $cleanToken.Length-1) }
$fabricToken = $fabricToken.Trim()

# Start by assuming that the UpgradePackage is in Downloads.
# If the user selects a different folder when browsing for the UpgradePackage,
# then we will update the working folder.
$workingFolder = (New-Object -ComObject Shell.Application).NameSpace('shell:Downloads').Self.Path

$fileContentsBase64 = SelectUpgradePackage
$resolutionString = BuildResolutions

# Build the payload for the request...
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

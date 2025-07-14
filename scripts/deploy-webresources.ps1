param (
    [string]$CrmUrl,
    [string]$Username,
    [string]$Password
)

Import-Module Microsoft.Xrm.Data.PowerShell -Force

Write-Host "Connecting to Dynamics 365..."
$crmConn = Get-CrmConnection -ServerUrl $CrmUrl -Username $Username -Password $Password

if (-not $crmConn) {
    Write-Host "Failed to connect to CRM. Check credentials and URL."
    exit 1
}

$resources = Import-Csv ./webresources/metadata.csv
$folderPath = "./webresources/"

Write-Host "Starting deployment of web resources..."

foreach ($res in $resources) {
    $resourcePath = "$folderPath$($res.filepath)"

    if (-not (Test-Path $resourcePath)) {
        Write-Host "File not found: $resourcePath"
        continue
    }

    try {
        Write-Host "Processing file: $resourcePath"
        $bytes = [System.IO.File]::ReadAllBytes($resourcePath)
        $base64 = [System.Convert]::ToBase64String($bytes)

        $existing = Get-CrmRecords -EntityLogicalName webresource `
                                   -FilterAttribute name `
                                   -FilterOperator eq `
                                   -FilterValue $res.logicalname `
                                   -Fields webresourceid `
                                   -Connection $crmConn

        if ($existing.Count -gt 0) {
            Write-Host "Updating Web Resource: $($res.logicalname)"
            Set-CrmRecord -EntityLogicalName webresource `
                          -Id $existing.Records[0].webresourceid `
                          -Fields @{ content = $base64 } `
                          -Connection $crmConn
        }
        else {
            Write-Host "Creating Web Resource: $($res.logicalname)"
            New-CrmRecord -EntityLogicalName webresource `
                          -Fields @{
                              name            = $res.logicalname
                              displayname     = $res.displayname
                              description     = $res.description
                              content         = $base64
                              webresourcetype = [int]$res.type
                          } `
                          -Connection $crmConn
        }

        Write-Host "Completed: $($res.logicalname)"
    }
    catch {
        Write-Host "Error for $($res.logicalname): $_"
    }
}

Write-Host "Publishing all customizations..."
Publish-CrmAllCustomization -Connection $crmConn

Write-Host "Deployment completed."

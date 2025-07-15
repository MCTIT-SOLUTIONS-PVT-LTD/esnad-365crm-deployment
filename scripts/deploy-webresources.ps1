param (
    [string]$CrmUrl,
    [string]$Username,
    [string]$Password
)

Import-Module Microsoft.Xrm.Data.PowerShell -Force

Write-Host "Connecting to Dynamics 365 (IFD)..."

# IFD connection string
$connectionString = @"
AuthType=IFD;
Url=$CrmUrl;
Domain=crm-esnad.com;
Username=$Username;
Password=$Password;
HomeRealmUri=https://sts1.crm-esnad.com/adfs/services/trust/13/usernamemixed;
RequireNewInstance=true;
"@

try {
    $crmConn = Get-CrmConnection -ConnectionString $connectionString
    Set-Variable -Name conn -Value $crmConn -Scope Global  # Required for module to work

    if ($crmConn) {
        Write-Host "Connection successful."
    } else {
        Write-Host "Failed to connect. No connection returned."
        exit 1
    }
}
catch {
    Write-Host "Exception while connecting: $($_.Exception.Message)"
    exit 1
}

Write-Host "Starting deployment of web resources..."

# Load metadata
$resources = Import-Csv ./webresources/metadata.csv
$folderPath = "./webresources/"

foreach ($res in $resources) {
    $resourcePath = Join-Path $folderPath $res.filepath

    if (-not (Test-Path $resourcePath)) {
        Write-Host "File not found: $resourcePath"
        continue
    }

    try {
        $bytes = [System.IO.File]::ReadAllBytes($resourcePath)
        $base64 = [System.Convert]::ToBase64String($bytes)

        $existing = Get-CrmRecords -EntityLogicalName webresource `
                                   -FilterAttribute name `
                                   -FilterOperator eq `
                                   -FilterValue $res.logicalname `
                                   -Fields webresourceid

        if ($existing.Count -gt 0) {
            Write-Host "Updating Web Resource: $($res.logicalname)"
            Set-CrmRecord -EntityLogicalName webresource `
                          -Id $existing.Records[0].webresourceid `
                          -Fields @{ content = $base64 }
        } else {
            Write-Host "Creating Web Resource: $($res.logicalname)"
            New-CrmRecord -EntityLogicalName webresource `
                          -Fields @{
                              name            = $res.logicalname
                              displayname     = $res.displayname
                              description     = $res.description
                              content         = $base64
                              webresourcetype = New-Object Microsoft.Xrm.Sdk.OptionSetValue([int]($res.type.Trim()))
                          }
            Write-Host "Deploying $($res.logicalname) with type = '$($res.type)' → Trimmed: '$($res.type.Trim())'"
        }

        Write-Host "Successfully deployed: $($res.logicalname)"
    }
    catch {
        Write-Host "Error processing $($res.logicalname): $($_.Exception.Message)"
    }
}

Write-Host "Publishing all customizations..."
Publish-CrmAllCustomization

Write-Host "Deployment completed successfully."

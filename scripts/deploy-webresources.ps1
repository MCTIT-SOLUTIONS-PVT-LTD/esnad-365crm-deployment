param (
    [string]$CrmUrl,
    [string]$Username,
    [string]$Password
)

Import-Module Microsoft.Xrm.Data.PowerShell -Force

Write-Host "🔐 Connecting to Dynamics 365 (IFD)..."

# Use the working IFD connection string with usernamemixed endpoint
$connectionString = "AuthType=IFD;Url=$CrmUrl;Domain=crm-esnad.com;Username=$Username;Password=$Password;RequireNewInstance=true"

# Connect
try {
    $crmConn = Get-CrmConnection -ConnectionString $connectionString
    if (-not $crmConn) {
        Write-Host "❌ Failed to connect to CRM. Check credentials and connection string."
        exit 1
    }
    Write-Host "✅ Connected to CRM successfully."
}
catch {
    Write-Host "❌ Exception during connection: $($_.Exception.Message)"
    exit 1
}

Write-Host "📂 Starting deployment of web resources..."

# Read metadata
$resources = Import-Csv ./webresources/metadata.csv
$folderPath = "./webresources/"

foreach ($res in $resources) {
    $resourcePath = Join-Path $folderPath $res.filepath

    if (-not (Test-Path $resourcePath)) {
        Write-Host "❌ File not found: $resourcePath"
        continue
    }

    try {
        $bytes = [System.IO.File]::ReadAllBytes($resourcePath)
        $base64 = [System.Convert]::ToBase64String($bytes)

        $existing = Get-CrmRecords -EntityLogicalName webresource `
                                   -FilterAttribute name `
                                   -FilterOperator eq `
                                   -FilterValue $res.logicalname `
                                   -Fields webresourceid `
                                   -Connection $crmConn

        if ($existing.Count -gt 0) {
            Write-Host "🔁 Updating Web Resource: $($res.logicalname)"
            Set-CrmRecord -EntityLogicalName webresource `
                          -Id $existing.Records[0].webresourceid `
                          -Fields @{ content = $base64 } `
                          -Connection $crmConn
        } else {
            Write-Host "➕ Creating Web Resource: $($res.logicalname)"
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

        Write-Host "✅ Successfully deployed: $($res.logicalname)"
    }
    catch {
        Write-Host "❌ Error processing $($res.logicalname): $($_.Exception.Message)"
    }
}

Write-Host "📢 Publishing all customizations..."
Publish-CrmAllCustomization -Connection $crmConn

Write-Host "🏁 Deployment completed successfully."

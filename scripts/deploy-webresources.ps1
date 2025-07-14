param (
  [string]$CrmUrl,
  [string]$Username,
  [string]$Password
)

Import-Module Microsoft.Xrm.Data.PowerShell -Force

Write-Host "🔗 Connecting to CRM: $CrmUrl with user $Username"
$crmConn = Get-CrmConnection -ServerUrl $CrmUrl -Username $Username -Password $Password

$resources = Import-Csv ./webresources/metadata.csv
$folderPath = "./webresources/"

Write-Host "📦 Found $($resources.Count) web resources to process..."

foreach ($res in $resources) {
    $fullPath = "$folderPath$($res.filepath)"

    if (-not (Test-Path $fullPath)) {
        Write-Warning "⚠️ File not found: $fullPath — skipping $($res.logicalname)"
        continue
    }

    Write-Host "📄 Processing file: $fullPath"

    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    $base64 = [System.Convert]::ToBase64String($bytes)

    $existing = Get-CrmRecords -EntityLogicalName webresource -FilterAttribute name -FilterOperator eq -FilterValue $res.logicalname -Fields webresourceid -Connection $crmConn

    if ($existing.Count -gt 0) {
        Write-Host "🔁 Updating existing Web Resource: $($res.logicalname)"
        Set-CrmRecord -EntityLogicalName webresource -Id $existing.Records[0].webresourceid `
            -Fields @{ content = $base64 } -Connection $crmConn
    } else {
        Write-Host "➕ Creating new Web Resource: $($res.logicalname)"
        New-CrmRecord -EntityLogicalName webresource -Fields @{
            name           = $res.logicalname
            displayname    = $res.displayname
            description    = $res.description
            content        = $base64
            webresourcetype = [int]$res.type
        } -Connection $crmConn
    }
}

Write-Host "🚀 Publishing all customizations..."
Publish-CrmAllCustomization -Connection $crmConn

Write-Host "✅ Deployment completed!"

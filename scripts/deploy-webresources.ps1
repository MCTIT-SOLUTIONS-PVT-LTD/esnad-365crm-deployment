param (
  [string]$CrmUrl,
  [string]$Username,
  [string]$Password
)

Import-Module Microsoft.Xrm.Data.PowerShell -Force

$crmConn = Get-CrmConnection -ServerUrl $CrmUrl -Username $Username -Password $Password

$resources = Import-Csv ./webresources/metadata.csv
$folderPath = "./webresources/"

foreach ($res in $resources) {
    $bytes = [System.IO.File]::ReadAllBytes("$folderPath$($res.filepath)")
    $base64 = [System.Convert]::ToBase64String($bytes)

    $existing = Get-CrmRecords -EntityLogicalName webresource -FilterAttribute name -FilterOperator eq -FilterValue $res.logicalname -Fields webresourceid -Connection $crmConn
    if ($existing.Count -gt 0) {
        Write-Host "🔁 Updating existing Web Resource: $($res.logicalname)"
        Set-CrmRecord -EntityLogicalName webresource -Id $existing.Records[0].webresourceid `
            -Fields @{ content = $base64 }
    } else {
        Write-Host "➕ Creating new Web Resource: $($res.logicalname)"
        New-CrmRecord -EntityLogicalName webresource -Fields @{
            name        = $res.logicalname
            displayname = $res.displayname
            description = $res.description
            content     = $base64
            webresourcetype = [int]$res.type
        } -Connection $crmConn
    }
}

Publish-CrmAllCustomization -Connection $crmConn

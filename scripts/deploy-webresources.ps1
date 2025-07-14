param (
    [string]$CrmUrl,
    [string]$Username,
    [string]$Password
)

Import-Module Microsoft.Xrm.Data.PowerShell -Force

Write-Host "🔐 Connecting to Dynamics 365..."

$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential ($Username, $securePassword)

$crmConn = Get-CrmConnection -ServerUrl $CrmUrl -Credential $cred

if (-not $crmConn) {
    Write-Host "❌ Failed to connect to CRM. Check credentials and URL."
    exit 1
}

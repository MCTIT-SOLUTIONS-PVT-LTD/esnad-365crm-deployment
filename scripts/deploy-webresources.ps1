param (
    [string]$CrmUrl,
    [string]$Username,
    [string]$Password
)

Import-Module Microsoft.Xrm.Data.PowerShell -Force

Write-Host "Connecting to Dynamics 365..."

$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential ($Username, $securePassword)

try {
    $crmConn = Get-CrmConnection -ServerUrl $CrmUrl -Credential $cred

    if ($crmConn) {
        Write-Host "✅ Connection successful."
    } else {
        Write-Host "❌ Failed to connect. No connection returned."
        exit 1
    }
}
catch {
    Write-Host "❌ Exception while connecting: $($_.Exception.Message)"
    exit 1
}

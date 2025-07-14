param (
    [string]$CrmUrl,
    [string]$Username,
    [string]$Password
)

# Load the Dynamics module
Import-Module Microsoft.Xrm.Data.PowerShell -Force

Write-Host "🔐 Connecting to Dynamics 365..."

# Convert password to secure string
$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential ($Username, $securePassword)

# Try to connect
try {
    $crmConn = Get-CrmConnection -ServerUrl $CrmUrl -Credential $cred

    if ($crmConn) {
        Write-Host "✅ Connection successful."
    } else {
        Write-Host "❌ Failed to connect. No connection object returned."
        exit 1
    }
}
catch {
    Write-Host "❌ Exception while connecting: $_"
    exit 1
}

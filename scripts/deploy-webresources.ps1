param (
    [string]$CrmUrl,
    [string]$Username,
    [string]$Password
)

Import-Module Microsoft.Xrm.Data.PowerShell -Force

Write-Host "Connecting to Dynamics 365..."

$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
# $connectionString = "AuthType=Office365;Url=$CrmUrl;Username=$Username;Password=$Password"

$connectionString = "AuthType=IFD;Url=$CrmUrl;Username=$Username;Password=$Password;HomeRealmUri=https://sts1.crm-esnad.com/adfs/services/trust"

try {
    $crmConn = Get-CrmConnection -ConnectionString $connectionString

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

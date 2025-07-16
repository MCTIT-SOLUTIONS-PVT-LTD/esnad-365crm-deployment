param(
    [string]$crmUrl,
    [string]$username,
    [string]$password,
    [string]$solutionPath
)

Write-Host "📦 Starting D365 Unmanaged Solution Import..."
Write-Host "➡️ CRM URL: $crmUrl"
Write-Host "➡️ Username: $username"
Write-Host "➡️ Solution File: $solutionPath"

# Install required module if missing
Install-Module -Name Microsoft.Xrm.Data.PowerShell -Force -Scope CurrentUser

# Build connection string for IFD with ADFS
$connectionString = @"
AuthType=IFD;
Url=$crmUrl;
Domain=crm-esnad.com;
Username=$username;
Password=$password;
HomeRealmUri=https://sts1.crm-esnad.com/adfs/services/trust/13/usernamemixed;
RequireNewInstance=true;
"@

# Connect using connection string
$connection = Get-CrmConnection -ConnectionString $connectionString

# Import unmanaged solution
Import-CrmSolution `
    -conn $connection `
    -SolutionFilePath $solutionPath `
    -OverwriteUnManagedCustomizations:$true `
    -PublishWorkflows:$true `
    -Verbose

# Publish all changes
Publish-CrmAllCustomization -conn $connection

Write-Host "✅ Solution import and publish completed."

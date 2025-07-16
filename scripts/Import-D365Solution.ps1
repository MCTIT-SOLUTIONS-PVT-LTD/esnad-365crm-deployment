param(
  [string]$crmUrl,
  [string]$username,
  [string]$password,
  [string]$solutionPath
)

Write-Host "📦 Starting Unmanaged Solution Import..."

Install-Module Microsoft.Xrm.Data.PowerShell -Force -Scope CurrentUser

$securePassword = ConvertTo-SecureString $password -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential ($username, $securePassword)

$connection = Connect-CrmOnline -ServerUrl $crmUrl -Credential $cred -AuthType IFDP

Import-CrmSolution `
  -conn $connection `
  -SolutionFilePath $solutionPath `
  -OverwriteUnManagedCustomizations:$true `
  -PublishWorkflows:$true `
  -Verbose

Write-Host "✅ Solution imported and published successfully"

# Signs one file with Artifact Signing, as Inno Setup's sign tool in the release workflow.
# The workflow's earlier steps log in to Azure and install the ArtifactSigning module.
param([Parameter(Mandatory)][string]$File)

$ErrorActionPreference = 'Stop'

$params = @{
  Endpoint                           = 'https://neu.codesigning.azure.net/'
  CodeSigningAccountName             = 'asa-bits'
  CertificateProfileName             = 'exception-uploader'
  Files                              = $File
  FileDigest                         = 'SHA256'
  TimestampRfc3161                   = 'http://timestamp.acs.microsoft.com'
  TimestampDigest                    = 'SHA256'
  ExcludeWorkloadIdentityCredential  = $true
  ExcludeManagedIdentityCredential   = $true
  ExcludeSharedTokenCacheCredential  = $true
  ExcludeVisualStudioCredential      = $true
  ExcludeVisualStudioCodeCredential  = $true
  ExcludeAzurePowerShellCredential   = $true
  ExcludeAzureDeveloperCliCredential = $true
  ExcludeInteractiveBrowserCredential = $true
}
Invoke-ArtifactSigning @params

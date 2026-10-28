[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Manifest,
    [Parameter(Mandatory = $true)][string] $PrivateKeyPath,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-zA-Z0-9._-]{1,80}$')][string] $KeyId,
    [string] $SignaturePath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
$keyPath = (Resolve-Path -LiteralPath $PrivateKeyPath).Path
if (!$SignaturePath) { $SignaturePath = Join-Path (Split-Path $manifestPath -Parent) 'office-release.signature.json' }
$signatureFullPath = [IO.Path]::GetFullPath($SignaturePath)
if ($signatureFullPath -eq $manifestPath -or $signatureFullPath -eq $keyPath) { throw 'The signature output must differ from the manifest and private key paths.' }
$rsa = [Security.Cryptography.RSA]::Create()
try {
    $rsa.ImportFromPem([IO.File]::ReadAllText($keyPath))
    if ($rsa.KeySize -lt 3072) { throw 'Office signing requires an RSA key of at least 3072 bits.' }
    $bytes = [IO.File]::ReadAllBytes($manifestPath)
    if ($bytes.Length -gt 1MB) { throw 'Office release manifest exceeds its size limit.' }
    $signature = $rsa.SignData($bytes, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pss)
    $result = [ordered]@{ schemaVersion = 1; keyId = $KeyId; signature = [Convert]::ToBase64String($signature) }
    $json = $result | ConvertTo-Json
    [IO.File]::WriteAllText($signatureFullPath, $json, [Text.UTF8Encoding]::new($false))
    Write-Output "Wrote Office manifest signature: $SignaturePath"
} finally { $rsa.Dispose() }

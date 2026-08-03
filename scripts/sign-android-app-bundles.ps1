<#
.SYNOPSIS
    Signs Broiler's Android app bundles with the release key held in repository secrets.

.DESCRIPTION
    An .aab is signed with jarsigner, not apksigner: apksigner only speaks the APK signature
    schemes, which a bundle does not carry, and the JAR signature produced here is what Play
    checks on upload.

    The key material never reaches a command line or the repository. The script reads four
    environment variables, which .github/workflows/broiler-preview-package.yml maps from
    repository secrets of the same names:

        ANDROID_KEYSTORE_BASE64      the keystore file, base64-encoded (PKCS#12 or JKS)
        ANDROID_KEYSTORE_PASSWORD    password of that keystore
        ANDROID_KEY_ALIAS            alias of the signing key inside it
        ANDROID_KEY_PASSWORD         password of that key

    The keystore is decoded to a temporary file outside the workspace — so no packaging glob or
    artifact upload can reach it — and deleted again before the script returns. The two passwords
    are handed to jarsigner through its -storepass:env / -keypass:env forms, so they never appear
    in the process list.

    Signing is verified rather than assumed: jarsigner must report the bundle verified, the
    signer certificate must be the one the keystore holds under ANDROID_KEY_ALIAS, and the bundle
    must carry a signature block. `jarsigner -verify` exits 0 on an unsigned jar, so an exit code
    alone proves nothing.

    Returns the SHA-256 fingerprint of the signing certificate, for BUILD-INFO.txt and the release
    notes to quote.

.PARAMETER BundlePath
    App bundles to sign in place.

.PARAMETER ValidateOnly
    Check the secrets, the toolchain, and the keystore without signing anything. The preview
    package workflow runs this before the Android build so a missing or wrong secret fails in
    seconds instead of after an hour of building.

.EXAMPLE
    ./scripts/sign-android-app-bundles.ps1 -ValidateOnly

.EXAMPLE
    ./scripts/sign-android-app-bundles.ps1 -BundlePath ./Broiler.Browser.aab, ./Broiler.Writer.aab
#>
[CmdletBinding(DefaultParameterSetName = 'Sign')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Sign', Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string[]] $BundlePath,

    [Parameter(Mandatory = $true, ParameterSetName = 'Validate')]
    [switch] $ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# keytool and jarsigner report through exit codes this script inspects itself; letting PowerShell
# turn a non-zero exit into its own terminating error would replace every diagnostic below with
# 'the command failed'.
$PSNativeCommandUseErrorActionPreference = $false

# keytool and jarsigner localize their output, and this script parses it. Pin them to English so a
# runner's locale cannot turn 'jar verified.' into something the check does not recognize.
$JdkLocaleArguments = @('-J-Duser.language=en', '-J-Duser.country=US')

$RequiredSecrets = @(
    'ANDROID_KEYSTORE_BASE64'
    'ANDROID_KEYSTORE_PASSWORD'
    'ANDROID_KEY_ALIAS'
    'ANDROID_KEY_PASSWORD'
)

function Resolve-JdkTool {
    param([Parameter(Mandatory = $true)][string] $Name)

    # JAVA_HOME first: a CI runner carries several JDKs, and the one setup-java selected is the
    # one whose keytool matches the jarsigner that does the signing.
    if (-not [string]::IsNullOrWhiteSpace($env:JAVA_HOME)) {
        foreach ($extension in @('', '.exe')) {
            $candidate = Join-Path $env:JAVA_HOME "bin/$Name$extension"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }

    $onPath = @(Get-Command -Name $Name -CommandType Application -ErrorAction SilentlyContinue)
    if ($onPath.Count -gt 0) {
        return $onPath[0].Source
    }

    throw "$Name was not found. Set JAVA_HOME to a JDK 21 installation, or put $Name on PATH."
}

function Invoke-JdkTool {
    param(
        [Parameter(Mandatory = $true)][string] $Executable,
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [string] $StandardInput
    )

    # Merged stderr arrives as error records; they are diagnostics to report, not failures in
    # themselves, so they must not trip $ErrorActionPreference. The assignment is function-scoped.
    $ErrorActionPreference = 'Continue'

    $output = if ($PSBoundParameters.ContainsKey('StandardInput')) {
        $StandardInput | & $Executable @Arguments 2>&1
    }
    else {
        & $Executable @Arguments 2>&1
    }

    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = (@($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine)
    }
}

function Get-CertificateFingerprint {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $ToolOutput)

    # 'SHA256:' on current JDKs, 'SHA-256:' on older ones, always 32 colon-separated hex bytes.
    $found = [regex]::Matches($ToolOutput, '(?im)^\s*SHA-?256:\s*((?:[0-9A-F]{2}:){31}[0-9A-F]{2})\s*$')
    return @($found | ForEach-Object { $_.Groups[1].Value.ToUpperInvariant() })
}

function Assert-SignatureBlock {
    param([Parameter(Mandatory = $true)][string] $Path)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName })
    }
    finally {
        $archive.Dispose()
    }

    # A JAR signature is three entries: the manifest of digests, the .SF signature file over it,
    # and the .RSA/.DSA/.EC block holding the signature and the signer certificate.
    $missing = @()
    if ($entries -notcontains 'META-INF/MANIFEST.MF') { $missing += 'META-INF/MANIFEST.MF' }
    if (-not ($entries | Where-Object { $_ -match '^META-INF/[^/]+\.SF$' })) { $missing += 'META-INF/*.SF' }
    if (-not ($entries | Where-Object { $_ -match '^META-INF/[^/]+\.(RSA|DSA|EC)$' })) { $missing += 'META-INF/*.RSA' }

    if ($missing) {
        throw "$Path carries no JAR signature after signing; missing $($missing -join ', ')."
    }
}

$missingSecrets = @($RequiredSecrets | Where-Object {
        [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
    })
if ($missingSecrets) {
    $verb = if ($missingSecrets.Count -eq 1) { 'is' } else { 'are' }
    throw @"
Android release signing is not configured: $($missingSecrets -join ', ') $verb empty or unset.

All four values are repository secrets, set under Settings > Secrets and variables > Actions:

  ANDROID_KEYSTORE_BASE64      base64 of the release keystore ('base64 -w0 release.keystore')
  ANDROID_KEYSTORE_PASSWORD    password of that keystore
  ANDROID_KEY_ALIAS            alias of the signing key inside it
  ANDROID_KEY_PASSWORD         password of that key

A preview package with unsigned bundles is not a package this workflow ships, so this is a hard
failure rather than a fallback.
"@
}

$keytool = Resolve-JdkTool -Name 'keytool'
$jarsigner = Resolve-JdkTool -Name 'jarsigner'

$storePassword = [Environment]::GetEnvironmentVariable('ANDROID_KEYSTORE_PASSWORD')
$keyAlias = [Environment]::GetEnvironmentVariable('ANDROID_KEY_ALIAS')

# RUNNER_TEMP, never the workspace: nothing under the checkout may hold key material, where a
# packaging glob or an artifact upload could pick it up.
$temporaryRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { [System.IO.Path]::GetTempPath() } else { $env:RUNNER_TEMP }
$signingDirectory = Join-Path $temporaryRoot ('broiler-android-signing-' + [Guid]::NewGuid().ToString('N'))
$keystorePath = Join-Path $signingDirectory 'release.keystore'
$fingerprint = $null

try {
    New-Item -ItemType Directory -Force -Path $signingDirectory | Out-Null

    try {
        # Whitespace-tolerant: a secret pasted from wrapped base64 output is still the keystore.
        $keystoreBytes = [Convert]::FromBase64String(([Environment]::GetEnvironmentVariable('ANDROID_KEYSTORE_BASE64') -replace '\s', ''))
    }
    catch {
        throw "ANDROID_KEYSTORE_BASE64 is not valid base64: $($_.Exception.Message)"
    }

    if ($keystoreBytes.Length -eq 0) {
        throw 'ANDROID_KEYSTORE_BASE64 decodes to an empty file.'
    }

    [System.IO.File]::WriteAllBytes($keystorePath, $keystoreBytes)

    # keytool has no :env password form of its own, and a password on a command line is readable
    # by every process on the machine, so it goes in on stdin — which keytool falls back to when
    # it is not attached to a console, as here.
    $listing = Invoke-JdkTool -Executable $keytool -StandardInput $storePassword -Arguments (
        $JdkLocaleArguments + @('-list', '-v', '-keystore', $keystorePath, '-alias', $keyAlias))
    if ($listing.ExitCode -ne 0) {
        throw @"
keytool could not read alias '$keyAlias' from the keystore (exit code $($listing.ExitCode)).
Check ANDROID_KEY_ALIAS against the keystore and ANDROID_KEYSTORE_PASSWORD against its password.

$($listing.Output)
"@
    }

    # @() around the call: PowerShell unrolls a single-element result on the way out of a function,
    # and a lone string is not a collection to count or search.
    $keystoreFingerprints = @(Get-CertificateFingerprint -ToolOutput $listing.Output)
    if ($keystoreFingerprints.Count -eq 0) {
        throw "keytool printed no SHA-256 certificate fingerprint for alias '$keyAlias':`n$($listing.Output)"
    }

    # The first is the leaf — the certificate that signs — with any CA certificates after it.
    $fingerprint = $keystoreFingerprints[0]
    Write-Host "==> signing key '$keyAlias', certificate SHA-256 $fingerprint"

    if ($ValidateOnly) {
        Write-Host '==> Android signing secrets validated; nothing signed.'
    }
    else {
        foreach ($bundle in $BundlePath) {
            if (-not (Test-Path -LiteralPath $bundle -PathType Leaf)) {
                throw "App bundle not found: $bundle"
            }

            $resolved = (Resolve-Path -LiteralPath $bundle).ProviderPath

            # jarsigner rewrites the archive in place. -sigalg is left to jarsigner so an EC key
            # signs as readily as an RSA one; -digestalg is pinned because SHA-256 is the floor
            # Play accepts.
            $signing = Invoke-JdkTool -Executable $jarsigner -Arguments (
                $JdkLocaleArguments + @(
                    '-keystore', $keystorePath
                    '-storepass:env', 'ANDROID_KEYSTORE_PASSWORD'
                    '-keypass:env', 'ANDROID_KEY_PASSWORD'
                    '-digestalg', 'SHA-256'
                    $resolved
                    $keyAlias
                ))
            if ($signing.ExitCode -ne 0) {
                $hint = ''
                if ($signing.Output -match 'not a private key|cannot recover key') {
                    # PKCS#12 keeps one password for the store and its keys; keytool drops a
                    # -keypass that differs from -storepass when it creates one, which surfaces
                    # here as a key that cannot be unlocked.
                    $hint = "`n`nIf the keystore is a PKCS#12 file, ANDROID_KEY_PASSWORD has to be the same value as ANDROID_KEYSTORE_PASSWORD."
                }

                throw "jarsigner failed on $resolved (exit code $($signing.ExitCode)).$hint`n`n$($signing.Output)"
            }

            # jarsigner reports an unsigned jar as 'jar is unsigned.' and still exits 0, so the
            # verdict has to be read out of the output, not the exit code.
            $verification = Invoke-JdkTool -Executable $jarsigner -Arguments (
                $JdkLocaleArguments + @('-verify', '-keystore', $keystorePath, $resolved))
            if ($verification.ExitCode -ne 0 -or $verification.Output -notmatch '(?m)^jar verified\.') {
                throw "jarsigner did not verify $resolved after signing it (exit code $($verification.ExitCode)):`n`n$($verification.Output)"
            }

            Assert-SignatureBlock -Path $resolved

            # Verified is not enough: it says the bundle is signed by *some* key. This says it is
            # signed by ours, which is what a debug key slipping through would fail.
            $signer = Invoke-JdkTool -Executable $keytool -Arguments (
                $JdkLocaleArguments + @('-printcert', '-jarfile', $resolved))
            $signerFingerprints = @(Get-CertificateFingerprint -ToolOutput $signer.Output)
            if ($signerFingerprints -notcontains $fingerprint) {
                $reported = if ($signerFingerprints.Count -gt 0) { $signerFingerprints -join ', ' } else { '(none)' }
                throw "$resolved is not signed by '$keyAlias'. Expected certificate $fingerprint, found: $reported."
            }

            $megabytes = [Math]::Round((Get-Item -LiteralPath $resolved).Length / 1MB, 1)
            Write-Host "==> signed and verified $(Split-Path -Leaf $resolved): $megabytes MB, signer $fingerprint"
        }
    }
}
finally {
    if (Test-Path -LiteralPath $signingDirectory) {
        Remove-Item -LiteralPath $signingDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

return $fingerprint

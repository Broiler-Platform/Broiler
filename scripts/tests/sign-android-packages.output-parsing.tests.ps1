<#
.SYNOPSIS
    Checks that sign-android-packages.ps1 reads the signer certificate out of every shape its
    tools report it in.

.DESCRIPTION
    The signing script proves a package is signed with the release key by finding that key's
    certificate digest in what keytool and apksigner print. That check is only as good as the
    parsing behind it, and a parser that silently matches nothing reads exactly like a package
    signed with the wrong key — which is how a correctly signed APK once failed the release job.

    apksigner has two shapes for the same line, and which one it uses depends on the APK:

        Signer #1 certificate SHA-256 digest: <hex>
        Signer (minSdkVersion=33, maxSdkVersion=2147483647) certificate SHA-256 digest: <hex>

    The second appears when one signer does not cover the whole supported SDK range — a rotated
    key, where the previous certificate still signs for older platforms. It carries no signer
    number at all.

    The fixtures below are real tool output, not invented: the plain shape from an APK signed with
    a single key, the ranged shape from one signed with a two-key lineage, and the keytool listing
    from the keystore behind them. The functions under test are pulled out of the script by name so
    this exercises the shipped code rather than a copy of the pattern.

    Run: pwsh -File scripts/tests/sign-android-packages.output-parsing.tests.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Failures = 0

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)][string] $What,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Expected,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Actual
    )

    $expectedText = if ($Expected.Count -gt 0) { $Expected -join ', ' } else { '(none)' }
    $actualText = if ($Actual.Count -gt 0) { $Actual -join ', ' } else { '(none)' }

    if ($expectedText -eq $actualText) {
        Write-Host "PASS  $What"
    }
    else {
        $script:Failures++
        Write-Host "FAIL  $What"
        Write-Host "        expected: $expectedText"
        Write-Host "        actual  : $actualText"
    }
}

# The functions under test, lifted out of the script by name. Running the script itself would
# demand the four signing secrets and a keystore; its parsing does not.
$scriptPath = Join-Path (Split-Path -Parent (Split-Path -Parent $PSCommandPath)) 'sign-android-packages.ps1'
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Script under test not found: $scriptPath"
}

$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref] $null, [ref] $parseErrors)
if ($parseErrors) {
    throw "$scriptPath does not parse: $($parseErrors -join '; ')"
}

foreach ($name in @('Get-ApkSignerDigest', 'Get-CertificateFingerprint', 'ConvertTo-ComparableFingerprint')) {
    $definition = $ast.FindAll(
        { param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name },
        $true) | Select-Object -First 1

    if (-not $definition) {
        throw "$scriptPath no longer defines $name."
    }

    Set-Item -Path "function:script:$name" -Value ([scriptblock]::Create($definition.Body.Extent.Text.Trim('{', '}')))
}

# --- apksigner, one signer covering the whole SDK range ---------------------------------------
$plain = @'
Verifies
Verified using v1 scheme (JAR signing): false
Verified using v2 scheme (APK Signature Scheme v2): true
Verified using v3 scheme (APK Signature Scheme v3): true
Verified using v3.1 scheme (APK Signature Scheme v3.1): false
Verified using v4 scheme (APK Signature Scheme v4): false
Verified for SourceStamp: false
Number of signers: 1
Signer #1 certificate DN: CN=Broiler Release, O=Broiler, C=DE
Signer #1 certificate SHA-256 digest: ad6acbc532096d6faec0ef9e6eaf856bf0c1d61e7d9aa4dbe6f7ee5978906785
Signer #1 certificate SHA-1 digest: 93663d7eeb75d9fc55bc8e045b16616ca798c8f2
Signer #1 certificate MD5 digest: 1f214e55c907e34390f3575d4322811d
Signer #1 key algorithm: RSA
Signer #1 key size (bits): 2048
Signer #1 public key SHA-256 digest: 59cf115c9bdd0675b5b8036c4330953de6fdb7c384e7fe5b8f58704e5beb1517
'@

Assert-Equal -What 'apksigner: numbered signer' `
    -Expected @('ad6acbc532096d6faec0ef9e6eaf856bf0c1d61e7d9aa4dbe6f7ee5978906785') `
    -Actual @(Get-ApkSignerDigest -ToolOutput $plain)

# --- apksigner, a rotated key: two signers, each covering part of the range --------------------
# No signer number in this shape at all. This is what the release job hit.
$ranged = @'
Verifies
Verified using v1 scheme (JAR signing): true
Verified using v2 scheme (APK Signature Scheme v2): true
Verified using v3 scheme (APK Signature Scheme v3): true
Signer (minSdkVersion=33, maxSdkVersion=2147483647) certificate DN: CN=New Key, O=Broiler, C=DE
Signer (minSdkVersion=33, maxSdkVersion=2147483647) certificate SHA-256 digest: 5daa4d2904841ecedf85997fae86a9ac40430af609b8a1cdc3b6f9aac8cd78b8
Signer (minSdkVersion=33, maxSdkVersion=2147483647) certificate SHA-1 digest: a73750e5a1e5d847c463b3085597e14e80df68f6
Signer (minSdkVersion=33, maxSdkVersion=2147483647) key algorithm: RSA
Signer (minSdkVersion=33, maxSdkVersion=2147483647) public key SHA-256 digest: c0f4c1c09610ec33ffe78539c297a35f0f784ea7d2f7172e1776d61cd385eed9
Signer (minSdkVersion=24, maxSdkVersion=32) certificate DN: CN=Old Key, O=Broiler, C=DE
Signer (minSdkVersion=24, maxSdkVersion=32) certificate SHA-256 digest: 6b3910b503caa833f7bb4334f6810cd9465891e429abceae2d0a3dffb5b3d104
Signer (minSdkVersion=24, maxSdkVersion=32) certificate SHA-1 digest: 69a06a15dc96d9f519f505f577fdc4d3763dfb76
Signer (minSdkVersion=24, maxSdkVersion=32) public key SHA-256 digest: 3170b450aa3272b232549b26201ee4f2ba8d60ce11223344556677889900aabb
'@

Assert-Equal -What 'apksigner: ranged signers, both certificates' `
    -Expected @(
        '5daa4d2904841ecedf85997fae86a9ac40430af609b8a1cdc3b6f9aac8cd78b8'
        '6b3910b503caa833f7bb4334f6810cd9465891e429abceae2d0a3dffb5b3d104'
    ) `
    -Actual @(Get-ApkSignerDigest -ToolOutput $ranged)

# Public-key digests are a different value and must never be mistaken for the certificate's.
$publicKeyDigests = @(
    'c0f4c1c09610ec33ffe78539c297a35f0f784ea7d2f7172e1776d61cd385eed9'
    '59cf115c9bdd0675b5b8036c4330953de6fdb7c384e7fe5b8f58704e5beb1517'
)
$picked = @(Get-ApkSignerDigest -ToolOutput ($plain + "`n" + $ranged))
Assert-Equal -What 'apksigner: public key digests are not treated as certificates' `
    -Expected @() `
    -Actual @($picked | Where-Object { $publicKeyDigests -contains $_ })

# A trailing note on the line must not hide the digest either.
Assert-Equal -What 'apksigner: digest found despite a trailing note' `
    -Expected @('ad6acbc532096d6faec0ef9e6eaf856bf0c1d61e7d9aa4dbe6f7ee5978906785') `
    -Actual @(Get-ApkSignerDigest -ToolOutput 'Signer #1 certificate SHA-256 digest: ad6acbc532096d6faec0ef9e6eaf856bf0c1d61e7d9aa4dbe6f7ee5978906785 (in lineage)')

Assert-Equal -What 'apksigner: nothing to find in an unsigned report' `
    -Expected @() `
    -Actual @(Get-ApkSignerDigest -ToolOutput "DOES NOT VERIFY`nERROR: Missing META-INF/MANIFEST.MF")

# --- keytool ----------------------------------------------------------------------------------
# A chain lists the leaf first; the leaf is the certificate that signs.
$keytool = @'
Alias name: broiler-upload
Entry type: PrivateKeyEntry
Certificate chain length: 2
Certificate[1]:
Owner: CN=Broiler Release, O=Broiler, C=DE
Certificate fingerprints:
	 SHA1: 93:66:3D:7E:EB:75:D9:FC:55:BC:8E:04:5B:16:61:6C:A7:98:C8:F2
	 SHA256: AD:6A:CB:C5:32:09:6D:6F:AE:C0:EF:9E:6E:AF:85:6B:F0:C1:D6:1E:7D:9A:A4:DB:E6:F7:EE:59:78:90:67:85
Certificate[2]:
Owner: CN=Broiler Issuing CA, O=Broiler, C=DE
Certificate fingerprints:
	 SHA1: 69:A0:6A:15:DC:96:D9:F5:19:F5:05:F5:77:FD:C4:D3:76:3D:FB:76
	 SHA256: 6B:39:10:B5:03:CA:A8:33:F7:BB:43:34:F6:81:0C:D9:46:58:91:E4:29:AB:CE:AE:2D:0A:3D:FF:B5:B3:D1:04
'@

$fingerprints = @(Get-CertificateFingerprint -ToolOutput $keytool)
Assert-Equal -What 'keytool: leaf certificate comes first' `
    -Expected @('AD:6A:CB:C5:32:09:6D:6F:AE:C0:EF:9E:6E:AF:85:6B:F0:C1:D6:1E:7D:9A:A4:DB:E6:F7:EE:59:78:90:67:85') `
    -Actual @($fingerprints | Select-Object -First 1)

Assert-Equal -What 'keytool: every certificate in the chain is read' -Expected @('2') -Actual @("$($fingerprints.Count)")

# --- the two renderings have to compare equal -------------------------------------------------
Assert-Equal -What 'keytool and apksigner renderings normalize to the same value' `
    -Expected @('ad6acbc532096d6faec0ef9e6eaf856bf0c1d61e7d9aa4dbe6f7ee5978906785') `
    -Actual @(ConvertTo-ComparableFingerprint -Fingerprint $fingerprints[0])

Write-Host ''
if ($script:Failures -eq 0) {
    Write-Host 'ALL PARSING TESTS PASSED'
}
else {
    Write-Host "$script:Failures PARSING TEST(S) FAILED"
    exit 1
}

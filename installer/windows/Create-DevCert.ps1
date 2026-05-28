<#
.SYNOPSIS
    Creates a self-signed code-signing certificate for development/CI use.
    Run ONCE locally, then add the outputs to GitHub Secrets.

.OUTPUTS
    codesign.pfx      — certificate + private key (keep secret)
    codesign.pfx.b64  — Base64-encoded PFX for GitHub Secret CERTIFICATE_PFX_BASE64

.NOTES
    A self-signed cert removes the "Unknown publisher" dialog but does NOT
    suppress Windows SmartScreen on first run (SmartScreen requires reputation
    built via an EV certificate from DigiCert, Sectigo, GlobalSign, etc.).
    Replace with a real EV cert when commercially distributing.
#>

param(
    [string]$Password     = "MonaServer2GUI_DevSign_2025",
    [string]$OutDir       = $PSScriptRoot,
    [string]$Subject      = "CN=Mehdi Mahdian, O=MonaServer2 GUI, C=IR",
    [int]   $ValidYears   = 5
)

$pfxPath = Join-Path $OutDir "codesign.pfx"
$b64Path = Join-Path $OutDir "codesign.pfx.b64"

Write-Host "Creating self-signed code signing certificate..." -ForegroundColor Cyan

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -FriendlyName "MonaServer2 GUI Code Signing" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3") `
    -NotAfter (Get-Date).AddYears($ValidYears)

Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

$secPass = ConvertTo-SecureString -String $Password -Force -AsPlainText
$cert | Export-PfxCertificate -FilePath $pfxPath -Password $secPass | Out-Null

$pfxBytes = [IO.File]::ReadAllBytes($pfxPath)
$b64      = [Convert]::ToBase64String($pfxBytes)
[IO.File]::WriteAllText($b64Path, $b64, [System.Text.Encoding]::ASCII)

Write-Host ""
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host " Add the following two secrets to GitHub:" -ForegroundColor Yellow
Write-Host " https://github.com/mehdimahdian/MonaServer2-GUI/settings/secrets/actions" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Secret name : CERTIFICATE_PFX_BASE64" -ForegroundColor Cyan
Write-Host "  Secret value: (contents of codesign.pfx.b64)" -ForegroundColor White
Write-Host ""
Write-Host "  Secret name : CERTIFICATE_PFX_PASSWORD" -ForegroundColor Cyan
Write-Host "  Secret value: $Password" -ForegroundColor White
Write-Host ""
Write-Host "Files written:" -ForegroundColor Green
Write-Host "  $pfxPath"
Write-Host "  $b64Path"
Write-Host ""
Write-Host "IMPORTANT: Do NOT commit codesign.pfx or codesign.pfx.b64 to git!" -ForegroundColor Red

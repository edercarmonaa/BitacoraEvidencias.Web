Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $root 'scripts\operations\ScriptCompatibility.ps1')
. (Join-Path $root 'scripts\smoke\Smoke.Common.ps1')
$checks=0
function Assert-Alignment([bool]$Value,[string]$Message) { if (-not $Value) { throw $Message }; $script:checks++ }
function Assert-AlignmentThrows([scriptblock]$Action) { $failed=$false; try { & $Action | Out-Null } catch { $failed=$true }; Assert-Alignment $failed 'Se esperaba rechazo antes de modificar datos.' }
foreach ($file in Get-ChildItem (Join-Path $root 'scripts') -Recurse -Filter '*.ps1') {
    $tokens=$null; $errors=$null; [void][Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
    Assert-Alignment ($errors.Count -eq 0) ('Sintaxis invalida: '+$file.Name)
}
$fixture=Join-Path $root ('tmp\script-alignment\'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$fakeDb=Join-Path $fixture 'keep.db'; [IO.File]::WriteAllText($fakeDb,'keep')
Assert-AlignmentThrows { & (Join-Path $root 'scripts\db\Initialize-BitacoraDatabase.ps1') -DataDbPath $fakeDb -StorageRoot (Join-Path $fixture 'new-storage') -ResetDatabase }
Assert-Alignment (([IO.File]::ReadAllText($fakeDb) -ceq 'keep') -and -not (Test-Path -LiteralPath (Join-Path $fixture 'new-storage'))) 'El bloqueo debe proteger SQLite y storage antes de Reset.'
Assert-AlignmentThrows { & (Join-Path $root 'scripts\deploy\Publish-Bitacora.ps1') -DeployRoot (Join-Path $fixture 'old-deploy') }
Assert-Alignment (-not (Test-Path -LiteralPath (Join-Path $fixture 'old-deploy'))) 'El publicador SQLite no debe crear directorios en esta rama.'
foreach ($relative in @('scripts\db\Test-BitacoraLegacySchema.ps1','scripts\db\Stamp-BitacoraLegacyBaseline.ps1','scripts\storage\Migrate-LegacyStorage.ps1','scripts\validation\Run-StartupValidation.ps1')) {
    Assert-AlignmentThrows { & (Join-Path $root $relative) }
}
Assert-AlignmentThrows { & (Join-Path $root 'scripts\https\Start-BitacoraWithHttps.ps1') -ServerIp 127.0.0.1 }
Assert-AlignmentThrows { & (Join-Path $root 'scripts\security\Protect-ExistingData.ps1') -ProjectPath (Join-Path $root 'BitacoraEvidencias.Web.csproj') }
Assert-AlignmentThrows { Assert-SmokeTarget -BaseUrl https://127.0.0.1:5068 -StorageRoot $fixture }
Assert-AlignmentThrows { Assert-SmokeTarget -BaseUrl https://127.0.0.1:5067 -StorageRoot $fixture -AllowWriteTests }
Assert-AlignmentThrows { Assert-SmokeTarget -BaseUrl https://example.com:5068 -StorageRoot $fixture -AllowWriteTests }
Assert-SmokeTarget -BaseUrl http://127.0.0.1:5097 -StorageRoot $fixture -AllowWriteTests
$checks++
$previous=[Environment]::GetEnvironmentVariable('BITACORA_DATA_PROTECTION_KEY','Process')
try {
    $env:BITACORA_DATA_PROTECTION_KEY='script-alignment-fake-key-not-for-production'
    $folder=Get-SmokeOficioFolder $fixture 'OF-SYNTHETIC'
    Assert-Alignment ((Split-Path $folder -Leaf) -match '^of_[A-F0-9]{32}$') 'Debe usar nombre protegido HMAC.'
    Assert-Alignment ($folder -ceq (Get-SmokeOficioFolder $fixture 'of-synthetic')) 'El nombre protegido debe normalizar mayusculas.'
} finally { [Environment]::SetEnvironmentVariable('BITACORA_DATA_PROTECTION_KEY',$previous,'Process') }
$bytes=New-SmokeImageBytes; $stream=[IO.MemoryStream]::new($bytes)
try { $image=[Drawing.Image]::FromStream($stream); try { Assert-Alignment ($image.Width -eq 16 -and $image.Height -eq 16) 'Imagen de prueba no valida.' } finally { $image.Dispose() } } finally { $stream.Dispose() }
$private=New-SmokePrivateRoot
try { Assert-Alignment (Test-Path -LiteralPath $private -PathType Container) 'Falta staging privado.' } finally { Remove-SmokePrivateRoot $private }
Assert-Alignment (-not (Test-Path -LiteralPath $private)) 'No se limpio el staging propio.'
Assert-AlignmentThrows { Remove-SmokePrivateRoot $fixture }
Assert-AlignmentThrows { & (Join-Path $root 'scripts\deploy\Publish-BitacoraSqlRelease.ps1') -ReleaseRoot 'C:\BitacoraEvidencias\deploy\releases' }
Assert-AlignmentThrows { & (Join-Path $root 'scripts\deploy\Start-BitacoraSqlProduction.ps1') -ApplicationFolder 'C:\BitacoraEvidencias\deploy\current' }
$certDir=Join-Path $fixture 'cert'; New-Item -ItemType Directory -Path $certDir | Out-Null
[IO.File]::WriteAllText((Join-Path $certDir 'bitacora-lan.pfx'),'keep')
Assert-AlignmentThrows { & (Join-Path $root 'scripts\https\New-LanCertificate.ps1') -ServerIp 127.0.0.1 -OutputDir $certDir }
Assert-Alignment ([IO.File]::ReadAllText((Join-Path $certDir 'bitacora-lan.pfx')) -ceq 'keep') 'El certificado existente debe conservarse.'

# Load just the HTTP handler factory, without executing the smoke or any request.
$tokens=$null; $errors=$null
$smokeAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts\smoke\Run-Smoke.ps1'),[ref]$tokens,[ref]$errors)
$factory=$smokeAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'New-ClientContext' },$true)
. ([scriptblock]::Create($factory.Extent.Text))
Add-Type -AssemblyName System.Net.Http
$BaseUrl='https://127.0.0.1:5098'; $TrustLocalHttpsCertificate=$false
$client=New-ClientContext
try { Assert-Alignment ($null -eq $client.Handler.ServerCertificateCustomValidationCallback) 'HTTPS debe validar confianza por defecto.' } finally { $client.Client.Dispose() }
$TrustLocalHttpsCertificate=$true; $client=New-ClientContext
try { Assert-Alignment ($null -ne $client.Handler.ServerCertificateCustomValidationCallback) 'La excepcion HTTPS explicita no se configuro.' } finally { $client.Client.Dispose() }

# Exercise the production launcher using real binaries but mocked prompts/dotnet.
# It builds the connection string without ever opening SQL or starting a process.
$app=Join-Path $root 'bin\Release\net9.0'
if (Test-Path -LiteralPath (Join-Path $app 'BitacoraEvidencias.Web.dll')) {
    $saved=@{}
    foreach ($name in @('BITACORA_DATA_PROTECTION_KEY','BITACORA_HTTPS_CERT_PATH','BITACORA_HTTPS_CERT_PASSWORD','ConnectionStrings__DefaultConnection')) { $saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process') }
    $global:bitacoraLauncherRegression=@{ Password='fake;quote="'';equals=value'; Calls=0; Fail=$false }
    function Read-Host { param($Prompt,[switch]$AsSecureString) if ($AsSecureString) { return ConvertTo-SecureString $global:bitacoraLauncherRegression.Password -AsPlainText -Force }; return [Management.Automation.PSObject]::new('regression_user') }
    function Get-NetTCPConnection { param($LocalPort,$State,$ErrorAction) }
    function dotnet {
        param([Parameter(ValueFromRemainingArguments=$true)][object[]]$Arguments)
        $parsed=[Data.SqlClient.SqlConnectionStringBuilder]::new($env:ConnectionStrings__DefaultConnection)
        if ($parsed.InitialCatalog -cne 'BitacoraEvidencias' -or $parsed.UserID -cne 'regression_user' -or $parsed.Password -cne $global:bitacoraLauncherRegression.Password -or -not $parsed.Encrypt) { throw 'Conexion SQL productiva incorrecta.' }
        $global:bitacoraLauncherRegression.Calls++
        $global:LASTEXITCODE=if ($global:bitacoraLauncherRegression.Fail) { 1 } else { 0 }
    }
    try {
        $env:BITACORA_DATA_PROTECTION_KEY='fake-test-key'; $env:BITACORA_HTTPS_CERT_PATH=$fakeDb; $env:BITACORA_HTTPS_CERT_PASSWORD='fake-test-cert'
        & (Join-Path $root 'scripts\deploy\Start-BitacoraSqlProduction.ps1') -ApplicationFolder $app -EvidenceRoot $fixture -HttpsPort 5098
        Assert-Alignment ($global:bitacoraLauncherRegression.Calls -eq 1) 'No se ejercito el lanzador SQL.'
        Assert-Alignment ([Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection','Process') -ceq $saved.ConnectionStrings__DefaultConnection) 'El lanzador no restauro la conexion de proceso.'
        $global:bitacoraLauncherRegression.Fail=$true
        Assert-AlignmentThrows { & (Join-Path $root 'scripts\deploy\Start-BitacoraSqlProduction.ps1') -ApplicationFolder $app -EvidenceRoot $fixture -HttpsPort 5098 }
        Assert-Alignment ([Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection','Process') -ceq $saved.ConnectionStrings__DefaultConnection) 'El fallo del proceso debe restaurar variables.'
    } finally { foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') } }
}
Write-Host ('OK: '+$checks+' comprobaciones de compatibilidad y proteccion; sin SQL, tareas ni procesos de aplicacion.')

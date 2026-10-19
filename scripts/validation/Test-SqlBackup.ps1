Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $repo 'scripts\backup\SqlBackupSupport.ps1')
$checks = 0
function Assert-BackupTest {
    param([bool]$Condition,[string]$Message)
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Assert-BackupThrows {
    param([scriptblock]$Action)
    $threw = $false
    try { & $Action | Out-Null } catch { $threw = $true }
    Assert-BackupTest $threw 'Se esperaba rechazo seguro.'
}
foreach ($file in @('Backup-Bitacora.ps1','Backup-BitacoraSqlite.ps1','SqlBackupSupport.ps1','Save-BitacoraSqlBackupCredential.ps1','Initialize-BitacoraSqlBackupDirectory.ps1','Invoke-BitacoraScheduledSqlBackup.ps1','Register-BitacoraSqlBackupTask.ps1')) {
    $tokens = $null; $parseErrors = $null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $repo ('scripts\backup\' + $file)),[ref]$tokens,[ref]$parseErrors)
    Assert-BackupTest ($parseErrors.Count -eq 0) ('Sintaxis invalida: ' + $file)
}
$fakePassword = 'fake;quote="'';equals=val'
$fakeUser = [Management.Automation.PSObject]::new('regression_user')
$credential = [Management.Automation.PSCredential]::new([string]$fakeUser,(ConvertTo-SecureString $fakePassword -AsPlainText -Force))
foreach ($trust in @($false,$true)) {
    $connection = New-BitacoraBackupConnection -DatabaseName BitacoraEvidencias_Pruebas -Credential $credential -TrustLocalSqlCertificate:$trust
    try {
        $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new($connection.ConnectionString)
        Assert-BackupTest ($builder.UserID -ceq 'regression_user' -and $builder.Password -ceq $fakePassword) 'Credenciales alteradas por PowerShell.'
        Assert-BackupTest ($builder.Encrypt -and -not $builder.PersistSecurityInfo -and $builder.TrustServerCertificate -eq $trust) 'TLS o persistencia incorrectos.'
        Assert-BackupTest ($builder.DataSource -ceq '.\Auditoria' -and $builder.InitialCatalog -ceq 'BitacoraEvidencias_Pruebas') 'Destino incorrecto.'
        foreach ($operation in @('Backup','Verify','Header')) {
            $path = "C:\copias\nombre'con;caracteres.bak"
            $command = New-BitacoraBackupCommand -Connection $connection -Operation $operation -DatabaseName BitacoraEvidencias_Pruebas -BackupPath $path
            try {
                Assert-BackupTest ($command.Parameters['@path'].Value -ceq $path -and -not $command.CommandText.Contains($path)) 'La ruta debe ser un parametro SQL.'
                if ($operation -eq 'Backup') {
                    Assert-BackupTest ($command.CommandText -match 'COPY_ONLY, NO_COMPRESSION, CHECKSUM, STOP_ON_ERROR') 'Faltan opciones de respaldo nativo seguro.'
                    Assert-BackupTest ($command.CommandText -notmatch '\b(FORMAT|INIT|SKIP)\b') 'No se debe sobrescribir un respaldo existente.'
                }
                if ($operation -eq 'Verify') { Assert-BackupTest ($command.CommandText -match '^RESTORE VERIFYONLY.*CHECKSUM') 'Falta verificar el respaldo.' }
            } finally { $command.Dispose() }
        }
    } finally { $connection.Dispose() }
}
$windowsConnection = New-BitacoraBackupConnection -DatabaseName BitacoraEvidencias_Pruebas -UseWindowsAuthentication
try { Assert-BackupTest ([Data.SqlClient.SqlConnectionStringBuilder]::new($windowsConnection.ConnectionString).IntegratedSecurity) 'Autenticacion Windows incorrecta.' } finally { $windowsConnection.Dispose() }
Assert-BackupThrows { New-BitacoraBackupConnection -DatabaseName BitacoraEvidencias -UseWindowsAuthentication -Credential $credential }
Assert-BackupThrows { New-BitacoraBackupCommand -Connection ([Data.SqlClient.SqlConnection]::new()) -Operation Backup -DatabaseName 'other];DROP DATABASE x;--' }
Assert-BackupThrows { Assert-BitacoraBackupLocalPath '..\relative' }
Assert-BackupThrows { Assert-BitacoraBackupLocalPath '\\other\share' }
Assert-BackupThrows { Assert-BitacoraBackupChild 'C:\backup-other\file.zip' 'C:\backup' }
Assert-BackupThrows { Assert-BitacoraBackupChild 'C:\backup\..\outside.zip' 'C:\backup' }
Assert-BackupTest ((Assert-BitacoraBackupLocalPath 'C:\') -ceq 'C:\') 'La raiz de disco no debe convertirse en ruta relativa.'

$fixture = Join-Path $repo ('tmp\backup-tests\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$suffix = '20200101_010101_0123456789abcdef0123456789abcdef'
$wanted = 'BitacoraSqlBackup_BitacoraEvidencias_' + $suffix + '.zip'
foreach ($file in @($wanted, ('BitacoraSqlBackup_BitacoraEvidencias_Pruebas_' + $suffix + '.zip'), 'BitacoraBackup_20200101_010101.zip', ($wanted + '.partial'), 'other.zip')) {
    $created = New-Item -ItemType File -Path (Join-Path $fixture $file)
    $created.LastWriteTime = (Get-Date).AddDays(-60)
}
$expired = @(Get-BitacoraExpiredSqlBackups -Root $fixture -DatabaseName BitacoraEvidencias -Cutoff (Get-Date).AddDays(-30))
Assert-BackupTest ($expired.Count -eq 1 -and $expired[0].Name -ceq $wanted) 'La retencion debe excluir SQLite, otras bases y archivos incompletos.'
New-Item -ItemType Directory -Path (Join-Path $fixture 'evidence\sub') | Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'evidence\sub\foto[1].jpg'),'fake')
$files = @(Get-BitacoraBackupEvidenceFiles -Root (Join-Path $fixture 'evidence'))
Assert-BackupTest ($files.Count -eq 1 -and $files[0].Name -ceq 'foto[1].jpg') 'El enumerador debe manejar nombres literales.'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $fixture 'portable.zip'
Write-BitacoraBackupArchive -SourceRoot (Join-Path $fixture 'evidence') -DestinationPath $zip
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try { Assert-BackupTest ($null -ne $archive.GetEntry('sub/foto[1].jpg')) 'El ZIP debe tener rutas portables y coincidir con el manifiesto.' } finally { $archive.Dispose() }
function Get-NetTCPConnection { param($LocalPort,$State,$ErrorAction) return [pscustomobject]@{ LocalPort = $LocalPort } }
Assert-BackupThrows { Assert-BitacoraBackupStopped -Port 5068 }
Assert-BackupThrows { & (Join-Path $repo 'scripts\backup\Backup-Bitacora.ps1') -DatabaseName BitacoraEvidencias_Pruebas -BackupRoot $fixture -NonInteractive }
function Read-Host { throw 'No se debe pedir entrada durante la tarea.' }
Assert-BackupThrows { & (Join-Path $repo 'scripts\backup\Backup-Bitacora.ps1') -DatabaseName BitacoraEvidencias_Pruebas -BackupRoot $fixture -DatabaseOnly -NonInteractive -SqlCredentialFile '' }
Write-Host "OK: $checks comprobaciones; sin conexiones SQL, sin borrar respaldos ni modificar produccion."

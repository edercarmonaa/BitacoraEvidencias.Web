Set-StrictMode -Version Latest

function New-BitacoraBackupConnection {
    param(
        [ValidateSet('BitacoraEvidencias','BitacoraEvidencias_Pruebas')][string]$DatabaseName,
        [Management.Automation.PSCredential]$Credential,
        [switch]$UseWindowsAuthentication,
        [switch]$TrustLocalSqlCertificate
    )
    $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder.set_DataSource('.\Auditoria')
    $builder.set_InitialCatalog($DatabaseName)
    $builder.set_Encrypt($true)
    $builder.set_TrustServerCertificate([bool]$TrustLocalSqlCertificate)
    $builder.set_PersistSecurityInfo($false)
    $builder.set_ConnectTimeout(15)
    $builder.set_ApplicationName('BitacoraBackup-0.2.0')
    $builder.set_Pooling($false)
    if ($UseWindowsAuthentication) {
        if ($null -ne $Credential) { throw 'Selecciona un solo tipo de autenticacion.' }
        $builder.set_IntegratedSecurity($true)
    } else {
        if ($null -eq $Credential) { throw 'Falta la credencial SQL de respaldo.' }
        $builder.set_UserID([string]$Credential.UserName)
        $builder.set_Password($Credential.GetNetworkCredential().Password)
    }
    return [Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
}

function New-BitacoraBackupCommand {
    param(
        [Data.SqlClient.SqlConnection]$Connection,
        [ValidateSet('Backup','Verify','Header','DefaultPath','Ready')][string]$Operation,
        [ValidateSet('BitacoraEvidencias','BitacoraEvidencias_Pruebas')][string]$DatabaseName,
        [string]$BackupPath,
        [int]$TimeoutSeconds = 600
    )
    $command = $Connection.CreateCommand()
    $command.CommandTimeout = $TimeoutSeconds
    switch ($Operation) {
        'Backup' { $command.CommandText = "BACKUP DATABASE [$DatabaseName] TO DISK = @path WITH COPY_ONLY, NO_COMPRESSION, CHECKSUM, STOP_ON_ERROR;" }
        'Verify' { $command.CommandText = 'RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM, STOP_ON_ERROR;' }
        'Header' { $command.CommandText = 'RESTORE HEADERONLY FROM DISK = @path;' }
        'DefaultPath' { $command.CommandText = "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'));" }
        'Ready' { $command.CommandText = "IF OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NULL SELECT 0; ELSE SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%[_]InitialSqlServer';" }
    }
    if ($Operation -in @('Backup','Verify','Header')) {
        $parameter = $command.Parameters.Add('@path', [Data.SqlDbType]::NVarChar, 4000)
        $parameter.Value = [string]$BackupPath
    }
    return $command
}

function Assert-BitacoraBackupLocalPath {
    param([string]$Path)
    if ($Path -notmatch '^[A-Za-z]:[\\/]' -or $Path.IndexOfAny([char[]]'*?') -ge 0) {
        throw 'Usa una ruta absoluta de disco local, sin comodines.'
    }
    return [IO.Path]::GetFullPath($Path)
}

function Assert-BitacoraBackupChild {
    param([string]$Path, [string]$Root)
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {
        throw 'La ruta calculada sale del directorio permitido.'
    }
    return $full
}

function Get-BitacoraBackupEvidenceFiles {
    param([string]$Root)
    $rootInfo = Get-Item -LiteralPath $Root
    $pending = [Collections.Generic.Stack[IO.DirectoryInfo]]::new()
    $pending.Push($rootInfo)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'No se permiten enlaces o junctions en evidencias.' }
        foreach ($item in $directory.EnumerateFileSystemInfos()) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'No se permiten enlaces o junctions en evidencias.' }
            if ($item -is [IO.DirectoryInfo]) { $pending.Push($item) } else { $item }
        }
    }
}

function Assert-BitacoraBackupStopped {
    param([int]$Port)
    if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) {
        throw 'La aplicacion sigue escuchando. Deten las escrituras antes del respaldo completo, o usa -DatabaseOnly para respaldar solo SQL en linea.'
    }
}

function Get-BitacoraExpiredSqlBackups {
    param([string]$Root, [string]$DatabaseName, [datetime]$Cutoff)
    # Only this database/provider's finalized files; never legacy or partial backups.
    $pattern = '^BitacoraSqlBackup_' + [regex]::Escape($DatabaseName) + '_[0-9]{8}_[0-9]{6}_[a-f0-9]{32}\.zip$'
    Get-ChildItem -LiteralPath $Root -File | Where-Object {
        $_.Name -match $pattern -and $_.LastWriteTime -lt $Cutoff -and
        -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint)
    }
}

function Write-BitacoraBackupArchive {
    param([string]$SourceRoot, [string]$DestinationPath)
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # Windows PowerShell/.NET Framework CreateFromDirectory writes backslashes.
    # Generate portable entry names explicitly, streaming files with Zip64 support.
    $archive = [IO.Compression.ZipFile]::Open($DestinationPath,[IO.Compression.ZipArchiveMode]::Create)
    try {
        $prefixLength = $SourceRoot.TrimEnd('\').Length + 1
        foreach ($directory in [IO.Directory]::GetDirectories($SourceRoot,'*',[IO.SearchOption]::AllDirectories)) {
            [void]$archive.CreateEntry($directory.Substring($prefixLength).Replace('\','/') + '/')
        }
        foreach ($file in [IO.Directory]::GetFiles($SourceRoot,'*',[IO.SearchOption]::AllDirectories)) {
            $relative = $file.Substring($prefixLength).Replace('\','/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file,$relative,[IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $archive.Dispose() }
}

function New-BitacoraBackupPrivateStaging {
    param([string]$Path)
    # A short path avoids PowerShell 5.1's MAX_PATH limit for nested evidence files.
    New-Item -ItemType Directory -Path $Path | Out-Null
    $acl = Get-Acl -LiteralPath $Path
    $acl.SetAccessRuleProtection($true,$false)
    $inherit = [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
    foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User,
        [Security.Principal.SecurityIdentifier]::new('S-1-5-18'),[Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::FullControl,
            $inherit,[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow))
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

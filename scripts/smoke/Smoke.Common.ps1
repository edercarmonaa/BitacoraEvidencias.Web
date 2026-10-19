function Assert-SmokeTarget {
    param([string]$BaseUrl,[string]$StorageRoot,[switch]$AllowWriteTests)
    $uri = $null
    if (-not $AllowWriteTests) { throw 'Estas pruebas crean/modifican/eliminan registros y cuentas. Usar -AllowWriteTests solo en una copia de ensayo.' }
    if (-not [uri]::TryCreate($BaseUrl,[UriKind]::Absolute,[ref]$uri) -or -not $uri.IsLoopback -or
        $uri.Port -eq 5067 -or $uri.Scheme -notin @('http','https') -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/') {
        throw 'El smoke requiere URL local de ensayo, sin credenciales/parametros y distinta del puerto productivo 5067.'
    }
    $storage = [IO.Path]::GetFullPath($StorageRoot)
    if (-not [IO.Path]::IsPathRooted($StorageRoot) -or $storage.TrimEnd('\') -eq 'C:\BitacoraEvidencias' -or
        $storage.StartsWith('C:\BitacoraEvidencias\',[StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $storage -PathType Container)) {
        throw 'Usar una carpeta de evidencias existente de ensayo, fuera de produccion.'
    }
}

function Get-SmokeBusinessDate { return [TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([datetime]::UtcNow,'Central Standard Time (Mexico)').ToString('yyyy-MM-dd') }

function New-SmokeImageBytes {
    Add-Type -AssemblyName System.Drawing
    $image=[Drawing.Bitmap]::new(16,16); $stream=[IO.MemoryStream]::new()
    try { $image.SetPixel(0,0,[Drawing.Color]::Blue); $image.Save($stream,[Drawing.Imaging.ImageFormat]::Png); return ,$stream.ToArray() } finally { $image.Dispose(); $stream.Dispose() }
}

function Get-SmokeOficioFolder {
    param([string]$StorageRoot,[string]$NumeroOficio)
    $raw = $env:BITACORA_DATA_PROTECTION_KEY
    if (-not $raw) { throw 'El smoke SQL requiere la misma llave del proceso de ensayo, sin mostrarla.' }
    try { $master = [Convert]::FromBase64String($raw.Trim()) } catch { $master = [Text.Encoding]::UTF8.GetBytes($raw.Trim()) }
    if ($master.Length -lt 32) { $sha=[Security.Cryptography.SHA256]::Create(); try { $master=$sha.ComputeHash($master) } finally { $sha.Dispose() } }
    $hmac = [Security.Cryptography.HMACSHA256]::new([byte[]]$master)
    try { $indexKey = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes('bitacora:blind-index:v1')) } finally { $hmac.Dispose() }
    $hmac = [Security.Cryptography.HMACSHA256]::new([byte[]]$indexKey)
    try { $hash = [BitConverter]::ToString($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($NumeroOficio.Trim().ToUpperInvariant()))).Replace('-','') } finally { $hmac.Dispose() }
    return Join-Path $StorageRoot ('of_' + $hash.Substring(0,32))
}

function New-SmokePrivateRoot {
    . (Join-Path $PSScriptRoot '..\backup\SqlBackupSupport.ps1')
    $path=Join-Path ([IO.Path]::GetTempPath()) ('BitacoraSmoke_' + [guid]::NewGuid().ToString('N'))
    New-BitacoraBackupPrivateStaging -Path $path
    return $path
}

function Remove-SmokePrivateRoot([string]$Path) {
    $full=[IO.Path]::GetFullPath($Path); $parent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $full.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($full) -notmatch '^BitacoraSmoke_[a-f0-9]{32}$') { throw 'No se limpia una ruta ajena al smoke.' }
    if (Test-Path -LiteralPath $full) {
        if (@(Get-ChildItem -LiteralPath $full -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0 -or ((Get-Item -LiteralPath $full).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'No se limpian enlaces en el smoke.' }
        Remove-Item -LiteralPath $full -Recurse -Force
    }
}

function Resolve-SmokeAdminPassword {
    param([string]$AdminPass)

    if ([string]::IsNullOrWhiteSpace($AdminPass)) {
        $AdminPass = [Environment]::GetEnvironmentVariable('BITACORA_SMOKE_ADMIN_PASSWORD', 'Process')
    }

    if ([string]::IsNullOrWhiteSpace($AdminPass)) {
        $AdminPass = [Environment]::GetEnvironmentVariable('BITACORA_SMOKE_ADMIN_PASSWORD', 'User')
    }

    if ([string]::IsNullOrWhiteSpace($AdminPass)) {
        throw 'Debes indicar -AdminPass o definir BITACORA_SMOKE_ADMIN_PASSWORD. Ya no existe una contrasena admin por defecto en el bootstrap.'
    }

    return $AdminPass
}

function Get-SmokeToken {
    param([string]$Html)

    $match = [regex]::Match($Html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"', 'IgnoreCase')
    if ($match.Success) {
        return $match.Groups[1].Value
    }

    return $null
}

function Get-SmokeUserIdFromUsersHtml {
    param(
        [string]$Html,
        [string]$Username
    )

    $escapedUsername = [regex]::Escape($Username)
    $pattern = "<tr>.*?<div class=""fw-semibold"">$escapedUsername</div>.*?<input type=""hidden"" name=""id"" value=""(\d+)"""
    $match = [regex]::Match($Html, $pattern, 'IgnoreCase,Singleline')
    if ($match.Success) {
        return [int]$match.Groups[1].Value
    }

    return $null
}

function Get-SmokeHandlerPathFromHtml {
    param(
        [string]$Html,
        [string]$HandlerName
    )

    $escapedHandler = [regex]::Escape($HandlerName)
    $patterns = @(
        "hx-post=""([^""]*handler=$escapedHandler[^""]*)""",
        "action=""([^""]*handler=$escapedHandler[^""]*)"""
    )

    foreach ($pattern in $patterns) {
        $match = [regex]::Match($Html, $pattern, 'IgnoreCase')
        if ($match.Success) {
            return $match.Groups[1].Value.Replace('&amp;', '&')
        }
    }

    return $null
}

function Write-SmokeReport {
    param([System.Collections.Generic.List[object]]$Results)

    $timestamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '_' + [guid]::NewGuid().ToString('N')
    $reportRoot = Join-Path $PSScriptRoot '..\..\tmp\smoke-reports'
    New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
    $reportPath = Join-Path $reportRoot "SMOKE-RESULTS-$timestamp.md"
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# Smoke Test Results')
    $lines.Add('')
    $lines.Add("Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    $lines.Add('')
    $lines.Add('| Block | Check | Status | Detail |')
    $lines.Add('|---|---|---|---|')

    foreach ($result in $Results) {
        $detail = ([string]$result.Detail -replace '\|', '/')
        $lines.Add("| $($result.Block) | $($result.Check) | $($result.Status) | $detail |")
    }

    [System.IO.File]::WriteAllLines($reportPath, $lines)
    return $reportPath
}

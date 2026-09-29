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

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $reportPath = Join-Path (Get-Location) "docs/SMOKE-RESULTS-$timestamp.md"
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

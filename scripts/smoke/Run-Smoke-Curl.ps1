param(
    [string]$BaseUrl = 'https://127.0.0.1:5067',
    [string]$AdminUser = 'admin',
    [string]$AdminPass = '',
    [string]$StorageRoot = 'Storage'
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Smoke.Common.ps1')

$AdminPass = Resolve-SmokeAdminPassword -AdminPass $AdminPass

$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param([string]$Block,[string]$Check,[string]$Status,[string]$Detail)
    $results.Add([pscustomobject]@{ Block=$Block; Check=$Check; Status=$Status; Detail=$Detail })
}

function New-CookieFile {
    $p = Join-Path $env:TEMP ("smoke-cookies-" + [guid]::NewGuid().ToString('N') + '.txt')
    New-Item -Path $p -ItemType File -Force | Out-Null
    return $p
}

function ConvertTo-FormEncoded {
    param([hashtable]$Fields)
    $parts = @()
    foreach ($k in $Fields.Keys) {
        $ek = [Uri]::EscapeDataString([string]$k)
        $ev = [Uri]::EscapeDataString([string]$Fields[$k])
        $parts += "$ek=$ev"
    }
    return ($parts -join '&')
}

function Parse-CurlHeaders {
    param([string]$HeaderText)
    $codes = [regex]::Matches($HeaderText, 'HTTP/\d(?:\.\d)?\s+(\d{3})')
    $code = if ($codes.Count -gt 0) { [int]$codes[$codes.Count-1].Groups[1].Value } else { 0 }
    $locs = [regex]::Matches($HeaderText, '(?im)^Location:\s*(.+)$')
    $loc = if ($locs.Count -gt 0) { $locs[$locs.Count-1].Groups[1].Value.Trim() } else { '' }
    $cts = [regex]::Matches($HeaderText, '(?im)^Content-Type:\s*(.+)$')
    $ct = if ($cts.Count -gt 0) { $cts[$cts.Count-1].Groups[1].Value.Trim() } else { '' }
    return [pscustomobject]@{ Code=$code; Location=$loc; ContentType=$ct }
}

function Invoke-Curl {
    param(
        [string]$Method,
        [string]$Path,
        [string]$CookieFile,
        [hashtable]$FormFields,
        [string[]]$MultipartFields
    )

    $headersPath = Join-Path $env:TEMP ("smoke-h-" + [guid]::NewGuid().ToString('N') + '.txt')
    $bodyPath = Join-Path $env:TEMP ("smoke-b-" + [guid]::NewGuid().ToString('N') + '.txt')

    $args = @('-k','-sS','-D',$headersPath,'-o',$bodyPath,'-c',$CookieFile,'-b',$CookieFile)

    if ($Method -eq 'POST') {
        $args += @('-X','POST')
        if ($MultipartFields -and $MultipartFields.Count -gt 0) {
            foreach ($f in $MultipartFields) { $args += @('-F', $f) }
        } elseif ($FormFields) {
            $encoded = ConvertTo-FormEncoded $FormFields
            $args += @('--data-raw', $encoded)
        }
    }

    $url = if ($Path.StartsWith('http')) { $Path } else { "$BaseUrl$Path" }
    $args += $url

    & curl.exe @args | Out-Null
    $exit = $LASTEXITCODE
    if ($exit -ne 0) {
        throw "curl failed (exit=$exit) for $Method $url"
    }

    $headerText = Get-Content -Path $headersPath -Raw -Encoding UTF8
    $bodyText = Get-Content -Path $bodyPath -Raw -Encoding UTF8

    Remove-Item -Path $headersPath,$bodyPath -Force -ErrorAction SilentlyContinue

    $meta = Parse-CurlHeaders $headerText
    return [pscustomobject]@{
        Code = $meta.Code
        Location = $meta.Location
        ContentType = $meta.ContentType
        Body = $bodyText
    }
}

function Invoke-CurlMultipartDirect {
    param(
        [string]$Path,
        [string]$CookieFile,
        [string[]]$MultipartFields
    )

    $headersPath = Join-Path $env:TEMP ("smoke-mh-" + [guid]::NewGuid().ToString('N') + '.txt')
    $bodyPath = Join-Path $env:TEMP ("smoke-mb-" + [guid]::NewGuid().ToString('N') + '.txt')
    $url = if ($Path.StartsWith('http')) { $Path } else { "$BaseUrl$Path" }

    $args = @('-k','-sS','-D',$headersPath,'-o',$bodyPath,'-c',$CookieFile,'-b',$CookieFile,'-X','POST')
    foreach ($f in $MultipartFields) {
        $args += @('-F', $f)
    }
    $args += $url

    & curl.exe @args | Out-Null
    $exit = $LASTEXITCODE
    if ($exit -ne 0) {
        throw "curl multipart failed (exit=$exit) for $url"
    }

    $headerText = Get-Content -Path $headersPath -Raw -Encoding UTF8
    $bodyText = Get-Content -Path $bodyPath -Raw -Encoding UTF8
    Remove-Item -Path $headersPath,$bodyPath -Force -ErrorAction SilentlyContinue

    $meta = Parse-CurlHeaders $headerText
    return [pscustomobject]@{
        Code = $meta.Code
        Location = $meta.Location
        ContentType = $meta.ContentType
        Body = $bodyText
    }
}

$adminCookie = New-CookieFile
$anonCookie = New-CookieFile
$smokeCookie = New-CookieFile
$roleRevocationCookie = New-CookieFile
$smokeUsername = 'smoke.lock.' + (Get-Date -Format 'yyyyMMddHHmmss')
$tempPassword = $null
$roleRevocationUsername = 'smoke.role.' + (Get-Date -Format 'yyyyMMddHHmmss')
$roleRevocationTempPassword = $null
$roleRevocationNewPassword = 'RoleRevoc!' + (Get-Date -Format 'yyyyMMddHHmmss')
$roleRevocationUserId = $null
$activeAdminPass = $AdminPass
$smokeAdminChangedPass = 'SmokeAdmin!' + (Get-Date -Format 'yyyyMMddHHmmss')
$smokeOficioNumber = 'SMOKE-OF-' + (Get-Date -Format 'yyyyMMddHHmmss')
$smokeOficioId = $null
$smokeCaseId = $null
$smokeEvidenceId = $null
$smokeEvidenceFilePath = $null
$today = (Get-Date).ToString('yyyy-MM-dd')

try {
    # A1 anonymous redirect
    $rAnon = Invoke-Curl -Method 'GET' -Path '/Oficios' -CookieFile $anonCookie
    if ($rAnon.Code -eq 302 -and $rAnon.Location -like '*Login*') {
        Add-Result 'A' 'Anonymous access redirected' 'PASS' "HTTP 302 => $($rAnon.Location)"
    } else {
        Add-Result 'A' 'Anonymous access redirected' 'FAIL' "HTTP $($rAnon.Code) => $($rAnon.Location)"
    }

    # A2 admin login
    $loginGet = Invoke-Curl -Method 'GET' -Path '/Login' -CookieFile $adminCookie
    $loginToken = Get-SmokeToken $loginGet.Body
    if ([string]::IsNullOrWhiteSpace($loginToken)) { throw 'Login token not found.' }

    $loginPost = Invoke-Curl -Method 'POST' -Path '/Login' -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $loginToken
        'Input.Usuario' = $AdminUser
        'Input.Contrasena' = $AdminPass
    }

    if ($loginPost.Code -eq 302 -and $loginPost.Location -like '*ChangePassword*') {
        Add-Result 'A' 'Admin login' 'PASS' "HTTP 302 => $($loginPost.Location) (password change required)"

        $adminChangeGet = Invoke-Curl -Method 'GET' -Path $loginPost.Location -CookieFile $adminCookie
        $adminChangeToken = Get-SmokeToken $adminChangeGet.Body
        if ([string]::IsNullOrWhiteSpace($adminChangeToken)) { throw 'Admin change-password token not found.' }

        $adminChangePost = Invoke-Curl -Method 'POST' -Path $loginPost.Location -CookieFile $adminCookie -FormFields @{
            '__RequestVerificationToken' = $adminChangeToken
            'Input.CurrentPassword' = $AdminPass
            'Input.NewPassword' = $smokeAdminChangedPass
            'Input.ConfirmPassword' = $smokeAdminChangedPass
        }

        if ($adminChangePost.Code -eq 302 -and $adminChangePost.Location -notlike '*ChangePassword*') {
            $activeAdminPass = $smokeAdminChangedPass
            Add-Result 'A' 'Admin changed temporary password' 'PASS' "HTTP 302 => $($adminChangePost.Location)"
        } else {
            Add-Result 'A' 'Admin changed temporary password' 'FAIL' "HTTP $($adminChangePost.Code) => $($adminChangePost.Location)"
            throw 'Admin could not complete forced password change.'
        }
    } elseif ($loginPost.Code -eq 302 -and $loginPost.Location -notlike '*Login*') {
        Add-Result 'A' 'Admin login' 'PASS' "HTTP 302 => $($loginPost.Location)"
    } else {
        Add-Result 'A' 'Admin login' 'FAIL' "HTTP $($loginPost.Code) => $($loginPost.Location)"
        throw 'Admin login failed.'
    }

    $usersPage = Invoke-Curl -Method 'GET' -Path '/Admin/Users' -CookieFile $adminCookie
    if ($usersPage.Code -eq 200 -and $usersPage.Body -like '*Usuarios del sistema*') {
        Add-Result 'A' 'Admin authorization /Admin/Users' 'PASS' 'Users page accessible.'
    } else {
        Add-Result 'A' 'Admin authorization /Admin/Users' 'FAIL' "HTTP $($usersPage.Code)"
    }

    # B1 create smoke user
    $usersToken = Get-SmokeToken $usersPage.Body
    if ([string]::IsNullOrWhiteSpace($usersToken)) { throw 'Users token not found.' }

    $createUser = Invoke-Curl -Method 'POST' -Path '/Admin/Users?handler=Create' -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $usersToken
        'NewUser.Username' = $smokeUsername
        'NewUser.Role' = 'Capturista'
    }

    if ($createUser.Code -eq 302) {
        Add-Result 'B' 'Create lockout test user' 'PASS' "$smokeUsername"
    } else {
        Add-Result 'B' 'Create lockout test user' 'FAIL' "HTTP $($createUser.Code)"
        throw 'Cannot run lockout checks without smoke user.'
    }

    $usersAfterCreate = Invoke-Curl -Method 'GET' -Path '/Admin/Users' -CookieFile $adminCookie
    $pwMatch = [regex]::Match($usersAfterCreate.Body, 'temporal:\s*([^<\r\n]+)', 'IgnoreCase')
    if ($pwMatch.Success) {
        $tempPassword = $pwMatch.Groups[1].Value.Trim()
        Add-Result 'B' 'Temporary password extracted' 'PASS' 'Parsed from success alert.'
    } else {
        Add-Result 'B' 'Temporary password extracted' 'FAIL' 'Could not parse temporary password.'
    }

    # B2 fail attempts x5
    for ($i = 1; $i -le 5; $i++) {
        $lg = Invoke-Curl -Method 'GET' -Path '/Login' -CookieFile $smokeCookie
        $tk = Get-SmokeToken $lg.Body
        if ([string]::IsNullOrWhiteSpace($tk)) { throw "Attempt ${i}: missing login token" }

        $lp = Invoke-Curl -Method 'POST' -Path '/Login' -CookieFile $smokeCookie -FormFields @{
            '__RequestVerificationToken' = $tk
            'Input.Usuario' = $smokeUsername
            'Input.Contrasena' = 'WrongPass!123'
        }

        if ($lp.Body -like '*incorrect*' -or $lp.Body -like '*bloqueada*') {
            Add-Result 'B' "Failed login attempt $i" 'PASS' 'Rejected as expected.'
        } else {
            Add-Result 'B' "Failed login attempt $i" 'FAIL' "HTTP $($lp.Code)"
        }
    }

    # B3 sixth with correct temp password should stay locked
    if (-not [string]::IsNullOrWhiteSpace($tempPassword)) {
        $lg2 = Invoke-Curl -Method 'GET' -Path '/Login' -CookieFile $smokeCookie
        $tk2 = Get-SmokeToken $lg2.Body
        $lp2 = Invoke-Curl -Method 'POST' -Path '/Login' -CookieFile $smokeCookie -FormFields @{
            '__RequestVerificationToken' = $tk2
            'Input.Usuario' = $smokeUsername
            'Input.Contrasena' = $tempPassword
        }

        if ($lp2.Body -like '*Cuenta bloqueada*' -or $lp2.Body -like '*bloqueada*') {
            Add-Result 'B' 'Lockout after 5 failures' 'PASS' 'Still blocked with correct temporary password.'
        } else {
            Add-Result 'B' 'Lockout after 5 failures' 'FAIL' "HTTP $($lp2.Code)"
        }
    } else {
        Add-Result 'B' 'Lockout after 5 failures' 'FAIL' 'Skipped: temp password unavailable.'
    }

    # B2 role revocation should affect active sessions immediately
    $usersToken = Get-SmokeToken $usersAfterCreate.Body
    if ([string]::IsNullOrWhiteSpace($usersToken)) { throw 'Users token not found for role revocation test.' }

    $createRoleUser = Invoke-Curl -Method 'POST' -Path '/Admin/Users?handler=Create' -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $usersToken
        'NewUser.Username' = $roleRevocationUsername
        'NewUser.Role' = 'Admin'
    }

    if ($createRoleUser.Code -eq 302) {
        Add-Result 'B2' 'Create role-revocation admin user' 'PASS' "HTTP 302 => $($createRoleUser.Location) ($roleRevocationUsername)"
    } else {
        Add-Result 'B2' 'Create role-revocation admin user' 'FAIL' "HTTP $($createRoleUser.Code)"
        throw 'Cannot run role revocation checks without temp admin user.'
    }

    $usersAfterRoleCreate = Invoke-Curl -Method 'GET' -Path '/Admin/Users' -CookieFile $adminCookie
    $rolePasswordMatch = [regex]::Match($usersAfterRoleCreate.Body, 'temporal:\s*([^<\r\n]+)', 'IgnoreCase')
    if ($rolePasswordMatch.Success) {
        $roleRevocationTempPassword = $rolePasswordMatch.Groups[1].Value.Trim()
        Add-Result 'B2' 'Temporary password for temp admin extracted' 'PASS' 'Password extracted from success message.'
    } else {
        Add-Result 'B2' 'Temporary password for temp admin extracted' 'FAIL' 'Could not parse temporary password from Users page.'
        throw 'Cannot continue role revocation test without temp admin password.'
    }

    $roleRevocationUserId = Get-SmokeUserIdFromUsersHtml -Html $usersAfterRoleCreate.Body -Username $roleRevocationUsername
    if ($null -ne $roleRevocationUserId) {
        Add-Result 'B2' 'Temp admin user id detected' 'PASS' "UserId=$roleRevocationUserId"
    } else {
        Add-Result 'B2' 'Temp admin user id detected' 'FAIL' 'Could not parse temp admin user id from Users page.'
        throw 'Cannot continue role revocation test without temp admin user id.'
    }

    $roleLoginGet = Invoke-Curl -Method 'GET' -Path '/Login' -CookieFile $roleRevocationCookie
    $roleLoginToken = Get-SmokeToken $roleLoginGet.Body
    if ([string]::IsNullOrWhiteSpace($roleLoginToken)) { throw 'Temp admin login token not found.' }

    $roleLoginPost = Invoke-Curl -Method 'POST' -Path '/Login' -CookieFile $roleRevocationCookie -FormFields @{
        '__RequestVerificationToken' = $roleLoginToken
        'Input.Usuario' = $roleRevocationUsername
        'Input.Contrasena' = $roleRevocationTempPassword
    }

    if ($roleLoginPost.Code -eq 302 -and $roleLoginPost.Location -like '*ChangePassword*') {
        Add-Result 'B2' 'Temp admin redirected to change password' 'PASS' "HTTP 302 => $($roleLoginPost.Location)"
    } else {
        Add-Result 'B2' 'Temp admin redirected to change password' 'FAIL' "HTTP $($roleLoginPost.Code) => $($roleLoginPost.Location)"
        throw 'Temp admin login did not force password change.'
    }

    $roleChangeGet = Invoke-Curl -Method 'GET' -Path $roleLoginPost.Location -CookieFile $roleRevocationCookie
    $roleChangeToken = Get-SmokeToken $roleChangeGet.Body
    if ([string]::IsNullOrWhiteSpace($roleChangeToken)) { throw 'Temp admin change-password token not found.' }

    $roleChangePost = Invoke-Curl -Method 'POST' -Path $roleLoginPost.Location -CookieFile $roleRevocationCookie -FormFields @{
        '__RequestVerificationToken' = $roleChangeToken
        'Input.CurrentPassword' = $roleRevocationTempPassword
        'Input.NewPassword' = $roleRevocationNewPassword
        'Input.ConfirmPassword' = $roleRevocationNewPassword
    }

    if ($roleChangePost.Code -eq 302 -and $roleChangePost.Location -notlike '*ChangePassword*') {
        Add-Result 'B2' 'Temp admin changed password' 'PASS' "HTTP 302 => $($roleChangePost.Location)"
    } else {
        Add-Result 'B2' 'Temp admin changed password' 'FAIL' "HTTP $($roleChangePost.Code) => $($roleChangePost.Location)"
        throw 'Temp admin could not finish password change.'
    }

    $tempAdminUsers = Invoke-Curl -Method 'GET' -Path '/Admin/Users' -CookieFile $roleRevocationCookie
    if ($tempAdminUsers.Code -eq 200 -and $tempAdminUsers.Body -like '*Usuarios del sistema*') {
        Add-Result 'B2' 'Temp admin has admin access before demotion' 'PASS' 'Users page accessible.'
    } else {
        Add-Result 'B2' 'Temp admin has admin access before demotion' 'FAIL' "HTTP $($tempAdminUsers.Code)"
        throw 'Temp admin never reached admin area before demotion.'
    }

    $usersForDemotion = Invoke-Curl -Method 'GET' -Path '/Admin/Users' -CookieFile $adminCookie
    $usersDemotionToken = Get-SmokeToken $usersForDemotion.Body
    if ([string]::IsNullOrWhiteSpace($usersDemotionToken)) { throw 'Demotion token not found.' }

    $demotionPost = Invoke-Curl -Method 'POST' -Path '/Admin/Users?handler=UpdateRole' -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $usersDemotionToken
        'id' = [string]$roleRevocationUserId
        'role' = 'Capturista'
    }

    if ($demotionPost.Code -eq 302) {
        Add-Result 'B2' 'Admin demoted temp admin to Capturista' 'PASS' "HTTP 302 => $($demotionPost.Location)"
    } else {
        Add-Result 'B2' 'Admin demoted temp admin to Capturista' 'FAIL' "HTTP $($demotionPost.Code)"
        throw 'Could not demote temp admin.'
    }

    $demotedAdmin = Invoke-Curl -Method 'GET' -Path '/Admin/Users' -CookieFile $roleRevocationCookie
    if ($demotedAdmin.Code -eq 302 -and $demotedAdmin.Location -like '*Error*') {
        Add-Result 'B2' 'Demoted session loses admin access immediately' 'PASS' "HTTP 302 => $($demotedAdmin.Location)"
    } else {
        Add-Result 'B2' 'Demoted session loses admin access immediately' 'FAIL' "HTTP $($demotedAdmin.Code) => $($demotedAdmin.Location)"
        throw 'Demoted session kept admin access.'
    }

    $demotedRegular = Invoke-Curl -Method 'GET' -Path '/Oficios' -CookieFile $roleRevocationCookie
    if ($demotedRegular.Code -eq 200) {
        Add-Result 'B2' 'Demoted session remains authenticated as app user' 'PASS' 'HTTP 200'
    } else {
        Add-Result 'B2' 'Demoted session remains authenticated as app user' 'FAIL' "HTTP $($demotedRegular.Code) => $($demotedRegular.Location)"
    }

    $usersForCleanup = Invoke-Curl -Method 'GET' -Path '/Admin/Users' -CookieFile $adminCookie
    $usersCleanupToken = Get-SmokeToken $usersForCleanup.Body
    if ([string]::IsNullOrWhiteSpace($usersCleanupToken)) { throw 'Cleanup token not found.' }

    $cleanupPost = Invoke-Curl -Method 'POST' -Path '/Admin/Users?handler=ToggleActive' -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $usersCleanupToken
        'id' = [string]$roleRevocationUserId
    }

    if ($cleanupPost.Code -eq 302) {
        Add-Result 'B2' 'Temp admin user deactivated after test' 'PASS' "HTTP 302 => $($cleanupPost.Location)"
    } else {
        Add-Result 'B2' 'Temp admin user deactivated after test' 'FAIL' "HTTP $($cleanupPost.Code)"
    }

    # C1 create oficio
    $ofCreateGet = Invoke-Curl -Method 'GET' -Path '/Oficios/Create' -CookieFile $adminCookie
    $ofToken = Get-SmokeToken $ofCreateGet.Body
    if ([string]::IsNullOrWhiteSpace($ofToken)) { throw 'Oficios/Create token not found.' }

    $ofCreatePost = Invoke-Curl -Method 'POST' -Path '/Oficios/Create' -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $ofToken
        'Input.NumeroOficio' = $smokeOficioNumber
        'Input.FechaOficio' = $today
        'Input.Asunto' = 'SMOKE TEST OFICIO'
        'Input.Notas' = 'SMOKE NOTES'
    }

    if ($ofCreatePost.Code -eq 302) {
        $mOf = [regex]::Match($ofCreatePost.Location, 'id=(\d+)|/Oficios/Details/(\d+)', 'IgnoreCase')
        if ($mOf.Success) {
            $smokeOficioId = if ($mOf.Groups[1].Success) { [int]$mOf.Groups[1].Value } else { [int]$mOf.Groups[2].Value }
            Add-Result 'C' 'Create oficio' 'PASS' "OficioId=$smokeOficioId"
        } else {
            Add-Result 'C' 'Create oficio' 'FAIL' "Redirect=$($ofCreatePost.Location)"
            throw 'Oficio id parse failed.'
        }
    } else {
        Add-Result 'C' 'Create oficio' 'FAIL' "HTTP $($ofCreatePost.Code)"
        throw 'Oficio create failed.'
    }

    # C2 update oficio
    $ofEditGet = Invoke-Curl -Method 'GET' -Path "/Oficios/Edit/$smokeOficioId" -CookieFile $adminCookie
    $ofEditToken = Get-SmokeToken $ofEditGet.Body
    if ([string]::IsNullOrWhiteSpace($ofEditToken)) { throw 'Oficios/Edit token not found.' }

    $smokeOficioNumber = $smokeOficioNumber + '-UPD'
    $ofEditPost = Invoke-Curl -Method 'POST' -Path "/Oficios/Edit/$smokeOficioId" -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $ofEditToken
        'Input.Id' = [string]$smokeOficioId
        'Input.NumeroOficio' = $smokeOficioNumber
        'Input.FechaOficio' = $today
        'Input.Asunto' = 'SMOKE TEST OFICIO UPDATED'
        'Input.Notas' = 'SMOKE NOTES UPDATED'
    }
    if ($ofEditPost.Code -eq 302) {
        Add-Result 'C' 'Update oficio' 'PASS' "Redirect=$($ofEditPost.Location)"
    } else {
        Add-Result 'C' 'Update oficio' 'FAIL' "HTTP $($ofEditPost.Code)"
    }

    # C3 create caso
    $caseCreateGet = Invoke-Curl -Method 'GET' -Path "/Casos/Create?oficioId=$smokeOficioId" -CookieFile $adminCookie
    $caseToken = Get-SmokeToken $caseCreateGet.Body
    if ([string]::IsNullOrWhiteSpace($caseToken)) { throw 'Casos/Create token not found.' }

    $caseCreatePost = Invoke-Curl -Method 'POST' -Path "/Casos/Create?oficioId=$smokeOficioId" -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $caseToken
        'Input.OficioId' = [string]$smokeOficioId
        'Input.Curp' = 'SMOK900101HDFABC01'
        'Input.NombreCompleto' = 'USUARIO SMOKE'
        'Input.Cct' = '30ABC0001X'
        'Input.TipoCorreccion' = 'CORRECCION DE CURP'
        'Input.Folio' = 'SMOKE-FOLIO-1'
        'Input.Estatus' = 'Pendiente'
        'Input.Observaciones' = 'SMOKE CASE'
    }

    if ($caseCreatePost.Code -eq 302) {
        $mCase = [regex]::Match($caseCreatePost.Location, 'id=(\d+)|/Casos/Details/(\d+)', 'IgnoreCase')
        if ($mCase.Success) {
            $smokeCaseId = if ($mCase.Groups[1].Success) { [int]$mCase.Groups[1].Value } else { [int]$mCase.Groups[2].Value }
            Add-Result 'C' 'Create caso' 'PASS' "CaseId=$smokeCaseId"
        } else {
            Add-Result 'C' 'Create caso' 'FAIL' "Redirect=$($caseCreatePost.Location)"
            throw 'Case id parse failed.'
        }
    } else {
        Add-Result 'C' 'Create caso' 'FAIL' "HTTP $($caseCreatePost.Code)"
        throw 'Case create failed.'
    }

    # C4 upload evidencia
    $caseDetailsGet = Invoke-Curl -Method 'GET' -Path "/Casos/Details/$smokeCaseId" -CookieFile $adminCookie
    $uploadToken = Get-SmokeToken $caseDetailsGet.Body
    if ([string]::IsNullOrWhiteSpace($uploadToken)) { throw 'Casos/Details upload token not found.' }
    $uploadPath = Get-SmokeHandlerPathFromHtml -Html $caseDetailsGet.Body -HandlerName 'Upload'
    if ([string]::IsNullOrWhiteSpace($uploadPath)) { $uploadPath = "/Casos/Details/$smokeCaseId?handler=Upload" }

    $pngFile = Join-Path $env:TEMP ('smoke-' + [guid]::NewGuid().ToString('N') + '.png')
    [IO.File]::WriteAllBytes($pngFile, [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/a6kAAAAASUVORK5CYII='))

    $uploadPost = Invoke-CurlMultipartDirect -Path $uploadPath -CookieFile $adminCookie -MultipartFields @(
        "__RequestVerificationToken=$uploadToken",
        "Input.TipoEvidencia=Oficio",
        "Input.FechaEvidencia=$today",
        "Input.Notas=SMOKE EVIDENCE",
        "Input.Archivos=@$pngFile;type=image/png"
    )

    if ($uploadPost.Code -ne 302) {
        Start-Sleep -Milliseconds 800
        $caseDetailsRetry = Invoke-Curl -Method 'GET' -Path "/Casos/Details/$smokeCaseId" -CookieFile $adminCookie
        $uploadTokenRetry = Get-SmokeToken $caseDetailsRetry.Body
        $uploadPathRetry = Get-SmokeHandlerPathFromHtml -Html $caseDetailsRetry.Body -HandlerName 'Upload'
        if ([string]::IsNullOrWhiteSpace($uploadPathRetry)) { $uploadPathRetry = $uploadPath }
        $uploadPost = Invoke-CurlMultipartDirect -Path $uploadPathRetry -CookieFile $adminCookie -MultipartFields @(
            "__RequestVerificationToken=$uploadTokenRetry",
            "Input.TipoEvidencia=Oficio",
            "Input.FechaEvidencia=$today",
            "Input.Notas=SMOKE EVIDENCE RETRY",
            "Input.Archivos=@$pngFile;type=image/png"
        )
    }

    if ($uploadPost.Code -eq 302) {
        Add-Result 'C' 'Upload evidencia' 'PASS' "Redirect=$($uploadPost.Location)"
    } else {
        $uploadBodySnippet = ($uploadPost.Body -replace '\s+', ' ')
        if ($uploadBodySnippet.Length -gt 180) {
            $uploadBodySnippet = $uploadBodySnippet.Substring(0, 180)
        }
        Add-Result 'C' 'Upload evidencia' 'FAIL' "HTTP $($uploadPost.Code) Path=/Casos/Details/${smokeCaseId}?handler=Upload Snippet=$uploadBodySnippet"
    }

    # detect evidencia id
    $caseDetailsAfter = Invoke-Curl -Method 'GET' -Path "/Casos/Details/$smokeCaseId" -CookieFile $adminCookie
    $evidMatches = [regex]::Matches($caseDetailsAfter.Body, '/Evidencias/View/(\d+)', 'IgnoreCase')
    if ($evidMatches.Count -gt 0) {
        $smokeEvidenceId = [int]$evidMatches[$evidMatches.Count-1].Groups[1].Value
        Add-Result 'C' 'Evidence id detected' 'PASS' "EvidenceId=$smokeEvidenceId"
    } else {
        Add-Result 'C' 'Evidence id detected' 'FAIL' 'No evidence id in case detail.'
        throw 'Evidence id missing.'
    }

    # disk file check
    $storageFolder = Join-Path $StorageRoot (Join-Path $smokeOficioNumber '001\original')
    if (Test-Path $storageFolder) {
        $f = Get-ChildItem -Path $storageFolder -Filter *.jpg -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($f) {
            $smokeEvidenceFilePath = $f.FullName
            Add-Result 'C' 'Stored evidence file exists' 'PASS' $smokeEvidenceFilePath
        } else {
            Add-Result 'C' 'Stored evidence file exists' 'FAIL' "No jpg in $storageFolder"
        }
    } else {
        Add-Result 'C' 'Stored evidence file exists' 'FAIL' "Folder not found: $storageFolder"
    }

    # protected endpoint
    $evAnon = Invoke-Curl -Method 'GET' -Path "/Evidencias/View/$smokeEvidenceId" -CookieFile $anonCookie
    if ($evAnon.Code -eq 302 -and $evAnon.Location -like '*Login*') {
        Add-Result 'C' 'Evidence requires auth (anon)' 'PASS' "HTTP 302 => $($evAnon.Location)"
    } else {
        Add-Result 'C' 'Evidence requires auth (anon)' 'FAIL' "HTTP $($evAnon.Code)"
    }

    $evAuth = Invoke-Curl -Method 'GET' -Path "/Evidencias/View/$smokeEvidenceId" -CookieFile $adminCookie
    if ($evAuth.Code -eq 200 -and $evAuth.ContentType -like 'image/*') {
        Add-Result 'C' 'Evidence served to authenticated user' 'PASS' "ContentType=$($evAuth.ContentType)"
    } else {
        Add-Result 'C' 'Evidence served to authenticated user' 'FAIL' "HTTP $($evAuth.Code), CT=$($evAuth.ContentType)"
    }

    if ($smokeEvidenceFilePath) {
        $fname = [IO.Path]::GetFileName($smokeEvidenceFilePath)
        $staticUrl = "/media/$smokeOficioNumber/001/original/$fname"
        $staticResp = Invoke-Curl -Method 'GET' -Path $staticUrl -CookieFile $adminCookie
        if ($staticResp.Code -ne 200) {
            Add-Result 'C' 'Direct static URL blocked' 'PASS' "HTTP $($staticResp.Code) $staticUrl"
        } else {
            Add-Result 'C' 'Direct static URL blocked' 'FAIL' "HTTP 200 $staticUrl"
        }
    } else {
        Add-Result 'C' 'Direct static URL blocked' 'FAIL' 'No file path available.'
    }

    # delete evidence
    $evDelGet = Invoke-Curl -Method 'GET' -Path "/Evidencias/Delete/$smokeEvidenceId" -CookieFile $adminCookie
    $evDelToken = Get-SmokeToken $evDelGet.Body
    $evDelPost = Invoke-Curl -Method 'POST' -Path "/Evidencias/Delete/$smokeEvidenceId" -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $evDelToken
    }
    if ($evDelPost.Code -eq 302) {
        Add-Result 'C' 'Delete evidencia' 'PASS' "Redirect=$($evDelPost.Location)"
    } else {
        Add-Result 'C' 'Delete evidencia' 'FAIL' "HTTP $($evDelPost.Code)"
    }

    if ($smokeEvidenceFilePath) {
        if (-not (Test-Path $smokeEvidenceFilePath)) {
            Add-Result 'C' 'Evidence removed from disk' 'PASS' 'File removed.'
        } else {
            Add-Result 'C' 'Evidence removed from disk' 'FAIL' 'File still exists.'
        }
    }

    # delete case
    $caseDelGet = Invoke-Curl -Method 'GET' -Path "/Casos/Delete/$smokeCaseId" -CookieFile $adminCookie
    $caseDelToken = Get-SmokeToken $caseDelGet.Body
    $caseDelPost = Invoke-Curl -Method 'POST' -Path "/Casos/Delete/$smokeCaseId" -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $caseDelToken
    }
    if ($caseDelPost.Code -eq 302) {
        Add-Result 'C' 'Delete caso' 'PASS' "Redirect=$($caseDelPost.Location)"
    } else {
        Add-Result 'C' 'Delete caso' 'FAIL' "HTTP $($caseDelPost.Code)"
    }

    # delete oficio
    $ofDelGet = Invoke-Curl -Method 'GET' -Path "/Oficios/Delete/$smokeOficioId" -CookieFile $adminCookie
    $ofDelToken = Get-SmokeToken $ofDelGet.Body
    $ofDelPost = Invoke-Curl -Method 'POST' -Path "/Oficios/Delete/$smokeOficioId" -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $ofDelToken
    }
    if ($ofDelPost.Code -eq 302) {
        Add-Result 'C' 'Delete oficio' 'PASS' "Redirect=$($ofDelPost.Location)"
    } else {
        Add-Result 'C' 'Delete oficio' 'FAIL' "HTTP $($ofDelPost.Code)"
    }

    $ofFolder = Join-Path $StorageRoot $smokeOficioNumber
    if (-not (Test-Path $ofFolder)) {
        Add-Result 'C' 'Oficio folder removed from disk' 'PASS' $ofFolder
    } else {
        Add-Result 'C' 'Oficio folder removed from disk' 'FAIL' $ofFolder
    }

    # D: audit event checks
    $auditChecks = @(
        @{ Event='LOGIN'; Username='admin'; Label='Admin LOGIN logged' },
        @{ Event='LOGIN_FAIL'; Username=$smokeUsername; Label='Smoke LOGIN_FAIL logged' },
        @{ Event='LOCKOUT'; Username=$smokeUsername; Label='Smoke LOCKOUT logged' },
        @{ Event='CREATE'; Username='admin'; Label='CREATE logged' },
        @{ Event='UPDATE'; Username='admin'; Label='UPDATE logged' },
        @{ Event='DELETE'; Username='admin'; Label='DELETE logged' },
        @{ Event='UPLOAD_EVIDENCE'; Username='admin'; Label='UPLOAD_EVIDENCE logged' },
        @{ Event='DELETE_EVIDENCE'; Username='admin'; Label='DELETE_EVIDENCE logged' },
        @{ Event='VIEW_EVIDENCE'; Username='admin'; Label='VIEW_EVIDENCE logged' }
    )

    foreach ($q in $auditChecks) {
        $u = [Uri]::EscapeDataString([string]$q.Username)
        $audit = Invoke-Curl -Method 'GET' -Path "/Admin/AuditLog?Days=2&EventType=$($q.Event)&Username=$u" -CookieFile $adminCookie
        if ($audit.Body -like '*Sin eventos para los filtros seleccionados*') {
            Add-Result 'D' $q.Label 'FAIL' "No rows for EventType=$($q.Event), Username=$($q.Username)"
        } else {
            Add-Result 'D' $q.Label 'PASS' "Rows found for EventType=$($q.Event), Username=$($q.Username)"
        }
    }

    # E logout and post-logout protection
    $index = Invoke-Curl -Method 'GET' -Path '/Index' -CookieFile $adminCookie
    $logoutToken = Get-SmokeToken $index.Body
    if ([string]::IsNullOrWhiteSpace($logoutToken)) { throw 'Logout token not found.' }

    $logoutPost = Invoke-Curl -Method 'POST' -Path '/Logout' -CookieFile $adminCookie -FormFields @{ '__RequestVerificationToken' = $logoutToken }
    if ($logoutPost.Code -eq 302 -and $logoutPost.Location -like '*Login*') {
        Add-Result 'E' 'Logout request' 'PASS' "HTTP 302 => $($logoutPost.Location)"
    } else {
        Add-Result 'E' 'Logout request' 'FAIL' "HTTP $($logoutPost.Code) => $($logoutPost.Location)"
    }

    $postLogout = Invoke-Curl -Method 'GET' -Path '/Oficios' -CookieFile $adminCookie
    if ($postLogout.Code -eq 302 -and $postLogout.Location -like '*Login*') {
        Add-Result 'E' 'Post-logout protected route' 'PASS' "HTTP 302 => $($postLogout.Location)"
    } else {
        Add-Result 'E' 'Post-logout protected route' 'FAIL' "HTTP $($postLogout.Code)"
    }

    # D logout audit after logout event happened: login again and verify
    $loginAgainGet = Invoke-Curl -Method 'GET' -Path '/Login' -CookieFile $adminCookie
    $loginAgainToken = Get-SmokeToken $loginAgainGet.Body
    $loginAgainPost = Invoke-Curl -Method 'POST' -Path '/Login' -CookieFile $adminCookie -FormFields @{
        '__RequestVerificationToken' = $loginAgainToken
        'Input.Usuario' = $AdminUser
        'Input.Contrasena' = $activeAdminPass
    }

    if ($loginAgainPost.Code -eq 302) {
        $auditLogout = Invoke-Curl -Method 'GET' -Path '/Admin/AuditLog?Days=2&EventType=LOGOUT&Username=admin' -CookieFile $adminCookie
        if ($auditLogout.Body -like '*Sin eventos para los filtros seleccionados*') {
            Add-Result 'D' 'Admin LOGOUT logged' 'FAIL' 'No rows found for LOGOUT/admin.'
        } else {
            Add-Result 'D' 'Admin LOGOUT logged' 'PASS' 'Rows found for LOGOUT/admin.'
        }
    } else {
        Add-Result 'D' 'Admin LOGOUT logged' 'FAIL' 'Could not re-login admin to validate logout event.'
    }
}
catch {
    Add-Result 'X' 'Smoke execution' 'FAIL' $_.Exception.Message
}
finally {
    Remove-Item -Path $adminCookie,$anonCookie,$smokeCookie,$roleRevocationCookie -Force -ErrorAction SilentlyContinue
}

Add-Result 'F' '30-min idle timeout runtime test' 'SKIP' 'Not executed automatically (requires waiting >30 minutes). Config remains 30 min.'

$reportPath = Write-SmokeReport -Results $results

$results | Format-Table -AutoSize
Write-Output "REPORT_PATH=$reportPath"



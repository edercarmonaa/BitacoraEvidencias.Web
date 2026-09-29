param(
    [string]$BaseUrl = 'https://127.0.0.1:5067',
    [string]$AdminUser = 'admin',
    [string]$AdminPass = '',
    [string]$StorageRoot = 'Storage'
)

Add-Type -AssemblyName System.Net.Http
$ErrorActionPreference = 'Stop'
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

. (Join-Path $PSScriptRoot 'Smoke.Common.ps1')

$AdminPass = Resolve-SmokeAdminPassword -AdminPass $AdminPass

$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Block,
        [string]$Check,
        [string]$Status,
        [string]$Detail
    )

    $results.Add([pscustomobject]@{
        Block = $Block
        Check = $Check
        Status = $Status
        Detail = $Detail
    })
}

function New-ClientContext {
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.CookieContainer = New-Object System.Net.CookieContainer
    $handler.AllowAutoRedirect = $false
    try {
        $handler.ServerCertificateCustomValidationCallback = { return $true }
    } catch {
        # ignore on older runtime
    }

    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.BaseAddress = [Uri]$BaseUrl
    return [pscustomobject]@{
        Client = $client
        Handler = $handler
    }
}

function Get-Body {
    param([System.Net.Http.HttpResponseMessage]$Response)
    return $Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
}

function New-FormContent {
    param([hashtable]$Fields)
    $pairs = New-Object 'System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]'
    foreach ($k in $Fields.Keys) {
        $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new([string]$k, [string]$Fields[$k]))
    }
    return [System.Net.Http.FormUrlEncodedContent]::new($pairs)
}

function Get-LocationValue {
    param([System.Net.Http.HttpResponseMessage]$Response)
    if ($null -eq $Response.Headers.Location) { return '' }
    return $Response.Headers.Location.ToString()
}

$adminCtx = $null
$smokeUsername = 'smoke.lock.' + (Get-Date -Format 'yyyyMMddHHmmss')
$tempPassword = $null
$roleRevocationCtx = $null
$roleRevocationUsername = 'smoke.role.' + (Get-Date -Format 'yyyyMMddHHmmss')
$roleRevocationTempPassword = $null
$roleRevocationNewPassword = 'RoleRevoc!' + (Get-Date -Format 'yyyyMMddHHmmss')
$roleRevocationUserId = $null
$activeAdminPass = $AdminPass
$smokeAdminChangedPass = 'SmokeAdmin!' + (Get-Date -Format 'yyyyMMddHHmmss')
$smokeOficioId = $null
$smokeOficioNumber = 'SMOKE-OF-' + (Get-Date -Format 'yyyyMMddHHmmss')
$smokeCaseId = $null
$smokeEvidenceId = $null
$smokeEvidenceFilePath = $null

# Block A: Access and auth baseline
try {
    $anonCtx = New-ClientContext
    $anonResp = $anonCtx.Client.GetAsync('/Oficios').GetAwaiter().GetResult()
    $anonCode = [int]$anonResp.StatusCode
    $anonLoc = Get-LocationValue $anonResp
    if ($anonCode -eq 302 -and $anonLoc -like '*Login*') {
        Add-Result 'A' 'Anonymous access redirected' 'PASS' "HTTP $anonCode => $anonLoc"
    } else {
        Add-Result 'A' 'Anonymous access redirected' 'FAIL' "HTTP $anonCode => $anonLoc"
    }
    $anonCtx.Client.Dispose()

    $adminCtx = New-ClientContext
    $loginGet = $adminCtx.Client.GetAsync('/Login').GetAwaiter().GetResult()
    $loginHtml = Get-Body $loginGet
    $loginToken = Get-SmokeToken $loginHtml
    if ([string]::IsNullOrWhiteSpace($loginToken)) {
        throw 'Could not read antiforgery token on /Login'
    }

    $loginForm = @{
        '__RequestVerificationToken' = $loginToken
        'Input.Usuario' = $AdminUser
        'Input.Contrasena' = $AdminPass
    }

    $loginPostContent = New-FormContent $loginForm
    $loginPost = $adminCtx.Client.PostAsync('/Login', $loginPostContent).GetAwaiter().GetResult()
    $loginPostContent.Dispose()

    $loginCode = [int]$loginPost.StatusCode
    $loginLoc = Get-LocationValue $loginPost

    if ($loginCode -eq 302 -and $loginLoc -like '*ChangePassword*') {
        Add-Result 'A' 'Admin login' 'PASS' "HTTP $loginCode => $loginLoc (password change required)"

        $adminChangeGet = $adminCtx.Client.GetAsync($loginLoc).GetAwaiter().GetResult()
        $adminChangeHtml = Get-Body $adminChangeGet
        $adminChangeToken = Get-SmokeToken $adminChangeHtml
        if ([string]::IsNullOrWhiteSpace($adminChangeToken)) {
            throw 'Could not read antiforgery token on admin change password page.'
        }

        $adminChangeForm = @{
            '__RequestVerificationToken' = $adminChangeToken
            'Input.CurrentPassword' = $AdminPass
            'Input.NewPassword' = $smokeAdminChangedPass
            'Input.ConfirmPassword' = $smokeAdminChangedPass
        }

        $adminChangeContent = New-FormContent $adminChangeForm
        $adminChangePost = $adminCtx.Client.PostAsync($loginLoc, $adminChangeContent).GetAwaiter().GetResult()
        $adminChangeContent.Dispose()
        $adminChangeLoc = Get-LocationValue $adminChangePost

        if ([int]$adminChangePost.StatusCode -eq 302 -and $adminChangeLoc -notlike '*ChangePassword*') {
            $activeAdminPass = $smokeAdminChangedPass
            Add-Result 'A' 'Admin changed temporary password' 'PASS' "HTTP 302 => $adminChangeLoc"
        } else {
            Add-Result 'A' 'Admin changed temporary password' 'FAIL' "HTTP $([int]$adminChangePost.StatusCode) => $adminChangeLoc"
            throw 'Admin could not complete forced password change.'
        }
    } elseif ($loginCode -eq 302 -and $loginLoc -notlike '*Login*') {
        Add-Result 'A' 'Admin login' 'PASS' "HTTP $loginCode => $loginLoc"
    } else {
        $body = Get-Body $loginPost
        Add-Result 'A' 'Admin login' 'FAIL' "HTTP $loginCode. BodyHasError=$($body -like '*incorrect*')"
        throw 'Admin login failed; aborting deep checks.'
    }

    $usersResp = $adminCtx.Client.GetAsync('/Admin/Users').GetAwaiter().GetResult()
    $usersCode = [int]$usersResp.StatusCode
    $usersHtml = Get-Body $usersResp
    if ($usersCode -eq 200 -and $usersHtml -like '*Usuarios del sistema*') {
        Add-Result 'A' 'Admin authorization to /Admin/Users' 'PASS' "HTTP $usersCode"
    } else {
        Add-Result 'A' 'Admin authorization to /Admin/Users' 'FAIL' "HTTP $usersCode"
    }
}
catch {
    Add-Result 'A' 'Access/auth execution' 'FAIL' $_.Exception.Message
}

# Block B: Create smoke user and lockout
if ($null -ne $adminCtx) {
    try {
        $usersGetForToken = $adminCtx.Client.GetAsync('/Admin/Users').GetAwaiter().GetResult()
        $usersTokenHtml = Get-Body $usersGetForToken
        $usersToken = Get-SmokeToken $usersTokenHtml
        if ([string]::IsNullOrWhiteSpace($usersToken)) {
            throw 'Could not read antiforgery token on /Admin/Users'
        }

        $createUserForm = @{
            '__RequestVerificationToken' = $usersToken
            'NewUser.Username' = $smokeUsername
            'NewUser.Role' = 'Capturista'
        }

        $createContent = New-FormContent $createUserForm
        $createResp = $adminCtx.Client.PostAsync('/Admin/Users?handler=Create', $createContent).GetAwaiter().GetResult()
        $createContent.Dispose()

        $createCode = [int]$createResp.StatusCode
        $createLoc = Get-LocationValue $createResp
        if ($createCode -eq 302) {
            Add-Result 'B' 'Create lockout test user' 'PASS' "HTTP $createCode => $createLoc ($smokeUsername)"
        } else {
            Add-Result 'B' 'Create lockout test user' 'FAIL' "HTTP $createCode"
            throw 'Cannot continue lockout test without smoke user.'
        }

        $usersAfterCreate = $adminCtx.Client.GetAsync('/Admin/Users').GetAwaiter().GetResult()
        $usersAfterCreateHtml = Get-Body $usersAfterCreate
        $pwMatch = [regex]::Match($usersAfterCreateHtml, 'temporal:\s*([^<\r\n]+)', 'IgnoreCase')
        if ($pwMatch.Success) {
            $tempPassword = $pwMatch.Groups[1].Value.Trim()
            Add-Result 'B' 'Temporary password extracted' 'PASS' 'Password extracted from success message.'
        } else {
            Add-Result 'B' 'Temporary password extracted' 'FAIL' 'Could not parse temporary password from Users page.'
        }

        for ($i = 1; $i -le 5; $i++) {
            $attemptCtx = New-ClientContext
            $lg = $attemptCtx.Client.GetAsync('/Login').GetAwaiter().GetResult()
            $lh = Get-Body $lg
            $tk = Get-SmokeToken $lh
            if ([string]::IsNullOrWhiteSpace($tk)) {
                throw "Attempt ${i}: missing login token"
            }

            $badForm = @{
                '__RequestVerificationToken' = $tk
                'Input.Usuario' = $smokeUsername
                'Input.Contrasena' = 'WrongPass!123'
            }
            $badContent = New-FormContent $badForm
            $badResp = $attemptCtx.Client.PostAsync('/Login', $badContent).GetAwaiter().GetResult()
            $badContent.Dispose()
            $badBody = Get-Body $badResp

            if ($badBody -like '*incorrect*' -or $badBody -like '*bloqueada*') {
                Add-Result 'B' "Failed login attempt $i" 'PASS' 'Application rejected credentials.'
            } else {
                Add-Result 'B' "Failed login attempt $i" 'FAIL' "Unexpected response HTTP $([int]$badResp.StatusCode)"
            }
            $attemptCtx.Client.Dispose()
        }

        if (-not [string]::IsNullOrWhiteSpace($tempPassword)) {
            $attemptCtx2 = New-ClientContext
            $lg2 = $attemptCtx2.Client.GetAsync('/Login').GetAwaiter().GetResult()
            $lh2 = Get-Body $lg2
            $tk2 = Get-SmokeToken $lh2
            $goodForm = @{
                '__RequestVerificationToken' = $tk2
                'Input.Usuario' = $smokeUsername
                'Input.Contrasena' = $tempPassword
            }
            $goodContent = New-FormContent $goodForm
            $goodResp = $attemptCtx2.Client.PostAsync('/Login', $goodContent).GetAwaiter().GetResult()
            $goodContent.Dispose()
            $goodBody = Get-Body $goodResp

            if ($goodBody -like '*Cuenta bloqueada*' -or $goodBody -like '*bloqueada*') {
                Add-Result 'B' 'Lockout after 5 failures' 'PASS' 'Locked user was rejected even with correct temporary password.'
            } else {
                $goodCode = [int]$goodResp.StatusCode
                $goodLoc = Get-LocationValue $goodResp
                Add-Result 'B' 'Lockout after 5 failures' 'FAIL' "Unexpected response HTTP $goodCode => $goodLoc"
            }
            $attemptCtx2.Client.Dispose()
        } else {
            Add-Result 'B' 'Lockout after 5 failures' 'FAIL' 'Skipped correct-password lockout validation because temp password was not parsed.'
        }
    }
    catch {
        Add-Result 'B' 'Lockout execution' 'FAIL' $_.Exception.Message
    }
}

# Block B2: Role revocation is applied to active sessions
if ($null -ne $adminCtx) {
    try {
        $usersGetForToken = $adminCtx.Client.GetAsync('/Admin/Users').GetAwaiter().GetResult()
        $usersTokenHtml = Get-Body $usersGetForToken
        $usersToken = Get-SmokeToken $usersTokenHtml
        if ([string]::IsNullOrWhiteSpace($usersToken)) {
            throw 'Could not read antiforgery token for role revocation test.'
        }

        $createRoleUserForm = @{
            '__RequestVerificationToken' = $usersToken
            'NewUser.Username' = $roleRevocationUsername
            'NewUser.Role' = 'Admin'
        }

        $createRoleUserContent = New-FormContent $createRoleUserForm
        $createRoleUserResp = $adminCtx.Client.PostAsync('/Admin/Users?handler=Create', $createRoleUserContent).GetAwaiter().GetResult()
        $createRoleUserContent.Dispose()

        if ([int]$createRoleUserResp.StatusCode -eq 302) {
            Add-Result 'B2' 'Create role-revocation admin user' 'PASS' "HTTP 302 => $((Get-LocationValue $createRoleUserResp)) ($roleRevocationUsername)"
        } else {
            Add-Result 'B2' 'Create role-revocation admin user' 'FAIL' "HTTP $([int]$createRoleUserResp.StatusCode)"
            throw 'Cannot continue role revocation test without temp admin user.'
        }

        $usersAfterRoleCreate = $adminCtx.Client.GetAsync('/Admin/Users').GetAwaiter().GetResult()
        $usersAfterRoleCreateHtml = Get-Body $usersAfterRoleCreate
        $rolePasswordMatch = [regex]::Match($usersAfterRoleCreateHtml, 'temporal:\s*([^<\r\n]+)', 'IgnoreCase')
        if ($rolePasswordMatch.Success) {
            $roleRevocationTempPassword = $rolePasswordMatch.Groups[1].Value.Trim()
            Add-Result 'B2' 'Temporary password for temp admin extracted' 'PASS' 'Password extracted from success message.'
        } else {
            Add-Result 'B2' 'Temporary password for temp admin extracted' 'FAIL' 'Could not parse temporary password from Users page.'
            throw 'Cannot continue role revocation test without temp admin password.'
        }

        $roleRevocationUserId = Get-SmokeUserIdFromUsersHtml -Html $usersAfterRoleCreateHtml -Username $roleRevocationUsername
        if ($null -ne $roleRevocationUserId) {
            Add-Result 'B2' 'Temp admin user id detected' 'PASS' "UserId=$roleRevocationUserId"
        } else {
            Add-Result 'B2' 'Temp admin user id detected' 'FAIL' 'Could not parse temp admin user id from Users page.'
            throw 'Cannot continue role revocation test without temp admin user id.'
        }

        $roleRevocationCtx = New-ClientContext
        $roleLoginGet = $roleRevocationCtx.Client.GetAsync('/Login').GetAwaiter().GetResult()
        $roleLoginHtml = Get-Body $roleLoginGet
        $roleLoginToken = Get-SmokeToken $roleLoginHtml
        if ([string]::IsNullOrWhiteSpace($roleLoginToken)) {
            throw 'Could not read login token for temp admin.'
        }

        $roleLoginForm = @{
            '__RequestVerificationToken' = $roleLoginToken
            'Input.Usuario' = $roleRevocationUsername
            'Input.Contrasena' = $roleRevocationTempPassword
        }

        $roleLoginContent = New-FormContent $roleLoginForm
        $roleLoginPost = $roleRevocationCtx.Client.PostAsync('/Login', $roleLoginContent).GetAwaiter().GetResult()
        $roleLoginContent.Dispose()
        $roleLoginLoc = Get-LocationValue $roleLoginPost

        if ([int]$roleLoginPost.StatusCode -eq 302 -and $roleLoginLoc -like '*ChangePassword*') {
            Add-Result 'B2' 'Temp admin redirected to change password' 'PASS' "HTTP 302 => $roleLoginLoc"
        } else {
            Add-Result 'B2' 'Temp admin redirected to change password' 'FAIL' "HTTP $([int]$roleLoginPost.StatusCode) => $roleLoginLoc"
            throw 'Temp admin login did not force password change.'
        }

        $changePasswordGet = $roleRevocationCtx.Client.GetAsync($roleLoginLoc).GetAwaiter().GetResult()
        $changePasswordHtml = Get-Body $changePasswordGet
        $changePasswordToken = Get-SmokeToken $changePasswordHtml
        if ([string]::IsNullOrWhiteSpace($changePasswordToken)) {
            throw 'Could not read antiforgery token on change password page.'
        }

        $changePasswordForm = @{
            '__RequestVerificationToken' = $changePasswordToken
            'Input.CurrentPassword' = $roleRevocationTempPassword
            'Input.NewPassword' = $roleRevocationNewPassword
            'Input.ConfirmPassword' = $roleRevocationNewPassword
        }

        $changePasswordContent = New-FormContent $changePasswordForm
        $changePasswordPost = $roleRevocationCtx.Client.PostAsync($roleLoginLoc, $changePasswordContent).GetAwaiter().GetResult()
        $changePasswordContent.Dispose()
        $changePasswordLoc = Get-LocationValue $changePasswordPost

        if ([int]$changePasswordPost.StatusCode -eq 302 -and $changePasswordLoc -notlike '*ChangePassword*') {
            Add-Result 'B2' 'Temp admin changed password' 'PASS' "HTTP 302 => $changePasswordLoc"
        } else {
            Add-Result 'B2' 'Temp admin changed password' 'FAIL' "HTTP $([int]$changePasswordPost.StatusCode) => $changePasswordLoc"
            throw 'Temp admin could not finish password change.'
        }

        $tempAdminUsersResp = $roleRevocationCtx.Client.GetAsync('/Admin/Users').GetAwaiter().GetResult()
        $tempAdminUsersCode = [int]$tempAdminUsersResp.StatusCode
        $tempAdminUsersHtml = Get-Body $tempAdminUsersResp
        if ($tempAdminUsersCode -eq 200 -and $tempAdminUsersHtml -like '*Usuarios del sistema*') {
            Add-Result 'B2' 'Temp admin has admin access before demotion' 'PASS' "HTTP $tempAdminUsersCode"
        } else {
            Add-Result 'B2' 'Temp admin has admin access before demotion' 'FAIL' "HTTP $tempAdminUsersCode"
            throw 'Temp admin never reached admin area before demotion.'
        }

        $usersGetForDemotion = $adminCtx.Client.GetAsync('/Admin/Users').GetAwaiter().GetResult()
        $usersDemotionHtml = Get-Body $usersGetForDemotion
        $usersDemotionToken = Get-SmokeToken $usersDemotionHtml
        if ([string]::IsNullOrWhiteSpace($usersDemotionToken)) {
            throw 'Could not read antiforgery token for demotion.'
        }

        $demotionForm = @{
            '__RequestVerificationToken' = $usersDemotionToken
            'id' = [string]$roleRevocationUserId
            'role' = 'Capturista'
        }

        $demotionContent = New-FormContent $demotionForm
        $demotionResp = $adminCtx.Client.PostAsync('/Admin/Users?handler=UpdateRole', $demotionContent).GetAwaiter().GetResult()
        $demotionContent.Dispose()

        if ([int]$demotionResp.StatusCode -eq 302) {
            Add-Result 'B2' 'Admin demoted temp admin to Capturista' 'PASS' "HTTP 302 => $((Get-LocationValue $demotionResp))"
        } else {
            Add-Result 'B2' 'Admin demoted temp admin to Capturista' 'FAIL' "HTTP $([int]$demotionResp.StatusCode)"
            throw 'Could not demote temp admin.'
        }

        $demotedAdminResp = $roleRevocationCtx.Client.GetAsync('/Admin/Users').GetAwaiter().GetResult()
        $demotedAdminCode = [int]$demotedAdminResp.StatusCode
        $demotedAdminLoc = Get-LocationValue $demotedAdminResp
        if ($demotedAdminCode -eq 302 -and $demotedAdminLoc -like '*Error*') {
            Add-Result 'B2' 'Demoted session loses admin access immediately' 'PASS' "HTTP $demotedAdminCode => $demotedAdminLoc"
        } else {
            Add-Result 'B2' 'Demoted session loses admin access immediately' 'FAIL' "HTTP $demotedAdminCode => $demotedAdminLoc"
            throw 'Demoted session kept admin access.'
        }

        $demotedRegularResp = $roleRevocationCtx.Client.GetAsync('/Oficios').GetAwaiter().GetResult()
        $demotedRegularCode = [int]$demotedRegularResp.StatusCode
        if ($demotedRegularCode -eq 200) {
            Add-Result 'B2' 'Demoted session remains authenticated as app user' 'PASS' "HTTP $demotedRegularCode"
        } else {
            Add-Result 'B2' 'Demoted session remains authenticated as app user' 'FAIL' "HTTP $demotedRegularCode => $((Get-LocationValue $demotedRegularResp))"
        }

        $usersGetForCleanup = $adminCtx.Client.GetAsync('/Admin/Users').GetAwaiter().GetResult()
        $usersCleanupHtml = Get-Body $usersGetForCleanup
        $usersCleanupToken = Get-SmokeToken $usersCleanupHtml
        if ([string]::IsNullOrWhiteSpace($usersCleanupToken)) {
            throw 'Could not read antiforgery token for cleanup.'
        }

        $cleanupForm = @{
            '__RequestVerificationToken' = $usersCleanupToken
            'id' = [string]$roleRevocationUserId
        }

        $cleanupContent = New-FormContent $cleanupForm
        $cleanupResp = $adminCtx.Client.PostAsync('/Admin/Users?handler=ToggleActive', $cleanupContent).GetAwaiter().GetResult()
        $cleanupContent.Dispose()

        if ([int]$cleanupResp.StatusCode -eq 302) {
            Add-Result 'B2' 'Temp admin user deactivated after test' 'PASS' "HTTP 302 => $((Get-LocationValue $cleanupResp))"
        } else {
            Add-Result 'B2' 'Temp admin user deactivated after test' 'FAIL' "HTTP $([int]$cleanupResp.StatusCode)"
        }
    }
    catch {
        Add-Result 'B2' 'Role revocation execution' 'FAIL' $_.Exception.Message
    }
}

# Block C: CRUD smoke (Oficio/Caso/Evidencia)
if ($null -ne $adminCtx) {
    try {
        # Create oficio
        $ofCreateGet = $adminCtx.Client.GetAsync('/Oficios/Create').GetAwaiter().GetResult()
        $ofCreateHtml = Get-Body $ofCreateGet
        $ofToken = Get-SmokeToken $ofCreateHtml
        if ([string]::IsNullOrWhiteSpace($ofToken)) {
            throw 'Oficios/Create token not found.'
        }

        $today = (Get-Date).ToString('yyyy-MM-dd')
        $ofCreateForm = @{
            '__RequestVerificationToken' = $ofToken
            'Input.NumeroOficio' = $smokeOficioNumber
            'Input.FechaOficio' = $today
            'Input.Asunto' = 'SMOKE TEST OFICIO'
            'Input.Notas' = 'SMOKE NOTES'
        }
        $ofCreateContent = New-FormContent $ofCreateForm
        $ofCreateResp = $adminCtx.Client.PostAsync('/Oficios/Create', $ofCreateContent).GetAwaiter().GetResult()
        $ofCreateContent.Dispose()

        $ofCreateCode = [int]$ofCreateResp.StatusCode
        $ofCreateLoc = Get-LocationValue $ofCreateResp
        if ($ofCreateCode -eq 302) {
            $mOf = [regex]::Match($ofCreateLoc, 'id=(\d+)|/Oficios/Details/(\d+)', 'IgnoreCase')
            if ($mOf.Success) {
                $smokeOficioId = if ($mOf.Groups[1].Success) { [int]$mOf.Groups[1].Value } else { [int]$mOf.Groups[2].Value }
                Add-Result 'C' 'Create oficio' 'PASS' "OficioId=$smokeOficioId"
            } else {
                Add-Result 'C' 'Create oficio' 'FAIL' "Could not parse oficio id from redirect: $ofCreateLoc"
                throw 'Cannot continue CRUD without oficio id.'
            }
        } else {
            Add-Result 'C' 'Create oficio' 'FAIL' "HTTP $ofCreateCode"
            throw 'Cannot continue CRUD without oficio.'
        }

        # Update oficio
        $ofEditGet = $adminCtx.Client.GetAsync("/Oficios/Edit/$smokeOficioId").GetAwaiter().GetResult()
        $ofEditHtml = Get-Body $ofEditGet
        $ofEditToken = Get-SmokeToken $ofEditHtml
        if ([string]::IsNullOrWhiteSpace($ofEditToken)) {
            throw 'Oficios/Edit token not found.'
        }

        $smokeOficioNumberUpdated = $smokeOficioNumber + '-UPD'
        $ofEditForm = @{
            '__RequestVerificationToken' = $ofEditToken
            'Input.Id' = [string]$smokeOficioId
            'Input.NumeroOficio' = $smokeOficioNumberUpdated
            'Input.FechaOficio' = $today
            'Input.Asunto' = 'SMOKE TEST OFICIO UPDATED'
            'Input.Notas' = 'SMOKE NOTES UPDATED'
        }
        $ofEditContent = New-FormContent $ofEditForm
        $ofEditResp = $adminCtx.Client.PostAsync("/Oficios/Edit/$smokeOficioId", $ofEditContent).GetAwaiter().GetResult()
        $ofEditContent.Dispose()

        if ([int]$ofEditResp.StatusCode -eq 302) {
            Add-Result 'C' 'Update oficio' 'PASS' "Redirect=$((Get-LocationValue $ofEditResp))"
            $smokeOficioNumber = $smokeOficioNumberUpdated
        } else {
            Add-Result 'C' 'Update oficio' 'FAIL' "HTTP $([int]$ofEditResp.StatusCode)"
        }

        # Create case
        $caseCreateGet = $adminCtx.Client.GetAsync("/Casos/Create?oficioId=$smokeOficioId").GetAwaiter().GetResult()
        $caseCreateHtml = Get-Body $caseCreateGet
        $caseToken = Get-SmokeToken $caseCreateHtml
        if ([string]::IsNullOrWhiteSpace($caseToken)) {
            throw 'Casos/Create token not found.'
        }

        $caseCreateForm = @{
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
        $caseCreateContent = New-FormContent $caseCreateForm
        $caseCreateResp = $adminCtx.Client.PostAsync("/Casos/Create?oficioId=$smokeOficioId", $caseCreateContent).GetAwaiter().GetResult()
        $caseCreateContent.Dispose()

        $caseCreateCode = [int]$caseCreateResp.StatusCode
        $caseCreateLoc = Get-LocationValue $caseCreateResp
        if ($caseCreateCode -eq 302) {
            $mCase = [regex]::Match($caseCreateLoc, 'id=(\d+)|/Casos/Details/(\d+)', 'IgnoreCase')
            if ($mCase.Success) {
                $smokeCaseId = if ($mCase.Groups[1].Success) { [int]$mCase.Groups[1].Value } else { [int]$mCase.Groups[2].Value }
                Add-Result 'C' 'Create caso' 'PASS' "CaseId=$smokeCaseId"
            } else {
                Add-Result 'C' 'Create caso' 'FAIL' "Could not parse case id from redirect: $caseCreateLoc"
                throw 'Cannot continue evidence tests without case id.'
            }
        } else {
            Add-Result 'C' 'Create caso' 'FAIL' "HTTP $caseCreateCode"
            throw 'Cannot continue evidence tests without case.'
        }

        # Upload evidence
        $caseDetailsGet = $adminCtx.Client.GetAsync("/Casos/Details/$smokeCaseId").GetAwaiter().GetResult()
        $caseDetailsHtml = Get-Body $caseDetailsGet
        $uploadToken = Get-SmokeToken $caseDetailsHtml
        if ([string]::IsNullOrWhiteSpace($uploadToken)) {
            throw 'Casos/Details upload token not found.'
        }
        $uploadPath = Get-SmokeHandlerPathFromHtml -Html $caseDetailsHtml -HandlerName 'Upload'
        if ([string]::IsNullOrWhiteSpace($uploadPath)) {
            $uploadPath = "/Casos/Details/$smokeCaseId?handler=Upload"
        }

        $pngBytes = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/a6kAAAAASUVORK5CYII=')

        $multipart = New-Object System.Net.Http.MultipartFormDataContent
        $multipart.Add([System.Net.Http.StringContent]::new($uploadToken), '__RequestVerificationToken')
        $multipart.Add([System.Net.Http.StringContent]::new('Oficio'), 'Input.TipoEvidencia')
        $multipart.Add([System.Net.Http.StringContent]::new($today), 'Input.FechaEvidencia')
        $multipart.Add([System.Net.Http.StringContent]::new('SMOKE EVIDENCE'), 'Input.Notas')

        $fileContent = [System.Net.Http.ByteArrayContent]::new([byte[]]$pngBytes)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
        $multipart.Add($fileContent, 'Input.Archivos', 'smoke.png')

        $uploadResp = $adminCtx.Client.PostAsync($uploadPath, $multipart).GetAwaiter().GetResult()
        $uploadBody = Get-Body $uploadResp
        $multipart.Dispose()

        if ([int]$uploadResp.StatusCode -ne 302) {
            Start-Sleep -Milliseconds 800

            $caseDetailsRetry = $adminCtx.Client.GetAsync("/Casos/Details/$smokeCaseId").GetAwaiter().GetResult()
            $caseDetailsRetryHtml = Get-Body $caseDetailsRetry
            $uploadTokenRetry = Get-SmokeToken $caseDetailsRetryHtml
            $uploadPathRetry = Get-SmokeHandlerPathFromHtml -Html $caseDetailsRetryHtml -HandlerName 'Upload'
            if ([string]::IsNullOrWhiteSpace($uploadPathRetry)) {
                $uploadPathRetry = $uploadPath
            }
            if (-not [string]::IsNullOrWhiteSpace($uploadTokenRetry)) {
                $retryMultipart = New-Object System.Net.Http.MultipartFormDataContent
                $retryMultipart.Add([System.Net.Http.StringContent]::new($uploadTokenRetry), '__RequestVerificationToken')
                $retryMultipart.Add([System.Net.Http.StringContent]::new('Oficio'), 'Input.TipoEvidencia')
                $retryMultipart.Add([System.Net.Http.StringContent]::new($today), 'Input.FechaEvidencia')
                $retryMultipart.Add([System.Net.Http.StringContent]::new('SMOKE EVIDENCE RETRY'), 'Input.Notas')

                $retryFileContent = [System.Net.Http.ByteArrayContent]::new([byte[]]$pngBytes)
                $retryFileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
                $retryMultipart.Add($retryFileContent, 'Input.Archivos', 'smoke.png')

                $uploadResp = $adminCtx.Client.PostAsync($uploadPathRetry, $retryMultipart).GetAwaiter().GetResult()
                $uploadBody = Get-Body $uploadResp
                $retryMultipart.Dispose()
            }
        }

        if ([int]$uploadResp.StatusCode -eq 302) {
            Add-Result 'C' 'Upload evidencia' 'PASS' "Redirect=$((Get-LocationValue $uploadResp))"
        } else {
            $uploadBodySnippet = ($uploadBody -replace '\s+', ' ')
            if ($uploadBodySnippet.Length -gt 180) {
                $uploadBodySnippet = $uploadBodySnippet.Substring(0, 180)
            }
            Add-Result 'C' 'Upload evidencia' 'FAIL' "HTTP $([int]$uploadResp.StatusCode) Path=/Casos/Details/${smokeCaseId}?handler=Upload Snippet=$uploadBodySnippet"
        }

        # Locate evidence id and file path in storage
        $caseDetailsAfterUpload = $adminCtx.Client.GetAsync("/Casos/Details/$smokeCaseId").GetAwaiter().GetResult()
        $caseDetailsAfterUploadHtml = Get-Body $caseDetailsAfterUpload
        $evidMatches = [regex]::Matches($caseDetailsAfterUploadHtml, '/Evidencias/View/(\d+)', 'IgnoreCase')
        if ($evidMatches.Count -gt 0) {
            $smokeEvidenceId = [int]$evidMatches[$evidMatches.Count - 1].Groups[1].Value
            Add-Result 'C' 'Evidence id detected in case detail' 'PASS' "EvidenceId=$smokeEvidenceId"
        } else {
            Add-Result 'C' 'Evidence id detected in case detail' 'FAIL' 'No /Evidencias/View/{id} found in case detail html.'
            throw 'Cannot continue evidence view/delete checks.'
        }

        $storageFolder = Join-Path $StorageRoot (Join-Path $smokeOficioNumber '001\original')
        if (Test-Path $storageFolder) {
            $evidenceFile = Get-ChildItem -Path $storageFolder -Filter *.jpg -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($null -ne $evidenceFile) {
                $smokeEvidenceFilePath = $evidenceFile.FullName
                Add-Result 'C' 'Stored evidence file exists' 'PASS' $smokeEvidenceFilePath
            } else {
                Add-Result 'C' 'Stored evidence file exists' 'FAIL' "Folder exists but no jpg files: $storageFolder"
            }
        } else {
            Add-Result 'C' 'Stored evidence file exists' 'FAIL' "Storage folder not found: $storageFolder"
        }

        # Protected evidence endpoint
        $anonCtx2 = New-ClientContext
        $evAnonResp = $anonCtx2.Client.GetAsync("/Evidencias/View/$smokeEvidenceId").GetAwaiter().GetResult()
        if ([int]$evAnonResp.StatusCode -eq 302 -and (Get-LocationValue $evAnonResp) -like '*Login*') {
            Add-Result 'C' 'Evidence endpoint requires auth (anonymous)' 'PASS' "HTTP 302 => $((Get-LocationValue $evAnonResp))"
        } else {
            Add-Result 'C' 'Evidence endpoint requires auth (anonymous)' 'FAIL' "HTTP $([int]$evAnonResp.StatusCode)"
        }
        $anonCtx2.Client.Dispose()

        $evAuthResp = $adminCtx.Client.GetAsync("/Evidencias/View/$smokeEvidenceId").GetAwaiter().GetResult()
        $ct = ''
        if ($null -ne $evAuthResp.Content.Headers.ContentType) { $ct = $evAuthResp.Content.Headers.ContentType.MediaType }
        if ([int]$evAuthResp.StatusCode -eq 200 -and $ct -like 'image/*') {
            Add-Result 'C' 'Evidence endpoint serves image when authenticated' 'PASS' "HTTP 200 ContentType=$ct"
        } else {
            Add-Result 'C' 'Evidence endpoint serves image when authenticated' 'FAIL' "HTTP $([int]$evAuthResp.StatusCode) ContentType=$ct"
        }

        # Direct URL static access should fail
        if ($smokeEvidenceFilePath) {
            $fname = [System.IO.Path]::GetFileName($smokeEvidenceFilePath)
            $staticPath = "/media/$smokeOficioNumber/001/original/$fname"
            $staticResp = $adminCtx.Client.GetAsync($staticPath).GetAwaiter().GetResult()
            if ([int]$staticResp.StatusCode -ne 200) {
                Add-Result 'C' 'Direct static URL to evidence blocked' 'PASS' "HTTP $([int]$staticResp.StatusCode) $staticPath"
            } else {
                Add-Result 'C' 'Direct static URL to evidence blocked' 'FAIL' "HTTP 200 $staticPath"
            }
        } else {
            Add-Result 'C' 'Direct static URL to evidence blocked' 'FAIL' 'Evidence file path unavailable; static URL test skipped.'
        }

        # Delete evidence
        $evDelGet = $adminCtx.Client.GetAsync("/Evidencias/Delete/$smokeEvidenceId").GetAwaiter().GetResult()
        $evDelHtml = Get-Body $evDelGet
        $evDelToken = Get-SmokeToken $evDelHtml
        $evDelForm = @{ '__RequestVerificationToken' = $evDelToken }
        $evDelContent = New-FormContent $evDelForm
        $evDelPost = $adminCtx.Client.PostAsync("/Evidencias/Delete/$smokeEvidenceId", $evDelContent).GetAwaiter().GetResult()
        $evDelContent.Dispose()
        if ([int]$evDelPost.StatusCode -eq 302) {
            Add-Result 'C' 'Delete evidencia' 'PASS' "Redirect=$((Get-LocationValue $evDelPost))"
        } else {
            Add-Result 'C' 'Delete evidencia' 'FAIL' "HTTP $([int]$evDelPost.StatusCode)"
        }

        if ($smokeEvidenceFilePath) {
            if (-not (Test-Path $smokeEvidenceFilePath)) {
                Add-Result 'C' 'Evidence file removed from disk' 'PASS' 'Original jpg file removed after delete.'
            } else {
                Add-Result 'C' 'Evidence file removed from disk' 'FAIL' 'Original jpg file still exists after delete.'
            }
        }

        # Delete case
        $caseDelGet = $adminCtx.Client.GetAsync("/Casos/Delete/$smokeCaseId").GetAwaiter().GetResult()
        $caseDelHtml = Get-Body $caseDelGet
        $caseDelToken = Get-SmokeToken $caseDelHtml
        $caseDelForm = @{ '__RequestVerificationToken' = $caseDelToken }
        $caseDelContent = New-FormContent $caseDelForm
        $caseDelPost = $adminCtx.Client.PostAsync("/Casos/Delete/$smokeCaseId", $caseDelContent).GetAwaiter().GetResult()
        $caseDelContent.Dispose()
        if ([int]$caseDelPost.StatusCode -eq 302) {
            Add-Result 'C' 'Delete caso' 'PASS' "Redirect=$((Get-LocationValue $caseDelPost))"
        } else {
            Add-Result 'C' 'Delete caso' 'FAIL' "HTTP $([int]$caseDelPost.StatusCode)"
        }

        # Delete oficio
        $ofDelGet = $adminCtx.Client.GetAsync("/Oficios/Delete/$smokeOficioId").GetAwaiter().GetResult()
        $ofDelHtml = Get-Body $ofDelGet
        $ofDelToken = Get-SmokeToken $ofDelHtml
        $ofDelForm = @{ '__RequestVerificationToken' = $ofDelToken }
        $ofDelContent = New-FormContent $ofDelForm
        $ofDelPost = $adminCtx.Client.PostAsync("/Oficios/Delete/$smokeOficioId", $ofDelContent).GetAwaiter().GetResult()
        $ofDelContent.Dispose()
        if ([int]$ofDelPost.StatusCode -eq 302) {
            Add-Result 'C' 'Delete oficio' 'PASS' "Redirect=$((Get-LocationValue $ofDelPost))"
        } else {
            Add-Result 'C' 'Delete oficio' 'FAIL' "HTTP $([int]$ofDelPost.StatusCode)"
        }

        $ofFolder = Join-Path $StorageRoot $smokeOficioNumber
        if (-not (Test-Path $ofFolder)) {
            Add-Result 'C' 'Oficio folder removed from disk' 'PASS' $ofFolder
        } else {
            Add-Result 'C' 'Oficio folder removed from disk' 'FAIL' $ofFolder
        }
    }
    catch {
        Add-Result 'C' 'CRUD execution' 'FAIL' $_.Exception.Message
    }
}

# Block D: Audit log checks
if ($null -ne $adminCtx) {
    try {
        $auditChecks = @(
            @{ Event='LOGIN'; Username='admin'; Label='Admin LOGIN logged' },
            @{ Event='LOGIN_FAIL'; Username=$smokeUsername; Label='Smoke user LOGIN_FAIL logged' },
            @{ Event='LOCKOUT'; Username=$smokeUsername; Label='Smoke user LOCKOUT logged' },
            @{ Event='CREATE'; Username='admin'; Label='CREATE logged' },
            @{ Event='UPDATE'; Username='admin'; Label='UPDATE logged' },
            @{ Event='DELETE'; Username='admin'; Label='DELETE logged' },
            @{ Event='UPLOAD_EVIDENCE'; Username='admin'; Label='UPLOAD_EVIDENCE logged' },
            @{ Event='DELETE_EVIDENCE'; Username='admin'; Label='DELETE_EVIDENCE logged' },
            @{ Event='VIEW_EVIDENCE'; Username='admin'; Label='VIEW_EVIDENCE logged' }
        )

        foreach ($q in $auditChecks) {
            $url = "/Admin/AuditLog?Days=2&EventType=$($q.Event)&Username=$([Uri]::EscapeDataString($q.Username))"
            $resp = $adminCtx.Client.GetAsync($url).GetAwaiter().GetResult()
            $html = Get-Body $resp
            if ($html -like '*Sin eventos para los filtros seleccionados*') {
                Add-Result 'D' $q.Label 'FAIL' "No rows for EventType=$($q.Event), Username=$($q.Username)"
            } else {
                Add-Result 'D' $q.Label 'PASS' "Rows found for EventType=$($q.Event), Username=$($q.Username)"
            }
        }
    }
    catch {
        Add-Result 'D' 'Audit log execution' 'FAIL' $_.Exception.Message
    }
}

# Block E: Logout and post-logout protection
if ($null -ne $adminCtx) {
    try {
        $indexResp = $adminCtx.Client.GetAsync('/Index').GetAwaiter().GetResult()
        $indexHtml = Get-Body $indexResp
        $logoutToken = Get-SmokeToken $indexHtml
        if ([string]::IsNullOrWhiteSpace($logoutToken)) {
            throw 'Could not parse logout antiforgery token from /Index.'
        }

        $logoutForm = @{ '__RequestVerificationToken' = $logoutToken }
        $logoutContent = New-FormContent $logoutForm
        $logoutResp = $adminCtx.Client.PostAsync('/Logout', $logoutContent).GetAwaiter().GetResult()
        $logoutContent.Dispose()

        if ([int]$logoutResp.StatusCode -eq 302 -and (Get-LocationValue $logoutResp) -like '*Login*') {
            Add-Result 'E' 'Logout request' 'PASS' "HTTP 302 => $((Get-LocationValue $logoutResp))"
        } else {
            Add-Result 'E' 'Logout request' 'FAIL' "HTTP $([int]$logoutResp.StatusCode)"
        }

        $afterLogout = $adminCtx.Client.GetAsync('/Oficios').GetAwaiter().GetResult()
        if ([int]$afterLogout.StatusCode -eq 302 -and (Get-LocationValue $afterLogout) -like '*Login*') {
            Add-Result 'E' 'Post-logout protected route' 'PASS' "HTTP 302 => $((Get-LocationValue $afterLogout))"
        } else {
            Add-Result 'E' 'Post-logout protected route' 'FAIL' "HTTP $([int]$afterLogout.StatusCode)"
        }

        $loginAgainGet = $adminCtx.Client.GetAsync('/Login').GetAwaiter().GetResult()
        $loginAgainHtml = Get-Body $loginAgainGet
        $loginAgainToken = Get-SmokeToken $loginAgainHtml
        if ([string]::IsNullOrWhiteSpace($loginAgainToken)) {
            throw 'Could not parse login token when validating logout audit.'
        }

        $loginAgainForm = @{
            '__RequestVerificationToken' = $loginAgainToken
            'Input.Usuario' = $AdminUser
            'Input.Contrasena' = $activeAdminPass
        }

        $loginAgainContent = New-FormContent $loginAgainForm
        $loginAgainResp = $adminCtx.Client.PostAsync('/Login', $loginAgainContent).GetAwaiter().GetResult()
        $loginAgainContent.Dispose()

        if ([int]$loginAgainResp.StatusCode -eq 302 -and (Get-LocationValue $loginAgainResp) -notlike '*Login*') {
            $logoutAuditResp = $adminCtx.Client.GetAsync('/Admin/AuditLog?Days=2&EventType=LOGOUT&Username=admin').GetAwaiter().GetResult()
            $logoutAuditHtml = Get-Body $logoutAuditResp
            if ($logoutAuditHtml -like '*Sin eventos para los filtros seleccionados*') {
                Add-Result 'D' 'Admin LOGOUT logged' 'FAIL' 'No rows found for LOGOUT/admin.'
            } else {
                Add-Result 'D' 'Admin LOGOUT logged' 'PASS' 'Rows found for LOGOUT/admin.'
            }
        } else {
            Add-Result 'D' 'Admin LOGOUT logged' 'FAIL' 'Could not re-login admin to validate logout event.'
        }
    }
    catch {
        Add-Result 'E' 'Logout execution' 'FAIL' $_.Exception.Message
    }
}

# Block F: Session timeout runtime
Add-Result 'F' '30-min idle timeout runtime test' 'SKIP' 'Not executed automatically (requires waiting >30 minutes). Configuration remains at 30 min.'

if ($null -ne $adminCtx) {
    $adminCtx.Client.Dispose()
}

if ($null -ne $roleRevocationCtx) {
    $roleRevocationCtx.Client.Dispose()
}

$reportPath = Write-SmokeReport -Results $results

$results | Format-Table -AutoSize
Write-Output "REPORT_PATH=$reportPath"


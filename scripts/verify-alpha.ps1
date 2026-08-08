param(
    [string]$BaseUrl = "http://127.0.0.1:5101",
    [switch]$LocalProductionHttp
)

$ErrorActionPreference = "Stop"
Import-Module Microsoft.PowerShell.Utility
$passed = 0
$failed = 0

function Assert-True([bool]$Condition, [string]$Name) {
    if ($Condition) {
        $script:passed++
        Write-Host "PASS $Name" -ForegroundColor Green
        return
    }
    $script:failed++
    Write-Host "FAIL $Name" -ForegroundColor Red
}

function Invoke-Api {
    param(
        [object]$Session,
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [int[]]$Expected = @(200)
    )

    $parameters = @{
        Uri = "$BaseUrl$Path"
        Method = $Method
        WebSession = $Session
        Headers = @{ "X-Permut-STIB" = "app"; "X-Forwarded-Proto" = "https" }
        UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 8 -Compress
    }

    $content = ""
    try {
        $response = Invoke-WebRequest @parameters
        $status = [int]$response.StatusCode
        $content = $response.Content
        if ($LocalProductionHttp -and $response.Headers["Set-Cookie"]) {
            $pair = $response.Headers["Set-Cookie"].Split(';')[0].Split('=', 2)
            if ($pair.Count -eq 2) {
                $uri = [Uri]$BaseUrl
                $Session.Cookies.Add($uri, (New-Object System.Net.Cookie($pair[0], $pair[1], "/", $uri.Host)))
            }
        }
    }
    catch [System.Net.WebException] {
        $response = $_.Exception.Response
        $status = [int]$response.StatusCode
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    Assert-True ($Expected -contains $status) "$Method $Path retourne $($Expected -join '/') (reçu $status)"
    $data = $null
    if (-not [string]::IsNullOrWhiteSpace($content)) {
        try { $data = $content | ConvertFrom-Json } catch { }
    }
    [pscustomobject]@{ Status = $status; Data = $data; Raw = $content }
}

$anonymous = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$admin = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$agentA = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$agentB = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$agentC = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$suffix = Get-Random -Minimum 100000 -Maximum 899998
$matriculeA = "QA-$suffix-A"
$matriculeB = "QA-$suffix-B"
$matriculeDuplicate = "QA-$suffix-C"
$phoneA = "+32471$($suffix.ToString('000000'))"
$phoneB = "+32471$(($suffix + 1).ToString('000000'))"
$password = "test1234"

$health = Invoke-Api $anonymous GET "/healthz"
Assert-True ($health.Data.status -eq "healthy") "La base de données répond au health check"

Invoke-Api $anonymous GET "/api/auth/me" -Expected @(401) | Out-Null
$registrationA = Invoke-Api $anonymous POST "/api/auth/register" @{ matricule = $matriculeA; phoneNumber = $phoneA; password = $password } @(202)
$registrationB = Invoke-Api $anonymous POST "/api/auth/register" @{ matricule = $matriculeB; phoneNumber = $phoneB; password = $password } @(202)
Invoke-Api $anonymous POST "/api/auth/register" @{ matricule = $matriculeDuplicate; phoneNumber = $phoneA; password = $password } @(409) | Out-Null
Invoke-Api $agentA POST "/api/auth/login" @{ identifier = $matriculeA; password = $password } @(401) | Out-Null

$adminLogin = Invoke-Api $admin POST "/api/auth/login" @{ identifier = "DELEGUE"; password = $password }
Assert-True ($adminLogin.Data.role -eq "Admin") "Le compte délégué reçoit le rôle Admin"
$agentsBefore = Invoke-Api $admin GET "/api/admin/agents"
$pendingA = @($agentsBefore.Data) | Where-Object matricule -eq $matriculeA
$pendingB = @($agentsBefore.Data) | Where-Object matricule -eq $matriculeB
Assert-True ($pendingA.status -eq "Pending" -and $pendingB.status -eq "Pending") "Les nouveaux agents sont en attente"
Assert-True ($pendingA.phoneNumber -eq $phoneA) "Le GSM est visible pour l'administrateur"

Invoke-Api $admin POST "/api/admin/agents/$($pendingA.id)/status" @{ status = "Active"; reason = "Vérification alpha automatisée" } @(204) | Out-Null
Invoke-Api $admin POST "/api/admin/agents/$($pendingB.id)/status" @{ status = "Active"; reason = "Vérification alpha automatisée" } @(204) | Out-Null
$loginA = Invoke-Api $agentA POST "/api/auth/login" @{ identifier = $matriculeA; password = $password }
$loginB = Invoke-Api $agentB POST "/api/auth/login" @{ identifier = $phoneB; password = $password }
$loginC = Invoke-Api $agentC POST "/api/auth/login" @{ identifier = "70-003"; password = $password }
Assert-True ($loginA.Data.role -eq "Agent" -and $loginB.Data.role -eq "Agent") "Connexion agent par matricule et par GSM"
$meA = Invoke-Api $agentA GET "/api/auth/me"
Assert-True ($meA.Raw -notmatch '(?i)phone|gsm|\+324') "Le GSM n'est jamais exposé dans la session agent"
Invoke-Api $agentA GET "/api/admin/summary" -Expected @(403) | Out-Null

$start = (Get-Date).Date.AddYears(3).AddDays(($suffix % 120) + 1)
$ownedFrom = $start.ToString("yyyy-MM-dd")
$ownedTo = $start.AddDays(5).ToString("yyyy-MM-dd")
$wantedFrom = $start.AddDays(30).ToString("yyyy-MM-dd")
$wantedTo = $start.AddDays(35).ToString("yyyy-MM-dd")
$permutation = Invoke-Api $agentA POST "/api/permutations" @{
    ownedPeriod = @{ from = $ownedFrom; to = $ownedTo }
    wantedPeriod = @{ from = $wantedFrom; to = $wantedTo }
}
Assert-True ($permutation.Data.status -eq "Open") "Une permutation est créée ouverte"
$permutationId = $permutation.Data.id
$availableForB = Invoke-Api $agentB GET "/api/permutations/available"
Assert-True (@($availableForB.Data).id -contains $permutationId) "La permutation est visible par un autre agent"
Invoke-Api $agentA POST "/api/permutations/$permutationId/proposals" @{ from = $wantedFrom; to = $wantedTo } @(409) | Out-Null
$proposalResult = Invoke-Api $agentB POST "/api/permutations/$permutationId/proposals" @{ from = $wantedFrom; to = $wantedTo }
Assert-True ($proposalResult.Data.status -eq "ProposalReceived") "Un agent peut proposer la période recherchée"
$proposalId = @($proposalResult.Data.proposals) | Where-Object partnerId -eq $loginB.Data.id | Select-Object -ExpandProperty id
$availableForC = Invoke-Api $agentC GET "/api/permutations/available"
Assert-True (@($availableForC.Data).id -contains $permutationId) "La demande reste visible après une première proposition"
$secondProposal = Invoke-Api $agentC POST "/api/permutations/$permutationId/proposals" @{ from = $wantedFrom; to = $wantedTo }
Assert-True (@($secondProposal.Data.proposals).Count -eq 2) "Plusieurs agents peuvent proposer leur période"
$notificationsA = Invoke-Api $agentA GET "/api/notifications"
Assert-True (@($notificationsA.Data).type -contains "PermutationProposalReceived") "Le demandeur reçoit la notification de proposition"
Invoke-Api $agentB POST "/api/permutations/$permutationId/proposals/$proposalId/accept" -Expected @(403) | Out-Null
$accepted = Invoke-Api $agentA POST "/api/permutations/$permutationId/proposals/$proposalId/accept"
Assert-True ($accepted.Data.status -eq "Accepted") "Seul le demandeur accepte la proposition"
Assert-True (@($accepted.Data.proposals | Where-Object status -eq "Rejected").Count -eq 1) "Les autres propositions sont refusées après acceptation"
$confirmedA = Invoke-Api $agentA POST "/api/permutations/$permutationId/confirm"
Assert-True ($confirmedA.Data.status -eq "Confirmed") "La première confirmation attend le second agent"
$confirmedB = Invoke-Api $agentB POST "/api/permutations/$permutationId/confirm"
Assert-True ($confirmedB.Data.status -eq "Locked") "La deuxième confirmation verrouille la permutation"
Invoke-Api $agentA POST "/api/permutations/$permutationId/cancel" -Expected @(409) | Out-Null
$notificationsB = Invoke-Api $agentB GET "/api/notifications"
Assert-True (@($notificationsB.Data).type -contains "PermutationProposalAccepted") "Le partenaire est notifié de l'acceptation"
Assert-True (@($notificationsB.Data).type -contains "PermutationLocked") "Le partenaire est notifié du verrouillage"

$signatureDate = $start.AddDays(75).ToString("yyyy-MM-dd")
$availability = Invoke-Api $agentB POST "/api/signatures/availabilities" @{ serviceDate = $signatureDate; comment = "Disponible pour aider" }
Assert-True ($availability.Data.isActive -eq $true -and $availability.Data.serviceDate -eq $signatureDate) "Un agent peut proposer un jour de signature à l'avance"
$myAvailabilities = Invoke-Api $agentB GET "/api/signatures/availabilities/mine"
Assert-True (@($myAvailabilities.Data).id -contains $availability.Data.id) "Le jour proposé apparaît dans ses disponibilités"
$signature = Invoke-Api $agentA POST "/api/signatures" @{ serviceDate = $signatureDate; comment = "Test complet alpha" }
Assert-True ($signature.Data.status -eq "ProposalReceived") "La demande détecte immédiatement le collègue disponible"
$signatureId = $signature.Data.id
$automaticOffer = @($signature.Data.offers) | Where-Object { $_.signerId -eq $loginB.Data.id -and $_.availabilityId -eq $availability.Data.id }
Assert-True ($null -ne $automaticOffer) "Une proposition proactive est créée automatiquement"
$availableSignatures = Invoke-Api $agentB GET "/api/signatures/available"
Assert-True (@($availableSignatures.Data).id -notcontains $signatureId) "Une demande déjà associée n'est pas reproposée au même agent"
Invoke-Api $agentA POST "/api/signatures/$signatureId/offers" -Expected @(409) | Out-Null
$offerId = $automaticOffer.id
$signatureNotificationsA = Invoke-Api $agentA GET "/api/notifications"
$signatureNotificationsB = Invoke-Api $agentB GET "/api/notifications"
Assert-True (@($signatureNotificationsA.Data).type -contains "SignatureAvailabilityMatched") "Le demandeur est notifié du collègue disponible"
Assert-True (@($signatureNotificationsB.Data).type -contains "SignatureRequestMatched") "Le collègue disponible est notifié de la demande correspondante"
Invoke-Api $agentB POST "/api/signatures/$signatureId/offers/$offerId/confirm" -Expected @(403) | Out-Null
$lockedSignature = Invoke-Api $agentA POST "/api/signatures/$signatureId/offers/$offerId/confirm"
Assert-True ($lockedSignature.Data.status -eq "Locked" -and $lockedSignature.Data.signerId -eq $loginB.Data.id) "Le demandeur choisit et verrouille le signataire"
Invoke-Api $agentA POST "/api/signatures/$signatureId/cancel" -Expected @(409) | Out-Null
$myAvailabilitiesAfterMatch = Invoke-Api $agentB GET "/api/signatures/availabilities/mine"
$usedAvailability = @($myAvailabilitiesAfterMatch.Data) | Where-Object id -eq $availability.Data.id
Assert-True ($usedAvailability.isActive -eq $false) "La disponibilité utilisée est automatiquement clôturée"
$signatureNotificationsB = Invoke-Api $agentB GET "/api/notifications"
Assert-True (@($signatureNotificationsB.Data).type -contains "SignatureOfferAccepted") "Le signataire choisi reçoit sa notification"

$cancelDate = $start.AddDays(76).ToString("yyyy-MM-dd")
$cancelAvailability = Invoke-Api $agentA POST "/api/signatures/availabilities" @{ serviceDate = $cancelDate; comment = "Disponibilité à retirer" }
Invoke-Api $agentA POST "/api/signatures/availabilities/$($cancelAvailability.Data.id)/cancel" -Expected @(204) | Out-Null
$cancelledAvailabilities = Invoke-Api $agentA GET "/api/signatures/availabilities/mine"
$cancelledAvailability = @($cancelledAvailabilities.Data) | Where-Object id -eq $cancelAvailability.Data.id
Assert-True ($cancelledAvailability.isActive -eq $false) "Un agent peut retirer une disponibilité proposée"

Invoke-Api $agentA POST "/api/notifications/read-all" -Expected @(204) | Out-Null
$unreadA = Invoke-Api $agentA GET "/api/notifications?unreadOnly=true"
Assert-True (@($unreadA.Data).Count -eq 0) "Toutes les notifications peuvent être marquées comme lues"
$summary = Invoke-Api $admin GET "/api/admin/summary"
$audit = Invoke-Api $admin GET "/api/admin/audit"
Assert-True ($summary.Data.activeAgents -ge 2) "Le tableau de bord admin calcule les agents actifs"
Assert-True (@($audit.Data).entityId -contains $permutationId) "Le journal d'audit contient la permutation testée"

Invoke-Api $admin POST "/api/admin/agents/$($pendingB.id)/status" @{ status = "Suspended"; reason = "Test d'invalidation de session" } @(204) | Out-Null
Invoke-Api $agentB GET "/api/signatures/mine" -Expected @(401) | Out-Null
Invoke-Api $admin POST "/api/admin/agents/$($pendingB.id)/status" @{ status = "Active"; reason = "Fin de la vérification alpha" } @(204) | Out-Null

$withoutHeaderStatus = [int](& curl.exe -s -o NUL -w "%{http_code}" -X POST -H "X-Forwarded-Proto: https" "$BaseUrl/api/auth/logout")
Assert-True ($withoutHeaderStatus -eq 400) "Une écriture sans en-tête applicatif est refusée"
Invoke-Api $agentA POST "/api/auth/logout" -Expected @(204) | Out-Null
Invoke-Api $agentA GET "/api/auth/me" -Expected @(401) | Out-Null

Write-Host "RESULTAT: $passed réussites, $failed échecs" -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }

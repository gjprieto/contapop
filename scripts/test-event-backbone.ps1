param(
    [string]$AppHostProject = "src/Contapop.Application.AppHost/Contapop.Application.AppHost.csproj"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command dapr -ErrorAction SilentlyContinue)) {
    throw "The Dapr CLI must be installed and available on PATH."
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker must be available to inspect the Aspire-managed PostgreSQL database."
}

$appHost = Start-Process dotnet -ArgumentList "run --project `"$AppHostProject`"" -PassThru

try {
    $deadline = (Get-Date).AddMinutes(2)
    do {
        try {
            Invoke-WebRequest "http://localhost:5111/health" -UseBasicParsing | Out-Null
            Invoke-WebRequest "http://localhost:5113/health" -UseBasicParsing | Out-Null
            break
        }
        catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)

    if ((Get-Date) -ge $deadline) {
        throw "Identity and Ledger did not become healthy within two minutes."
    }

    $email = "event-backbone-$([Guid]::NewGuid())@contapop.test"
    $tenant = Invoke-RestMethod -Method Post -Uri "http://localhost:5111/api/v1/tenants" -ContentType "application/json" -Body (@{
        tenantName = "Event Backbone Test"
        ownerName = "Event Backbone Owner"
        ownerEmail = $email
        initialPassword = "CorrectHorseBattery1"
    } | ConvertTo-Json)

    $postgres = docker ps --format "{{.Names}}" | Where-Object { $_ -like "postgres-*" } | Select-Object -First 1
    if (-not $postgres) {
        throw "Could not locate the Aspire-managed PostgreSQL container."
    }

    $password = (docker exec $postgres printenv POSTGRES_PASSWORD).Trim()
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $replica = docker exec $postgres psql "postgresql://postgres:$password@localhost:5432/contapop_ledger" -t -A -c "select project_id from bank_accounts.project_replica where project_id = '$($tenant.projectId)'"
        if ($null -ne $replica -and $replica.Trim() -eq $tenant.projectId) {
            "Project $($tenant.projectId) replicated to Ledger."
            exit 0
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "Project $($tenant.projectId) did not replicate to Ledger within 30 seconds."
}
finally {
    if (-not $appHost.HasExited) {
        Stop-Process -Id $appHost.Id -Force
    }
}

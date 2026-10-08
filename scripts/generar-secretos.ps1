# Genera valores aleatorios seguros para Jwt__SigningKey y Hashing__Pepper.
# Uso local:  pwsh ./scripts/generar-secretos.ps1
# Con -UserSecrets los guarda en dotnet user-secrets (no quedan en el repositorio).
param([switch]$UserSecrets)

function Nuevo-Secreto {
    $bytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    [Convert]::ToBase64String($bytes)
}

$jwt = Nuevo-Secreto
$pepper = Nuevo-Secreto

if ($UserSecrets) {
    $proyecto = Join-Path $PSScriptRoot "..\src\KARider.API"
    dotnet user-secrets set "Jwt:SigningKey" $jwt --project $proyecto | Out-Null
    dotnet user-secrets set "Hashing:Pepper" $pepper --project $proyecto | Out-Null
    Write-Host "Secretos guardados en user-secrets de KARider.API."
}
else {
    Write-Host "Jwt__SigningKey=$jwt"
    Write-Host "Hashing__Pepper=$pepper"
    Write-Host ""
    Write-Host "Cópialos a tu .env (local) o a las variables de entorno de Azure App Service. No los subas a git."
}

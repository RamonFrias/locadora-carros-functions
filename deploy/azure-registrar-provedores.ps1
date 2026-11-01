<#
  Registra na assinatura os provedores de recursos usados pelo deploy (necessario uma unica vez em
  assinaturas novas; sem isso a Azure responde "SubscriptionNotFound" ao criar os recursos).
  Uso: powershell -ExecutionPolicy Bypass -File .\deploy\azure-registrar-provedores.ps1
#>
$ErrorActionPreference = 'Stop'
$az = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd'

foreach ($provedor in 'Microsoft.Storage', 'Microsoft.Web', 'Microsoft.Sql') {
    Write-Host "Registrando $provedor (pode levar alguns minutos)..."
    & $az provider register --namespace $provedor --wait --only-show-errors
    if ($LASTEXITCODE -ne 0) { throw "Falha ao registrar $provedor." }
}

& $az provider list --query "[?namespace=='Microsoft.Storage' || namespace=='Microsoft.Web' || namespace=='Microsoft.Sql'].{Provedor:namespace, Estado:registrationState}" -o table --only-show-errors

<#
  Compila e publica a versao Azure Functions no Function App criado por azure-provisionar.ps1.
  Uso: powershell -ExecutionPolicy Bypass -File .\deploy\azure-publicar.ps1
#>
$ErrorActionPreference = 'Stop'
$func = 'C:\Program Files\Microsoft\Azure Functions Core Tools\func.exe'
$config = Get-Content (Join-Path $PSScriptRoot 'azure.json') -Raw | ConvertFrom-Json
$projeto = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\LocadoraCarros.Functions'

Write-Host "Publicando em $($config.app) (o Core Tools compila em Release e envia o pacote)"
Push-Location $projeto
try {
    & $func azure functionapp publish $config.app --dotnet-isolated
    if ($LASTEXITCODE -ne 0) { throw 'Falha na publicacao.' }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Publicado: https://$($config.app).azurewebsites.net/api/scalar"
Write-Host "A primeira chamada depois de um tempo parado pode levar alguns segundos (cold start + banco acordando)."

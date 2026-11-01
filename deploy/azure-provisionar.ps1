<#
  Cria a infraestrutura da API em Azure Functions:
    - Storage Account (obrigatoria para o Functions; custo de centavos por mes)
    - Function App no plano Flex Consumption (cota gratuita mensal), .NET 10 isolated, 512 MB por instancia
    - Azure SQL Database com a oferta gratuita, que PAUSA ao esgotar a cota mensal (sem cobranca)
  Segredos (senha do SQL, chave JWT e senha dos usuarios de teste) sao gerados aqui e gravados
  direto nas configuracoes do Function App. Nenhum segredo e salvo em arquivo.
  Pode ser executado de novo: reaproveita os nomes de deploy/azure.json e os segredos ja configurados.

  Uso: powershell -ExecutionPolicy Bypass -File .\deploy\azure-provisionar.ps1
#>
param(
    [string]$Local = 'brazilsouth',
    [string]$GrupoRecursos = 'rg-locadora-carros'
)

$ErrorActionPreference = 'Stop'
$az = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd'
$arquivoConfig = Join-Path $PSScriptRoot 'azure.json'

function Invoke-Az {
    $saida = & $az @args --only-show-errors
    if ($LASTEXITCODE -ne 0) { throw "Falha no comando: az $($args[0]) $($args[1]) $($args[2])" }
    $saida
}

function Test-Az {
    # No PowerShell 5.1, stderr de comando nativo com 'Stop' vira excecao; aqui o erro e esperado.
    $ErrorActionPreference = 'Continue'
    & $az @args --only-show-errors -o none 2>$null
    $LASTEXITCODE -eq 0
}

function New-Segredo([int]$tamanho) {
    $caracteres = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $bytes = New-Object byte[] $tamanho
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    -join ($bytes | ForEach-Object { $caracteres[$_ % $caracteres.Length] })
}

# Nomes globais precisam ser unicos na Azure: usamos um sufixo aleatorio.
if (Test-Path $arquivoConfig) {
    $config = Get-Content $arquivoConfig -Raw | ConvertFrom-Json
} else {
    $sufixo = (New-Segredo 5).ToLowerInvariant()
    $config = [pscustomobject]@{
        grupoRecursos = $GrupoRecursos
        local         = $Local
        storage       = "stlocadorafn$sufixo"
        app           = "locadora-carros-fn-$sufixo"
        sqlServer     = "locadora-carros-fn-sql-$sufixo"
        banco         = 'locadora_carros'
        sqlAdmin      = 'locadoraadmin'
    }
    $config | ConvertTo-Json | Set-Content $arquivoConfig -Encoding ASCII
}

Write-Host "1/6 Grupo de recursos $($config.grupoRecursos) em $($config.local)"
Invoke-Az group create -n $config.grupoRecursos -l $config.local -o none

Write-Host "2/6 Storage Account $($config.storage)"
if (-not (Test-Az storage account show -g $config.grupoRecursos -n $config.storage)) {
    Invoke-Az storage account create -g $config.grupoRecursos -n $config.storage -l $config.local `
        --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 --allow-blob-public-access false -o none
}

Write-Host "3/6 Function App $($config.app) (Flex Consumption, .NET 10 isolated)"
if (-not (Test-Az functionapp show -g $config.grupoRecursos -n $config.app)) {
    Invoke-Az functionapp create -g $config.grupoRecursos -n $config.app --storage-account $config.storage `
        --flexconsumption-location $config.local --runtime dotnet-isolated --runtime-version 10 `
        --instance-memory 512 --disable-app-insights -o none
}

Write-Host "4/6 Servidor Azure SQL $($config.sqlServer)"
$senhaSql = (New-Segredo 22) + 'a7K'
if (Test-Az sql server show -g $config.grupoRecursos -n $config.sqlServer) {
    Invoke-Az sql server update -g $config.grupoRecursos -n $config.sqlServer -p $senhaSql -o none
} else {
    Invoke-Az sql server create -g $config.grupoRecursos -n $config.sqlServer -l $config.local -u $config.sqlAdmin -p $senhaSql -o none
}
# Libera apenas servicos da propria Azure (o Function App); nenhum IP externo tem acesso.
Invoke-Az sql server firewall-rule create -g $config.grupoRecursos -s $config.sqlServer -n PermitirServicosAzure --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 -o none

Write-Host "5/6 Banco $($config.banco) com a oferta gratuita (pausa ao esgotar a cota)"
if (-not (Test-Az sql db show -g $config.grupoRecursos -s $config.sqlServer -n $config.banco)) {
    Invoke-Az sql db create -g $config.grupoRecursos -s $config.sqlServer -n $config.banco `
        -e GeneralPurpose --compute-model Serverless -f Gen5 -c 2 `
        --use-free-limit --free-limit-exhaustion-behavior AutoPause --backup-storage-redundancy Local -o none
}

Write-Host "6/6 Configuracoes da aplicacao"
$existentes = @{}
(Invoke-Az functionapp config appsettings list -g $config.grupoRecursos -n $config.app -o json | Out-String | ConvertFrom-Json) |
    ForEach-Object { $existentes[$_.name] = $_.value }

$chaveJwt = if ($existentes['Jwt__ChaveSecreta']) { $existentes['Jwt__ChaveSecreta'] } else { New-Segredo 64 }
$senhaSeed = if ($existentes['Seed__SenhaPadrao']) { $existentes['Seed__SenhaPadrao'] } else { (New-Segredo 10) + '7k' }
$conexao = "Server=tcp:$($config.sqlServer).database.windows.net,1433;Initial Catalog=$($config.banco);User ID=$($config.sqlAdmin);Password=$senhaSql;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"

Invoke-Az functionapp config appsettings set -g $config.grupoRecursos -n $config.app -o none --settings `
    "Database__Provider=SqlServer" `
    "ConnectionStrings__Locadora=$conexao" `
    "Jwt__ChaveSecreta=$chaveJwt" `
    "Seed__Habilitado=true" `
    "Seed__SenhaPadrao=$senhaSeed"

Write-Host ""
Write-Host "Infraestrutura pronta. Publique com: powershell -ExecutionPolicy Bypass -File .\deploy\azure-publicar.ps1"
Write-Host "API:          https://$($config.app).azurewebsites.net/api"
Write-Host "Documentacao: https://$($config.app).azurewebsites.net/api/scalar"
Write-Host "Senha dos usuarios de teste (gerente, funcionario, cliente @locadora.dev): $senhaSeed"

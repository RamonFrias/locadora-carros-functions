# Locadora de Carros — Azure Functions (.NET 10)

Mesma API da [versão Web API](../LocadoraCarros), agora hospedada em **Azure Functions** (modelo isolated worker, .NET 10). Rotas, contratos JSON, regras de negócio e permissões são idênticos. Um cliente que fala com a Web API fala com esta versão trocando apenas o endereço.

## O que mudou em relação à Web API

Domínio, aplicação e infraestrutura são os mesmos projetos (Clean Architecture). Só a camada de entrada foi trocada:

| Web API (ASP.NET Core) | Azure Functions |
|---|---|
| Controllers | Classes em `Funcoes/`, uma função HTTP por rota (46 funções) |
| `[Authorize(Policy = ...)]` / `[AllowAnonymous]` | `[Acesso(Politica.Equipe)]`, `Publico`, `Logado`, `Gerente`; sem atributo exige login |
| Middleware JwtBearer do ASP.NET | `AutenticacaoMiddleware` + `JwtTokenValidator` |
| `IExceptionHandler` + Problem Details | `TratamentoDeErrosMiddleware`, mesmos status e formato |
| Model binding e validação automática | `req.LerCorpoAsync<T>()` e `req.LerQuery<T>()`, com os mesmos atributos de validação |
| `appsettings.json` | `local.settings.json` (local) e configurações do Function App (Azure) |
| Rate limit de login | Não disponível nas Functions; use Azure API Management ou Front Door na frente |
| Scalar em `/scalar/v1` | Scalar em `/api/scalar` (reaproveita a especificação OpenAPI da Web API) |

## Como rodar

Pré-requisitos: .NET SDK 10, [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) e MySQL 8.

1. Copie `src/LocadoraCarros.Functions/local.settings.example.json` para `local.settings.json` e preencha os valores. Esse arquivo fica fora do Git.
2. A connection string vem dos **mesmos user-secrets da Web API** (mesmo `UserSecretsId`). Se ainda não configurou:
   ```bash
   dotnet user-secrets set "ConnectionStrings:Locadora" "Server=localhost;Port=3306;Database=locadora_carros;Uid=root;Pwd=<sua senha>;Persist Security Info=true;" --project src/LocadoraCarros.Functions
   ```
3. Rode:
   ```bash
   cd src/LocadoraCarros.Functions
   func start
   ```

Endereços:

- API: http://localhost:7071/api
- Documentação (Scalar): http://localhost:7071/api/scalar
- OpenAPI: http://localhost:7071/api/openapi/v1.json

As duas versões usam o **mesmo banco** e a **mesma chave JWT de desenvolvimento**. Os usuários de teste são os mesmos, e um token gerado numa versão vale na outra.

## Estrutura

```
src/
  LocadoraCarros.Domain          (igual à Web API)
  LocadoraCarros.Application     (igual à Web API)
  LocadoraCarros.Infrastructure  (igual; JWT validado por JwtTokenValidator em vez do pipeline do ASP.NET)
  LocadoraCarros.Functions
    Program.cs                   Host isolated worker com integração ASP.NET Core
    Funcoes/                     Funções HTTP por recurso (Auth, Conta, Clientes, Funcionários, Carros, Lojas, Locações, Documentação)
    Http/                        Middlewares de autenticação e erros, leitura de corpo e query, respostas JSON
    OpenApi/openapi.json         Especificação exportada da Web API
    host.json                    Prefixo de rota "api" e logs
tests/
  LocadoraCarros.Domain.Tests    33 testes de domínio
```

## Testes

```bash
dotnet test
```

O teste HTTP de ponta a ponta (44 cenários) também passa contra esta versão.

## Manutenção

Os projetos Domain, Application e Infrastructure são **cópias** dos da Web API. Uma mudança de regra precisa ser feita nas duas pastas. Se as duas versões forem evoluir juntas, vale unificar numa só solução, com os dois hosts (Api e Functions) referenciando as mesmas camadas.

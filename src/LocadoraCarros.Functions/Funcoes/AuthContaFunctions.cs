using LocadoraCarros.Application.Auth;
using LocadoraCarros.Application.Clientes;
using LocadoraCarros.Application.Contas;
using LocadoraCarros.Functions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace LocadoraCarros.Functions.Funcoes;

/// <summary>Autenticação (bearer token) e auto-cadastro de clientes.</summary>
[Acesso(Politica.Publico)]
public sealed class AuthFunctions(AuthService auth, ClienteService clientes)
{
    /// <summary>Gera o bearer token. O login pode ser o e-mail ou o CPF.</summary>
    [Function("Auth_Login")]
    public async Task<IActionResult> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await auth.LoginAsync(await req.LerCorpoAsync<LoginRequest>(), ct));

    /// <summary>Cadastro público de um novo cliente.</summary>
    [Function("Auth_Registrar")]
    public async Task<IActionResult> Registrar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/registro")] HttpRequest req, CancellationToken ct) =>
        Resposta.Criado("/api/conta", await clientes.CriarAsync(await req.LerCorpoAsync<CriarClienteRequest>(), ct));
}

/// <summary>Cadastro do usuário logado.</summary>
[Acesso(Politica.Logado)]
public sealed class ContaFunctions(ContaService conta)
{
    [Function("Conta_Obter")]
    public async Task<IActionResult> Obter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "conta")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await conta.ObterAsync(ct));

    /// <summary>Clientes alteram todos os dados, exceto o CPF. Funcionários recebem 403 (só podem trocar a senha).</summary>
    [Function("Conta_Atualizar")]
    public async Task<IActionResult> Atualizar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "conta")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await conta.AtualizarAsync(await req.LerCorpoAsync<AtualizarMeuCadastroRequest>(), ct));

    [Function("Conta_AlterarSenha")]
    public async Task<IActionResult> AlterarSenha(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "conta/senha")] HttpRequest req, CancellationToken ct)
    {
        await conta.AlterarSenhaAsync(await req.LerCorpoAsync<AlterarSenhaRequest>(), ct);
        return Resposta.SemConteudo();
    }
}

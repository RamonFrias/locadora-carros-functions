using LocadoraCarros.Application.Clientes;
using LocadoraCarros.Application.Funcionarios;
using LocadoraCarros.Functions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace LocadoraCarros.Functions.Funcoes;

/// <summary>Gestão de clientes pela equipe (funcionários e gerentes).</summary>
[Acesso(Politica.Equipe)]
public sealed class ClientesFunctions(ClienteService clientes)
{
    [Function("Clientes_Listar")]
    public async Task<IActionResult> Listar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clientes")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await clientes.ListarAsync(req.LerQuery<FiltroClientesRequest>(), ct));

    [Function("Clientes_Obter")]
    public async Task<IActionResult> Obter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clientes/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await clientes.ObterAsync(id, ct));

    [Function("Clientes_Criar")]
    public async Task<IActionResult> Criar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clientes")] HttpRequest req, CancellationToken ct)
    {
        var cliente = await clientes.CriarAsync(await req.LerCorpoAsync<CriarClienteRequest>(), ct);
        return Resposta.Criado($"/api/clientes/{cliente.Id}", cliente);
    }

    /// <summary>Atualização completa, inclusive correção de CPF.</summary>
    [Function("Clientes_Atualizar")]
    public async Task<IActionResult> Atualizar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "clientes/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await clientes.AtualizarAsync(id, await req.LerCorpoAsync<AtualizarClienteRequest>(), ct));

    [Function("Clientes_RedefinirSenha")]
    public async Task<IActionResult> RedefinirSenha(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "clientes/{id:guid}/senha")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await clientes.RedefinirSenhaAsync(id, await req.LerCorpoAsync<RedefinirSenhaRequest>(), ct);
        return Resposta.SemConteudo();
    }

    /// <summary>Exclusão lógica (o histórico de locações é preservado).</summary>
    [Function("Clientes_Excluir")]
    public async Task<IActionResult> Excluir(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "clientes/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await clientes.ExcluirAsync(id, ct);
        return Resposta.SemConteudo();
    }

    [Function("Clientes_Reativar")]
    public async Task<IActionResult> Reativar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clientes/{id:guid}/reativacao")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await clientes.ReativarAsync(id, ct);
        return Resposta.SemConteudo();
    }
}

/// <summary>Gestão de funcionários e gerentes: exclusiva do gerente.</summary>
[Acesso(Politica.Gerente)]
public sealed class FuncionariosFunctions(FuncionarioService funcionarios)
{
    [Function("Funcionarios_Listar")]
    public async Task<IActionResult> Listar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "funcionarios")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await funcionarios.ListarAsync(req.LerQuery<FiltroFuncionariosRequest>(), ct));

    [Function("Funcionarios_Obter")]
    public async Task<IActionResult> Obter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "funcionarios/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await funcionarios.ObterAsync(id, ct));

    [Function("Funcionarios_Criar")]
    public async Task<IActionResult> Criar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "funcionarios")] HttpRequest req, CancellationToken ct)
    {
        var funcionario = await funcionarios.CriarAsync(await req.LerCorpoAsync<CriarFuncionarioRequest>(), ct);
        return Resposta.Criado($"/api/funcionarios/{funcionario.Id}", funcionario);
    }

    [Function("Funcionarios_Atualizar")]
    public async Task<IActionResult> Atualizar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "funcionarios/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await funcionarios.AtualizarAsync(id, await req.LerCorpoAsync<AtualizarFuncionarioRequest>(), ct));

    [Function("Funcionarios_RedefinirSenha")]
    public async Task<IActionResult> RedefinirSenha(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "funcionarios/{id:guid}/senha")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await funcionarios.RedefinirSenhaAsync(id, await req.LerCorpoAsync<RedefinirSenhaRequest>(), ct);
        return Resposta.SemConteudo();
    }

    [Function("Funcionarios_Excluir")]
    public async Task<IActionResult> Excluir(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "funcionarios/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await funcionarios.ExcluirAsync(id, ct);
        return Resposta.SemConteudo();
    }

    [Function("Funcionarios_Reativar")]
    public async Task<IActionResult> Reativar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "funcionarios/{id:guid}/reativacao")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await funcionarios.ReativarAsync(id, ct);
        return Resposta.SemConteudo();
    }
}

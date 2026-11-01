using System.Globalization;
using LocadoraCarros.Application.Carros;
using LocadoraCarros.Application.Common;
using LocadoraCarros.Application.Funcionarios;
using LocadoraCarros.Application.Locacoes;
using LocadoraCarros.Application.Lojas;
using LocadoraCarros.Functions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace LocadoraCarros.Functions.Funcoes;

/// <summary>Lojas. Consulta pública; cadastro pelo gerente; operação da loja pela equipe.</summary>
[Acesso(Politica.Gerente)]
public sealed class LojasFunctions(
    LojaService lojas,
    CarroService carros,
    LocacaoService locacoes,
    FuncionarioService funcionarios)
{
    [Function("Lojas_Listar")]
    [Acesso(Politica.Publico)]
    public async Task<IActionResult> Listar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lojas")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await lojas.ListarAsync(req.LerQuery<FiltroLojasRequest>(), ct));

    [Function("Lojas_Obter")]
    [Acesso(Politica.Publico)]
    public async Task<IActionResult> Obter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lojas/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await lojas.ObterAsync(id, ct));

    [Function("Lojas_Criar")]
    public async Task<IActionResult> Criar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "lojas")] HttpRequest req, CancellationToken ct)
    {
        var loja = await lojas.CriarAsync(await req.LerCorpoAsync<LojaRequest>(), ct);
        return Resposta.Criado($"/api/lojas/{loja.Id}", loja);
    }

    [Function("Lojas_Atualizar")]
    public async Task<IActionResult> Atualizar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "lojas/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await lojas.AtualizarAsync(id, await req.LerCorpoAsync<LojaRequest>(), ct));

    /// <summary>Desativa a loja (exige que esteja sem frota, equipe e locações ativas).</summary>
    [Function("Lojas_Excluir")]
    public async Task<IActionResult> Excluir(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "lojas/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await lojas.ExcluirAsync(id, ct);
        return Resposta.SemConteudo();
    }

    [Function("Lojas_Reativar")]
    public async Task<IActionResult> Reativar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "lojas/{id:guid}/reativacao")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await lojas.ReativarAsync(id, ct);
        return Resposta.SemConteudo();
    }

    /// <summary>Carros cadastrados atualmente na loja.</summary>
    [Function("Lojas_Carros")]
    [Acesso(Politica.Equipe)]
    public async Task<IActionResult> Carros(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lojas/{id:guid}/carros")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await lojas.ObterAsync(id, ct);
        return Resposta.Ok(await carros.ListarAsync(req.LerQuery<FiltroCarrosRequest>() with { LojaId = id }, ct));
    }

    /// <summary>Locações que saem (Movimento=Retirada), chegam (Movimento=Devolucao) ou passam pela loja (Todos).</summary>
    [Function("Lojas_Locacoes")]
    [Acesso(Politica.Equipe)]
    public async Task<IActionResult> Locacoes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lojas/{id:guid}/locacoes")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await lojas.ObterAsync(id, ct);
        return Resposta.Ok(await locacoes.ListarAsync(req.LerQuery<FiltroLocacoesRequest>() with { LojaId = id }, ct));
    }

    [Function("Lojas_Funcionarios")]
    public async Task<IActionResult> Funcionarios(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lojas/{id:guid}/funcionarios")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await lojas.ObterAsync(id, ct);
        return Resposta.Ok(await funcionarios.ListarAsync(req.LerQuery<FiltroFuncionariosRequest>() with { LojaId = id }, ct));
    }

    /// <summary>Painel do dia: frota por status, retiradas, devoluções e atrasos.</summary>
    [Function("Lojas_Painel")]
    [Acesso(Politica.Equipe)]
    public async Task<IActionResult> Painel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lojas/{id:guid}/painel")] HttpRequest req, Guid id, CancellationToken ct)
    {
        var texto = req.Query["data"].ToString();
        DateOnly? data = null;
        if (!string.IsNullOrWhiteSpace(texto))
            data = DateOnly.TryParse(texto, CultureInfo.InvariantCulture, out var d)
                ? d
                : throw new ValidacaoException("Valor inválido no parâmetro 'data'. Use o formato AAAA-MM-DD.");
        return Resposta.Ok(await lojas.ObterPainelAsync(id, data, ct));
    }
}

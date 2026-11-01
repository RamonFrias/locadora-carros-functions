using LocadoraCarros.Application.Locacoes;
using LocadoraCarros.Functions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace LocadoraCarros.Functions.Funcoes;

/// <summary>
/// Locações. Clientes reservam, alteram e cancelam as próprias; a equipe opera qualquer uma
/// e registra retirada, devolução e pagamentos no balcão.
/// </summary>
[Acesso(Politica.Logado)]
public sealed class LocacoesFunctions(LocacaoService locacoes)
{
    /// <summary>Simula valores (diárias, proteção, adicionais, taxa de devolução e caução) sem reservar.</summary>
    [Function("Locacoes_Cotar")]
    public async Task<IActionResult> Cotar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locacoes/cotacao")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await locacoes.CotarAsync(await req.LerCorpoAsync<CotacaoRequest>(), ct));

    /// <summary>Cria a reserva. Para a equipe, ClienteId é obrigatório; o cliente reserva para si.</summary>
    [Function("Locacoes_Criar")]
    public async Task<IActionResult> Criar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locacoes")] HttpRequest req, CancellationToken ct)
    {
        var locacao = await locacoes.CriarAsync(await req.LerCorpoAsync<ReservaRequest>(), ct);
        return Resposta.Criado($"/api/locacoes/{locacao.Id}", locacao);
    }

    /// <summary>Clientes veem apenas as próprias locações.</summary>
    [Function("Locacoes_Listar")]
    public async Task<IActionResult> Listar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "locacoes")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await locacoes.ListarAsync(req.LerQuery<FiltroLocacoesRequest>(), ct));

    [Function("Locacoes_Obter")]
    public async Task<IActionResult> Obter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "locacoes/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await locacoes.ObterAsync(id, ct));

    /// <summary>Altera datas, lojas, carro, proteção, adicionais ou forma de pagamento (antes da retirada).</summary>
    [Function("Locacoes_Alterar")]
    public async Task<IActionResult> Alterar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "locacoes/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await locacoes.AlterarAsync(id, await req.LerCorpoAsync<ReservaRequest>(), ct));

    /// <summary>Cancela. Reembolso integral com 24h ou mais de antecedência da retirada.</summary>
    [Function("Locacoes_Cancelar")]
    public async Task<IActionResult> Cancelar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locacoes/{id:guid}/cancelamento")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await locacoes.CancelarAsync(id, await req.LerCorpoAsync<CancelarLocacaoRequest>(), ct));

    [Function("Locacoes_RegistrarRetirada")]
    [Acesso(Politica.Equipe)]
    public async Task<IActionResult> RegistrarRetirada(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locacoes/{id:guid}/retirada")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await locacoes.RegistrarRetiradaAsync(id, await req.LerCorpoAsync<RetiradaRequest>(), ct));

    /// <summary>Apura atraso, combustível e danos; encargos são descontados da caução.</summary>
    [Function("Locacoes_RegistrarDevolucao")]
    [Acesso(Politica.Equipe)]
    public async Task<IActionResult> RegistrarDevolucao(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locacoes/{id:guid}/devolucao")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await locacoes.RegistrarDevolucaoAsync(id, await req.LerCorpoAsync<DevolucaoRequest>(), ct));

    /// <summary>Quita no balcão o saldo devedor restante.</summary>
    [Function("Locacoes_QuitarSaldo")]
    [Acesso(Politica.Equipe)]
    public async Task<IActionResult> QuitarSaldo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locacoes/{id:guid}/quitacao")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await locacoes.QuitarSaldoAsync(id, ct));
}

using LocadoraCarros.Application.Carros;
using LocadoraCarros.Functions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace LocadoraCarros.Functions.Funcoes;

/// <summary>
/// Frota. Clientes só consultam os carros disponíveis de uma loja; a frota completa,
/// o detalhe e a manutenção são exclusivos da equipe.
/// </summary>
[Acesso(Politica.Equipe)]
public sealed class CarrosFunctions(CarroService carros)
{
    [Function("Carros_Listar")]
    public async Task<IActionResult> Listar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "carros")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await carros.ListarAsync(req.LerQuery<FiltroCarrosRequest>(), ct));

    /// <summary>Carros da loja livres para reserva no período (datas no horário local, sem fuso). Qualquer usuário logado.</summary>
    [Function("Carros_Disponiveis")]
    [Acesso(Politica.Logado)]
    public async Task<IActionResult> Disponiveis(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "carros/disponiveis")] HttpRequest req, CancellationToken ct) =>
        Resposta.Ok(await carros.ListarDisponiveisAsync(req.LerQuery<DisponibilidadeRequest>(), ct));

    [Function("Carros_Obter")]
    public async Task<IActionResult> Obter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "carros/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await carros.ObterAsync(id, ct));

    [Function("Carros_Criar")]
    public async Task<IActionResult> Criar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "carros")] HttpRequest req, CancellationToken ct)
    {
        var carro = await carros.CriarAsync(await req.LerCorpoAsync<CriarCarroRequest>(), ct);
        return Resposta.Criado($"/api/carros/{carro.Id}", carro);
    }

    [Function("Carros_Atualizar")]
    public async Task<IActionResult> Atualizar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "carros/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await carros.AtualizarAsync(id, await req.LerCorpoAsync<AtualizarCarroRequest>(), ct));

    /// <summary>Manutenção, limpeza, transferência, indisponível... ("Alugado" é automático).</summary>
    [Function("Carros_AlterarStatus")]
    public async Task<IActionResult> AlterarStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "carros/{id:guid}/status")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await carros.AlterarStatusAsync(id, await req.LerCorpoAsync<AlterarStatusCarroRequest>(), ct));

    [Function("Carros_Transferir")]
    public async Task<IActionResult> Transferir(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "carros/{id:guid}/transferencia")] HttpRequest req, Guid id, CancellationToken ct) =>
        Resposta.Ok(await carros.TransferirAsync(id, await req.LerCorpoAsync<TransferirCarroRequest>(), ct));

    /// <summary>Exclusão lógica: o carro sai da frota (status Inativo).</summary>
    [Function("Carros_Excluir")]
    public async Task<IActionResult> Excluir(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "carros/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        await carros.ExcluirAsync(id, ct);
        return Resposta.SemConteudo();
    }
}

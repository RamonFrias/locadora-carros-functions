using System.Reflection;
using System.Text.Json;
using LocadoraCarros.Application.Common;
using LocadoraCarros.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocadoraCarros.Functions.Http;

/// <summary>Converte exceções em Problem Details (RFC 9457), com os mesmos status da versão Web API.</summary>
public sealed class TratamentoDeErrosMiddleware(ILogger<TratamentoDeErrosMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var http = context.GetHttpContext();
            if (http is null) throw;

            var erro = Desembrulhar(ex);
            var (status, titulo) = erro switch
            {
                ValidacaoException or JsonException => (StatusCodes.Status400BadRequest, "Dados inválidos"),
                CredenciaisInvalidasException => (StatusCodes.Status401Unauthorized, "Não autenticado"),
                AcessoNegadoException => (StatusCodes.Status403Forbidden, "Acesso negado"),
                RecursoNaoEncontradoException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
                ConflitoException or DbUpdateException => (StatusCodes.Status409Conflict, "Conflito"),
                DomainException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
                _ => (StatusCodes.Status500InternalServerError, "Erro interno")
            };

            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(erro, "Erro não tratado em {Funcao}", context.FunctionDefinition.Name);

            var detalhe = erro switch
            {
                AppException or DomainException => erro.Message,
                JsonException => "O corpo da requisição não é um JSON válido para esta operação.",
                DbUpdateException => "Os dados violam uma restrição de unicidade ou foram alterados por outra operação.",
                _ => "Ocorreu um erro inesperado. Tente novamente mais tarde."
            };

            var problema = new ProblemDetails
            {
                Type = $"https://httpstatuses.io/{status}",
                Title = titulo,
                Status = status,
                Detail = detalhe,
                Instance = http.Request.Path
            };

            http.Response.StatusCode = status;
            http.Response.ContentType = "application/problem+json";
            await http.Response.WriteAsync(JsonSerializer.Serialize(problema, Json.Opcoes));
        }
    }

    private static Exception Desembrulhar(Exception ex)
    {
        while (ex is AggregateException or TargetInvocationException && ex.InnerException is not null)
            ex = ex.InnerException;
        return ex;
    }
}

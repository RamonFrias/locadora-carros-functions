using System.Text.Json.Nodes;
using LocadoraCarros.Functions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace LocadoraCarros.Functions.Funcoes;

/// <summary>
/// Documentação para testes no navegador. As rotas e contratos são os mesmos da versão Web API,
/// então a especificação OpenAPI dela é reaproveitada (OpenApi/openapi.json) e servida com o Scalar.
/// </summary>
[Acesso(Politica.Publico)]
public sealed class DocumentacaoFunctions
{
    private const string PaginaScalar = """
        <!doctype html>
        <html lang="pt-BR">
          <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>Locadora de Carros API (Azure Functions)</title>
          </head>
          <body>
            <script id="api-reference" data-url="/api/openapi/v1.json"></script>
            <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
          </body>
        </html>
        """;

    private static readonly Lazy<string> Especificacao = new(() =>
    {
        using var recurso = typeof(DocumentacaoFunctions).Assembly
            .GetManifestResourceStream("LocadoraCarros.Functions.OpenApi.openapi.json")!;
        using var leitor = new StreamReader(recurso);
        return leitor.ReadToEnd();
    });

    [Function("Docs_OpenApi")]
    public IActionResult OpenApi(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi/v1.json")] HttpRequest req)
    {
        // O servidor da especificação passa a ser o host atual (local ou Azure).
        var documento = JsonNode.Parse(Especificacao.Value)!.AsObject();
        documento["servers"] = new JsonArray(new JsonObject { ["url"] = $"{req.Scheme}://{req.Host}" });
        documento["info"]!["title"] = "Locadora de Carros API (Azure Functions)";
        return new ContentResult { Content = documento.ToJsonString(), ContentType = "application/json; charset=utf-8", StatusCode = 200 };
    }

    [Function("Docs_Scalar")]
    public IActionResult Scalar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scalar")] HttpRequest req) =>
        new ContentResult { Content = PaginaScalar, ContentType = "text/html; charset=utf-8", StatusCode = 200 };
}

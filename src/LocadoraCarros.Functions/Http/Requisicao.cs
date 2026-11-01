using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocadoraCarros.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraCarros.Functions.Http;

/// <summary>Mesmas convenções de JSON da Web API: camelCase e enums como texto.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>
/// Substitui o model binding e a validação automática dos controllers, que não existem nas Functions:
/// lê corpo JSON e query string e aplica os atributos de validação dos DTOs.
/// </summary>
public static class Requisicao
{
    public static async Task<T> LerCorpoAsync<T>(this HttpRequest req)
    {
        T? valor;
        try
        {
            valor = await JsonSerializer.DeserializeAsync<T>(req.Body, Json.Opcoes);
        }
        catch (JsonException ex)
        {
            throw new ValidacaoException(ex.Path is null or "$"
                ? "Envie o corpo da requisição em JSON válido."
                : $"Valor inválido no campo '{ex.Path.TrimStart('$', '.')}'.");
        }

        if (valor is null) throw new ValidacaoException("Envie o corpo da requisição em JSON.");
        Validacao.Validar(valor);
        return valor;
    }

    public static T LerQuery<T>(this HttpRequest req) where T : new()
    {
        var valor = new T();
        foreach (var propriedade in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            var texto = req.Query[propriedade.Name].ToString();
            if (string.IsNullOrWhiteSpace(texto)) continue;
            propriedade.SetValue(valor, Converter(texto, propriedade.PropertyType, propriedade.Name));
        }

        Validacao.Validar(valor);
        return valor;
    }

    private static object Converter(string texto, Type tipo, string nome)
    {
        var alvo = Nullable.GetUnderlyingType(tipo) ?? tipo;
        object? convertido = alvo switch
        {
            _ when alvo == typeof(string) => texto,
            _ when alvo == typeof(Guid) => Guid.TryParse(texto, out var g) ? g : null,
            _ when alvo == typeof(int) => int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
            _ when alvo == typeof(bool) => bool.TryParse(texto, out var b) ? b : null,
            _ when alvo == typeof(DateTime) => DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null,
            _ when alvo == typeof(DateOnly) => DateOnly.TryParse(texto, CultureInfo.InvariantCulture, out var d) ? d : null,
            _ when alvo.IsEnum => Enum.TryParse(alvo, texto, true, out var e) && Enum.IsDefined(alvo, e!) ? e : null,
            _ => null
        };
        return convertido ?? throw new ValidacaoException($"Valor inválido no parâmetro '{JsonNamingPolicy.CamelCase.ConvertName(nome)}'.");
    }
}

public static class Validacao
{
    public static void Validar(object valor)
    {
        var erros = new List<string>();
        ValidarObjeto(valor, string.Empty, erros, 0);
        if (erros.Count > 0) throw new ValidacaoException(string.Join(" ", erros));
    }

    // Records posicionais guardam os atributos no parâmetro do construtor, não na propriedade: lemos os dois.
    private static void ValidarObjeto(object valor, string prefixo, List<string> erros, int nivel)
    {
        if (nivel > 5) return;
        var tipo = valor.GetType();
        var parametros = tipo.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Where(p => p.Name is not null)
            .GroupBy(p => p.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var propriedade in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            var atributos = propriedade.GetCustomAttributes<ValidationAttribute>(true).ToList();
            if (parametros.TryGetValue(propriedade.Name, out var parametro))
                atributos.AddRange(parametro.GetCustomAttributes<ValidationAttribute>(true));

            var conteudo = propriedade.GetValue(valor);
            var nome = prefixo + JsonNamingPolicy.CamelCase.ConvertName(propriedade.Name);
            foreach (var atributo in atributos.Where(a => !a.IsValid(conteudo)))
                erros.Add(Mensagem(atributo, nome));

            if (conteudo is null || conteudo is string) continue;
            if (EhDto(conteudo.GetType()))
                ValidarObjeto(conteudo, nome + ".", erros, nivel + 1);
            else if (conteudo is IEnumerable itens)
                foreach (var item in itens)
                    if (item is not null && EhDto(item.GetType()))
                        ValidarObjeto(item, nome + ".", erros, nivel + 1);
        }
    }

    private static bool EhDto(Type tipo) =>
        tipo.IsClass && tipo.Namespace?.StartsWith("LocadoraCarros.Application", StringComparison.Ordinal) == true;

    private static string Mensagem(ValidationAttribute atributo, string campo) => atributo switch
    {
        RequiredAttribute => $"O campo '{campo}' é obrigatório.",
        StringLengthAttribute s => $"O campo '{campo}' deve ter até {s.MaximumLength} caracteres.",
        RangeAttribute r => $"O campo '{campo}' deve estar entre {r.Minimum} e {r.Maximum}.",
        EmailAddressAttribute => $"O campo '{campo}' deve ser um e-mail válido.",
        _ => $"O campo '{campo}' é inválido."
    };
}

/// <summary>Respostas JSON com as mesmas opções de serialização em todas as funções.</summary>
public static class Resposta
{
    public static IActionResult Ok(object valor) => new JsonResultado(valor, StatusCodes.Status200OK, null);

    public static IActionResult Criado(string local, object valor) => new JsonResultado(valor, StatusCodes.Status201Created, local);

    public static IActionResult SemConteudo() => new NoContentResult();

    private sealed class JsonResultado(object valor, int status, string? local) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            var resposta = context.HttpContext.Response;
            resposta.StatusCode = status;
            resposta.ContentType = "application/json; charset=utf-8";
            if (local is not null) resposta.Headers.Location = local;
            await JsonSerializer.SerializeAsync(resposta.Body, valor, valor.GetType(), Json.Opcoes);
        }
    }
}

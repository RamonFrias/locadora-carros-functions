using System.Collections.Concurrent;
using System.Security.Claims;
using LocadoraCarros.Application.Abstractions;
using LocadoraCarros.Application.Common;
using LocadoraCarros.Domain.Usuarios;
using LocadoraCarros.Infrastructure.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace LocadoraCarros.Functions.Http;

public enum Politica
{
    /// <summary>Sem token.</summary>
    Publico,
    /// <summary>Qualquer usuário com token válido (padrão quando a função não declara nada).</summary>
    Logado,
    /// <summary>Funcionários e gerentes.</summary>
    Equipe,
    /// <summary>Somente gerentes.</summary>
    Gerente
}

/// <summary>Equivalente ao [Authorize]/[AllowAnonymous] dos controllers. Vale na classe ou no método.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AcessoAttribute(Politica politica) : Attribute
{
    public Politica Politica { get; } = politica;
}

/// <summary>Valida o bearer token e a política de acesso de cada função antes de executá-la.</summary>
public sealed class AutenticacaoMiddleware(JwtTokenValidator validador) : IFunctionsWorkerMiddleware
{
    private static readonly ConcurrentDictionary<string, Politica> Politicas = new();

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var http = context.GetHttpContext();
        var politica = Politicas.GetOrAdd(context.FunctionDefinition.EntryPoint, ObterPolitica);
        if (http is null || politica == Politica.Publico)
        {
            await next(context);
            return;
        }

        var cabecalho = http.Request.Headers.Authorization.ToString();
        var token = cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? cabecalho[7..].Trim() : null;
        var usuario = await validador.ValidarAsync(token)
            ?? throw new CredenciaisInvalidasException("Token ausente, inválido ou expirado.");

        var perfil = usuario.FindFirstValue(JwtOptions.ClaimPerfil);
        var permitido = politica switch
        {
            Politica.Equipe => perfil is nameof(PerfilUsuario.Funcionario) or nameof(PerfilUsuario.Gerente),
            Politica.Gerente => perfil is nameof(PerfilUsuario.Gerente),
            _ => true
        };
        if (!permitido)
            throw new AcessoNegadoException("Seu perfil não tem permissão para esta operação.");

        context.InstanceServices.GetRequiredService<UsuarioAtualFunctions>().Definir(usuario);
        await next(context);
    }

    // EntryPoint = "Namespace.Classe.Metodo": o atributo do método vence o da classe; sem atributo, exige login.
    private static Politica ObterPolitica(string entryPoint)
    {
        var separador = entryPoint.LastIndexOf('.');
        var tipo = typeof(AutenticacaoMiddleware).Assembly.GetType(entryPoint[..separador]);
        var metodo = tipo?.GetMethod(entryPoint[(separador + 1)..]);
        var atributo = metodo?.GetCustomAttributes(typeof(AcessoAttribute), false).OfType<AcessoAttribute>().FirstOrDefault()
                       ?? tipo?.GetCustomAttributes(typeof(AcessoAttribute), false).OfType<AcessoAttribute>().FirstOrDefault();
        return atributo?.Politica ?? Politica.Logado;
    }
}

/// <summary>Usuário autenticado na invocação atual, preenchido pelo middleware.</summary>
public sealed class UsuarioAtualFunctions : IUsuarioAtual
{
    private ClaimsPrincipal? _usuario;

    public void Definir(ClaimsPrincipal usuario) => _usuario = usuario;

    public bool Autenticado => _usuario is not null;

    public Guid Id => Guid.TryParse(_usuario?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : Guid.Empty;

    // Sem perfil válido no token, assume o de menor privilégio.
    public PerfilUsuario Perfil =>
        Enum.TryParse<PerfilUsuario>(_usuario?.FindFirstValue(JwtOptions.ClaimPerfil), out var perfil) ? perfil : PerfilUsuario.Cliente;
}

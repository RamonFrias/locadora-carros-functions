using LocadoraCarros.Application.Abstractions;
using LocadoraCarros.Domain.Carros;
using LocadoraCarros.Domain.Common;
using LocadoraCarros.Domain.Locacoes;
using LocadoraCarros.Domain.Lojas;
using LocadoraCarros.Domain.Usuarios;
using LocadoraCarros.Infrastructure.Persistence;
using LocadoraCarros.Infrastructure.Persistence.Repositories;
using LocadoraCarros.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocadoraCarros.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provedor = configuration["Database:Provider"] ?? "Sqlite";
        var conexao = configuration.GetConnectionString("Locadora")
            ?? throw new InvalidOperationException("Connection string 'Locadora' não configurada.");

        services.AddDbContext<AppDbContext>(options => _ = provedor switch
        {
            "Sqlite" => options.UseSqlite(conexao),
            // Retentativas cobrem o "acordar" do Azure SQL serverless depois de pausado.
            "SqlServer" => options.UseSqlServer(conexao, sql => sql.EnableRetryOnFailure(5)),
            "MySql" => options.UseMySQL(conexao),
            _ => throw new InvalidOperationException($"Database:Provider '{provedor}' não suportado. Use MySql, Sqlite ou SqlServer.")
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ILojaRepository, LojaRepository>();
        services.AddScoped<ICarroRepository, CarroRepository>();
        services.AddScoped<ILocacaoRepository, LocacaoRepository>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IRelogio, RelogioBrasilia>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Azure Functions não tem o pipeline de autenticação do ASP.NET: o token é validado
        // por JwtTokenValidator, chamado pelo middleware de autenticação das Functions.
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Secao));
        services.AddSingleton<JwtTokenValidator>();
        return services;
    }
}

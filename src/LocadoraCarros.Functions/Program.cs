using LocadoraCarros.Application;
using LocadoraCarros.Application.Abstractions;
using LocadoraCarros.Functions.Http;
using LocadoraCarros.Infrastructure;
using LocadoraCarros.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Em desenvolvimento a connection string vem dos user-secrets, compartilhados com a versão Web API.
// As variáveis de ambiente são adicionadas de novo para continuarem com prioridade, como no ASP.NET.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
    builder.Configuration.AddEnvironmentVariables();
}

// Ordem importa: o tratamento de erros envolve a autenticação e as funções.
builder.UseMiddleware<TratamentoDeErrosMiddleware>();
builder.UseMiddleware<AutenticacaoMiddleware>();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<UsuarioAtualFunctions>();
builder.Services.AddScoped<IUsuarioAtual>(sp => sp.GetRequiredService<UsuarioAtualFunctions>());

var app = builder.Build();

await DbInicializador.InicializarAsync(app.Services, builder.Configuration);

app.Run();

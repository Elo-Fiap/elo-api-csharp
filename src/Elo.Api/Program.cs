using System.Text.Json.Serialization;
using Elo.Api.Configuracao;
using Elo.Api.Seguranca;
using Elo.Application;
using Elo.Infrastructure;
using Elo.Infrastructure.Persistencia;

var builder = WebApplication.CreateBuilder(args);

// ---------- Injeção de dependência ----------
builder.Services.AddApplication();     // serviços (casos de uso)
builder.Services.AddInfrastructure();  // EF Core + SQLite, repositórios, rotina de retenção
builder.Services.AddSegurancaDaApi();  // módulo de Cybersecurity: PII, taint analysis e eventos
builder.Services.ProtegerLogs();       // todo log passa pela guarda de saída (tem de vir depois da configuração de logging)

builder.Services
    .AddControllers()
    .AddJsonOptions(opcoes => opcoes.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Enums como texto também na documentação OpenAPI.
builder.Services.ConfigureHttpJsonOptions(opcoes => opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();                                  // erros no formato RFC 9457
builder.Services.AddExceptionHandler<TratadorGlobalDeExcecoes>();      // exceção -> 404/409/422/500
builder.Services.AddDocumentacaoDaApi();                               // OpenAPI
builder.Services.AddHealthChecks().AddDbContextCheck<EloDbContext>("banco");
builder.Services.AddOptions<DispositivosOptions>().BindConfiguration(DispositivosOptions.Secao); // chave do ESP32

// O app (Expo) e o ESP32 chamam a API de outras origens.
builder.Services.AddCors(opcoes => opcoes.AddDefaultPolicy(politica =>
    politica.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---------- Pipeline HTTP ----------
app.UseSegurancaDeEntradaESaida(); // antes do tratador de exceções: o ProblemDetails dele também passa pela guarda
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();   // "/" abre a página simples do resumo do bairro (wwwroot/index.html)
app.UseStaticFiles();
app.UseCors();

app.UseDocumentacaoDaApi(); // /swagger, /scalar e /openapi/v1.json
app.MapControllers();
app.MapHealthChecks("/health");

// Cria/atualiza o banco (migrations) e cadastra os dados simulados na primeira execução.
await app.Services.InicializarBancoAsync();

await app.RunAsync();

/// <summary>Exposta para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Elo.Tests.Integracao;

/// <summary>
/// Sobe a API inteira em memória (TestServer) com um banco SQLite temporário.
/// O Program.cs roda de verdade: migrations, dados simulados, controllers, filtros e serialização.
/// Cada classe de teste ganha a sua fábrica (e o seu próprio banco).
/// </summary>
public sealed class EloApiFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _arquivoDoBanco = Path.Combine(Path.GetTempPath(), $"elo-testes-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Elo"] = $"Data Source={_arquivoDoBanco}",
            ["Retencao:Habilitada"] = "false"
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(_arquivoDoBanco);
            }
            catch (IOException)
            {
                // Arquivo temporário: se ainda estiver em uso, o sistema limpa depois.
            }
        }
    }
}

internal static class HttpExtensoes
{
    public static async Task<T> LerAsync<T>(this HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<T>(EloApiFactory.Json))!;

    public static Task<HttpResponseMessage> EnviarAsync<T>(this HttpClient cliente, string url, T corpo) =>
        cliente.PostAsJsonAsync(url, corpo, EloApiFactory.Json);
}

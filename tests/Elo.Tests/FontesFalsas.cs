using System.Text.Json;
using Elo.Application.Avisos;
using Elo.Infrastructure.Avisos;

namespace Elo.Tests;

/// <summary>Fonte de avisos falsa: os testes nunca acessam o INMET de verdade.</summary>
internal sealed class FonteDeAvisosFalsa(Func<IReadOnlyList<AvisoDoInmet>> produzir) : IFonteDeAvisos
{
    public Task<IReadOnlyList<AvisoDoInmet>> ObterAvisosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(produzir());
}

internal static class Fixtures
{
    /// <summary>
    /// Resposta real do INMET de 05/11/2026, a mesma do elo-app (src/services/__fixtures__/inmet-ativos.json):
    /// 3 avisos hoje (2 para São Paulo) e 1 futuro (para São Paulo).
    /// </summary>
    public static IReadOnlyList<AvisoDoInmet> AvisosDoInmet()
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "Fixtures", "inmet-ativos.json");
        using var json = JsonDocument.Parse(File.ReadAllText(caminho));
        return LeitorDeAvisosDoInmet.Ler(json.RootElement);
    }

    public static AvisoDoInmet Aviso(
        string id,
        string municipios,
        int gravidade = 2,
        string quando = "hoje",
        bool encerrado = false,
        string inicio = "2026-11-05T00:01:00-03:00") => new(
            id,
            "Chuvas Intensas",
            gravidade switch { 3 => "Grande Perigo", 2 => "Perigo", _ => "Perigo Potencial" },
            gravidade,
            inicio,
            "2026-11-05T23:59:00-03:00",
            quando,
            ["Chuva entre 30 e 60 mm/h."],
            ["Evite usar aparelhos eletrônicos ligados à tomada."],
            municipios.Split(',').ToHashSet(),
            encerrado);
}

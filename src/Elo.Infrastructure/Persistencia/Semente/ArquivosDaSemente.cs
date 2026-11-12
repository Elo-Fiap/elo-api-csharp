using System.Reflection;
using System.Text.Json;
using Elo.Domain.Comum;

namespace Elo.Infrastructure.Persistencia.Semente;

/// <summary>
/// Lê os arquivos JSON da semente, embutidos no assembly (nada é lido de pasta nenhuma em tempo de execução).
/// <para>
/// Os traçados das ruas do piloto são reais, do OpenStreetMap (© colaboradores do OpenStreetMap, licença ODbL,
/// https://www.openstreetmap.org/copyright), usados com os nomes fictícios da API.
/// </para>
/// </summary>
internal static class ArquivosDaSemente
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public static T Ler<T>(string nomeDoArquivo)
    {
        using var fluxo = AbrirArquivo(nomeDoArquivo);
        return JsonSerializer.Deserialize<T>(fluxo, Opcoes)
            ?? throw new InvalidOperationException($"O arquivo da semente {nomeDoArquivo} está vazio.");
    }

    public static JsonDocument LerDocumento(string nomeDoArquivo)
    {
        using var fluxo = AbrirArquivo(nomeDoArquivo);
        return JsonDocument.Parse(fluxo);
    }

    private static Stream AbrirArquivo(string nomeDoArquivo) =>
        typeof(ArquivosDaSemente).Assembly.GetManifestResourceStream($"Elo.Semente.{nomeDoArquivo}")
            ?? throw new InvalidOperationException($"Arquivo da semente não encontrado no assembly: {nomeDoArquivo}.");
}

/// <summary>Uma rua do piloto no mapa: o ponto de referência e a linha, no formato [longitude, latitude].</summary>
internal sealed record GeografiaDaRua(string Nome, Coordenada Ponto, IReadOnlyList<Coordenada> Tracado);

/// <summary>Geografia das 8 ruas do Jardim Pantanal (na ordem em que a semente as cria) e da régua de exemplo.</summary>
internal sealed record GeografiaDoPiloto(IReadOnlyList<GeografiaDaRua> Ruas, Coordenada PontoDaRegua)
{
    public const string Arquivo = "piloto-tracados.json";

    public static GeografiaDoPiloto Carregar()
    {
        using var documento = ArquivosDaSemente.LerDocumento(Arquivo);
        var raiz = documento.RootElement;

        // As propriedades do JSON são lidas na ordem do arquivo: é ela que casa com a ordem das ruas da semente.
        var ruas = raiz.GetProperty("ruas").EnumerateObject()
            .Select(rua => new GeografiaDaRua(
                rua.Name,
                LerPar(rua.Value.GetProperty("ponto")),
                [.. rua.Value.GetProperty("tracado").EnumerateArray().Select(LerPar)]))
            .ToList();

        return new GeografiaDoPiloto(ruas, LerPar(raiz.GetProperty("regua").GetProperty("ponto")));
    }

    private static Coordenada LerPar(JsonElement par) => new(par[0].GetDouble(), par[1].GetDouble());
}

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Elo.Application.Avisos;

namespace Elo.Infrastructure.Avisos;

/// <summary>
/// Lê a resposta do INMET (<c>{ "hoje": [...], "futuro": [...] }</c>) com as mesmas regras do app
/// (<c>src/services/inmet.ts</c> e <c>listaPython.ts</c> do elo-app):
/// datas de Brasília sem fuso viram ISO com -03:00, "encerrado" pode vir como texto,
/// e "riscos"/"instrucoes" podem vir como lista de verdade ou como lista do Python escrita em texto.
/// Polígonos e ícones são descartados.
/// </summary>
public static partial class LeitorDeAvisosDoInmet
{
    public static IReadOnlyList<AvisoDoInmet> Ler(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return [.. LerLista(raiz, "hoje"), .. LerLista(raiz, "futuro")];
    }

    /// <summary>"2026-11-05 00:01" (horário de Brasília, sem fuso) vira "2026-11-05T00:01:00-03:00".</summary>
    public static string ParaIso(string texto)
    {
        var encontrado = DataDoInmet().Match(texto.Trim());
        return encontrado.Success
            ? $"{encontrado.Groups[1].Value}T{encontrado.Groups[2].Value}:00-03:00" // Brasília não tem horário de verão desde 2019
            : texto.Trim();
    }

    /// <summary>Severidade sem acento e sem diferença de maiúsculas; desconhecida vira "Perigo Potencial".</summary>
    public static (string Severidade, int Gravidade) LerSeveridade(string texto) =>
        SemAcento(texto).Trim().ToLowerInvariant() switch
        {
            "grande perigo" => ("Grande Perigo", 3),
            "perigo" => ("Perigo", 2),
            _ => ("Perigo Potencial", 1)
        };

    /// <summary>
    /// Lista escrita como texto do Python ("['a', 'b']"). Formato inesperado vira 1 item,
    /// para o texto não se perder.
    /// </summary>
    public static IReadOnlyList<string> ConverterListaPython(string valor)
    {
        var texto = valor.Trim();
        if (texto is "" or "None" or "[]")
        {
            return [];
        }

        if (!texto.StartsWith('[') || !texto.EndsWith(']'))
        {
            return [texto];
        }

        var itens = ItemEntreAspas().Matches(texto)
            .Select(item => Desescapar(item.Groups[1].Success ? item.Groups[1].Value : item.Groups[2].Value))
            .ToList();

        return itens.Count > 0 ? Limpar(itens) : [texto];
    }

    private static IEnumerable<AvisoDoInmet> LerLista(JsonElement raiz, string quando)
    {
        if (!raiz.TryGetProperty(quando, out var lista) || lista.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var (severidade, gravidade) = LerSeveridade(Texto(item, "severidade"));
            var titulo = Texto(item, "descricao").Trim();

            yield return new AvisoDoInmet(
                Id: Texto(item, "id"),
                Titulo: titulo.Length > 0 ? titulo : "Aviso",
                Severidade: severidade,
                Gravidade: gravidade,
                Inicio: ParaIso(Texto(item, "inicio")),
                Fim: ParaIso(Texto(item, "fim")),
                Quando: quando,
                Riscos: LerListaDeTextos(item, "riscos"),
                Instrucoes: LerListaDeTextos(item, "instrucoes"),
                Municipios: Texto(item, "geocodes")
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet(StringComparer.Ordinal),
                Encerrado: EstaEncerrado(item));
        }
    }

    private static string Texto(JsonElement item, string campo)
    {
        if (!item.TryGetProperty(campo, out var valor))
        {
            return string.Empty;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString() ?? string.Empty,
            JsonValueKind.Number => valor.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static bool EstaEncerrado(JsonElement item) =>
        item.TryGetProperty("encerrado", out var valor) && valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => string.Equals(valor.GetString()?.Trim(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static IReadOnlyList<string> LerListaDeTextos(JsonElement item, string campo)
    {
        if (!item.TryGetProperty(campo, out var valor))
        {
            return [];
        }

        return valor.ValueKind switch
        {
            JsonValueKind.Array => Limpar(valor.EnumerateArray()
                .Where(elemento => elemento.ValueKind == JsonValueKind.String)
                .Select(elemento => elemento.GetString()!)),
            JsonValueKind.String => ConverterListaPython(valor.GetString()!),
            _ => []
        };
    }

    private static IReadOnlyList<string> Limpar(IEnumerable<string> itens) =>
        [.. itens.Select(item => EspacosRepetidos().Replace(item, " ").Trim()).Where(item => item.Length > 0)];

    private static string Desescapar(string texto) => Escape().Replace(texto, "$1");

    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var semMarcas = decomposto.Where(letra => CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark);
        return new string([.. semMarcas]).Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})[ T](\d{2}:\d{2})")]
    private static partial Regex DataDoInmet();

    // Item entre aspas simples ou duplas, aceitando aspas escapadas dentro dele.
    [GeneratedRegex(@"'((?:[^'\\]|\\.)*)'|""((?:[^""\\]|\\.)*)""")]
    private static partial Regex ItemEntreAspas();

    [GeneratedRegex(@"\\(.)")]
    private static partial Regex Escape();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRepetidos();
}

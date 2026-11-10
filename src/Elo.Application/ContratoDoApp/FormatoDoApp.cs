using System.Globalization;
using Elo.Domain.Chamadas;

namespace Elo.Application.ContratoDoApp;

/// <summary>
/// Tradução entre o vocabulário do app (elo-app) e o do domínio:
/// status em snake_case, ids como texto, datas no formato do JavaScript e medidas em metros.
/// </summary>
public static class FormatoDoApp
{
    public const string TudoBem = "tudo_bem";
    public const string AguaSubindo = "agua_subindo";
    public const string PrecisoAjuda = "preciso_ajuda";

    /// <summary>Atalhos que o app mostra no plano da rua (números públicos, não são dado pessoal).</summary>
    public static readonly IReadOnlyList<ContatoDeEmergenciaApp> ContatosDeEmergencia =
    [
        new("Defesa Civil", "199"),
        new("Bombeiros", "193")
    ];

    public static string Status(SituacaoRua situacao) => situacao switch
    {
        SituacaoRua.TudoBem => TudoBem,
        SituacaoRua.AguaSubindo => AguaSubindo,
        SituacaoRua.PrecisoDeAjuda => PrecisoAjuda,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, "Situação desconhecida.")
    };

    public static SituacaoRua? Situacao(string? status) => status switch
    {
        TudoBem => SituacaoRua.TudoBem,
        AguaSubindo => SituacaoRua.AguaSubindo,
        PrecisoAjuda => SituacaoRua.PrecisoDeAjuda,
        _ => null
    };

    /// <summary>
    /// Mesmo formato do toISOString() do JavaScript ("2026-11-05T15:00:00.000Z"):
    /// o app compara o "respondidaEm" com o "criadaEm" que ele mesmo mandou.
    /// </summary>
    public static string Data(DateTime momento)
    {
        var utc = momento.Kind == DateTimeKind.Local ? momento.ToUniversalTime() : momento;
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    public static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>O app trata o id da rua como texto; aqui ele precisa ser um número positivo.</summary>
    public static int? LerId(string? id) =>
        int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var valor) && valor > 0 ? valor : null;

    public static double Metros(double centimetros) => Math.Round(centimetros / 100, 2);
}

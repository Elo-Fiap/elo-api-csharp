namespace Elo.Domain.Painel;

/// <summary>
/// Regra central do Elo, escrita em LINQ: em que ordem o coordenador vê as ruas.
/// <list type="number">
/// <item>Quem pediu ajuda: primeiro quem ainda não tem ninguém a caminho, depois quem espera há mais tempo.</item>
/// <item>Quem está em silêncio: sensor acima do limite primeiro, depois as ruas de maior risco.</item>
/// <item>Água subindo: sensor acima do limite primeiro, depois quem avisou antes.</item>
/// <item>Tudo bem: em ordem alfabética.</item>
/// </list>
/// </summary>
public static class PriorizadorDeRuas
{
    private static readonly StringComparer ComparadorDeNomes = StringComparer.InvariantCultureIgnoreCase;

    public static IReadOnlyList<SituacaoDaRua> Ordenar(IEnumerable<SituacaoDaRua> situacoes)
    {
        ArgumentNullException.ThrowIfNull(situacoes);
        var ruas = situacoes.ToList();

        var pedidosDeAjuda = ruas
            .Where(r => r.Status == StatusNoPainel.PrecisaDeAjuda)
            .OrderBy(r => r.AtendimentoACaminho)               // ninguém a caminho vem antes
            .ThenBy(r => r.UltimaResposta!.RespondidaEm)       // quem espera há mais tempo
            .ThenByDescending(r => r.UltimaResposta!.Pessoas); // mais pessoas

        var emSilencio = ruas
            .Where(r => r.Status == StatusNoPainel.EmSilencio)
            .OrderBy(r => r.AtendimentoACaminho)
            .ThenByDescending(r => r.SensorAcimaDoLimite)      // o sensor já viu a água passar do limite
            .ThenByDescending(r => r.Rua.NivelRisco)           // ruas que mais alagam
            .ThenBy(r => r.Rua.Nome, ComparadorDeNomes);

        var aguaSubindo = ruas
            .Where(r => r.Status == StatusNoPainel.AguaSubindo)
            .OrderByDescending(r => r.SensorAcimaDoLimite)
            .ThenBy(r => r.UltimaResposta!.RespondidaEm);

        var tudoBem = ruas
            .Where(r => r.Status == StatusNoPainel.TudoBem)
            .OrderBy(r => r.Rua.Nome, ComparadorDeNomes);

        return [.. pedidosDeAjuda, .. emSilencio, .. aguaSubindo, .. tudoBem];
    }
}

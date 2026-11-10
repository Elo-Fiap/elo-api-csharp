namespace Elo.Domain.Chamadas;

/// <summary>
/// Indicador de impacto do Elo: percentual de ruas com situação conhecida em até 15 minutos.
/// Também é guardado na chamada quando ela é encerrada, para continuar valendo
/// depois que as respostas forem apagadas (30 dias).
/// </summary>
public sealed record IndicadorDaChamada(
    int TotalDeRuas,
    int RuasComResposta,
    int RuasConhecidasNaJanela,
    int RuasQuePediramAjuda,
    double? MinutosAtePrimeiraRespostaEmMedia)
{
    /// <summary>A janela do indicador definida na proposta: 15 minutos.</summary>
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(15);

    public int RuasEmSilencio => TotalDeRuas - RuasComResposta;
    public double PercentualConhecidoNaJanela => Percentual(RuasConhecidasNaJanela);
    public double PercentualComResposta => Percentual(RuasComResposta);

    /// <summary>
    /// Calcula o indicador com LINQ a partir das respostas da chamada.
    /// Usa a hora em que a resposta CHEGOU (<see cref="Resposta.RecebidaEm"/>):
    /// uma resposta guardada offline só conta como "conhecida" quando o coordenador pôde vê-la.
    /// </summary>
    public static IndicadorDaChamada Calcular(
        DateTime inicioDaChamada,
        IReadOnlyCollection<int> ruasDoBairro,
        IEnumerable<Resposta> respostas)
    {
        ArgumentNullException.ThrowIfNull(ruasDoBairro);
        ArgumentNullException.ThrowIfNull(respostas);

        var ruas = ruasDoBairro.ToHashSet();
        var limite = inicioDaChamada + Janela;

        // Uma linha por rua: quando chegou a primeira resposta e se ela pediu ajuda em algum momento.
        var porRua = respostas
            .Where(resposta => ruas.Contains(resposta.RuaId))
            .GroupBy(resposta => resposta.RuaId)
            .Select(grupo => new
            {
                PrimeiraChegada = grupo.Min(resposta => resposta.RecebidaEm),
                PediuAjuda = grupo.Any(resposta => resposta.PediuAjuda)
            })
            .ToList();

        double? mediaEmMinutos = porRua.Count == 0
            ? null
            : Math.Round(porRua.Average(r => Math.Max(0, (r.PrimeiraChegada - inicioDaChamada).TotalMinutes)), 1);

        return new IndicadorDaChamada(
            TotalDeRuas: ruas.Count,
            RuasComResposta: porRua.Count,
            RuasConhecidasNaJanela: porRua.Count(r => r.PrimeiraChegada <= limite),
            RuasQuePediramAjuda: porRua.Count(r => r.PediuAjuda),
            MinutosAtePrimeiraRespostaEmMedia: mediaEmMinutos);
    }

    private double Percentual(int parte) =>
        TotalDeRuas == 0 ? 0 : Math.Round(100.0 * parte / TotalDeRuas, 1);
}

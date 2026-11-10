using Elo.Domain.Chamadas;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;

namespace Elo.Domain.Painel;

/// <summary>Como a rua aparece no painel do coordenador. A ordem dos valores é a ordem de prioridade.</summary>
public enum StatusNoPainel
{
    PrecisaDeAjuda = 1,
    EmSilencio = 2,
    AguaSubindo = 3,
    TudoBem = 4
}

/// <summary>
/// Situação de uma rua em uma chamada: a última resposta, a última leitura do sensor
/// e se alguém da coordenação já está indo até lá.
/// </summary>
public sealed record SituacaoDaRua
{
    public required Rua Rua { get; init; }
    public Resposta? UltimaResposta { get; init; }
    public LeituraRecente? UltimaLeitura { get; init; }
    public bool AtendimentoACaminho { get; init; }

    /// <summary>A resposta mais recente vale: "Preciso de ajuda" seguido de "Tudo bem" vira "Tudo bem".</summary>
    public StatusNoPainel Status => UltimaResposta?.Situacao switch
    {
        null => StatusNoPainel.EmSilencio,
        SituacaoRua.PrecisoDeAjuda => StatusNoPainel.PrecisaDeAjuda,
        SituacaoRua.AguaSubindo => StatusNoPainel.AguaSubindo,
        _ => StatusNoPainel.TudoBem
    };

    public bool SensorAcimaDoLimite => UltimaLeitura?.AcimaDoLimite ?? false;

    public static SituacaoDaRua Montar(
        Rua rua,
        IEnumerable<Resposta> respostasDaRua,
        IEnumerable<AcaoCoordenador> acoesDaRua,
        LeituraRecente? ultimaLeitura)
    {
        ArgumentNullException.ThrowIfNull(rua);

        var ultimaResposta = respostasDaRua
            .OrderByDescending(resposta => resposta.RespondidaEm)
            .ThenByDescending(resposta => resposta.RecebidaEm)
            .FirstOrDefault();

        // "Vou até lá" só conta se foi marcado DEPOIS da última resposta da rua.
        var aCaminho = acoesDaRua.Any(acao =>
            acao.Tipo == TipoAcao.VouAteLa &&
            (ultimaResposta is null || acao.RegistradaEm >= ultimaResposta.RespondidaEm));

        return new SituacaoDaRua
        {
            Rua = rua,
            UltimaResposta = ultimaResposta,
            UltimaLeitura = ultimaLeitura,
            AtendimentoACaminho = aCaminho
        };
    }
}

using Elo.Domain.Comum;
using Elo.Domain.Ruas;

namespace Elo.Domain.Chamadas;

/// <summary>
/// A "chamada" é o momento em que o coordenador pergunta a todas as ruas do bairro como elas estão.
/// <para>
/// Classe abstrata (abstração + herança): existe a <see cref="ChamadaReal"/>, disparada por um
/// aviso oficial, e a <see cref="ChamadaTreino"/>, um simulado. As regras comuns ficam aqui;
/// o que muda de um tipo para outro é polimórfico (<see cref="Pergunta"/>,
/// <see cref="MontarMensagemParaRua"/>).
/// </para>
/// </summary>
public abstract class Chamada : Entidade
{
    /// <summary>Margem para relógios de celular adiantados ou atrasados.</summary>
    public static readonly TimeSpan ToleranciaDeRelogio = TimeSpan.FromMinutes(5);

    private readonly List<Resposta> _respostas = [];
    private readonly List<AcaoCoordenador> _acoes = [];

    public int BairroId { get; private set; }
    public DateTime IniciadaEm { get; private set; }
    public DateTime? EncerradaEm { get; private set; }

    /// <summary>Quando a chamada acaba sozinha. Só o treino tem prazo.</summary>
    public DateTime? EncerramentoPrevistoEm { get; protected set; }

    /// <summary>Foto do indicador tirada no encerramento.</summary>
    public IndicadorDaChamada? Resultado { get; private set; }

    public IReadOnlyCollection<Resposta> Respostas => _respostas.AsReadOnly();
    public IReadOnlyCollection<AcaoCoordenador> Acoes => _acoes.AsReadOnly();

    protected Chamada()
    {
        // Usado pelo EF Core.
    }

    protected Chamada(int bairroId, DateTime iniciadaEm)
    {
        BairroId = Guarda.IdValido(bairroId, "A chamada precisa pertencer a um bairro.");
        IniciadaEm = iniciadaEm;
    }

    // ---------- Polimorfismo: cada tipo de chamada implementa do seu jeito ----------

    public abstract TipoChamada Tipo { get; }

    /// <summary>Pergunta exibida na tela "Agora" do app da liderança.</summary>
    public abstract string Pergunta { get; }

    /// <summary>Mensagem que o coordenador manda no WhatsApp de uma rua.</summary>
    public abstract string MontarMensagemParaRua(string nomeDaRua);

    // ---------- Regras comuns a todas as chamadas ----------

    public bool EstaAbertaEm(DateTime momento) =>
        EncerradaEm is null && (EncerramentoPrevistoEm is null || momento < EncerramentoPrevistoEm);

    /// <summary>Fim da janela de respostas: o encerramento manual ou o prazo do treino, o que vier antes.</summary>
    public DateTime? FimDaJanela => (EncerradaEm, EncerramentoPrevistoEm) switch
    {
        (DateTime encerrada, DateTime prevista) => encerrada < prevista ? encerrada : prevista,
        (DateTime encerrada, null) => encerrada,
        (null, DateTime prevista) => prevista,
        _ => null
    };

    /// <summary>
    /// Diz se uma resposta dada em <paramref name="respondidaEm"/> pertence a esta chamada.
    /// Uma resposta guardada offline pode chegar depois do encerramento: ela vale se foi
    /// DADA dentro da janela.
    /// </summary>
    public bool AceitaRespostaDadaEm(DateTime respondidaEm) =>
        respondidaEm >= IniciadaEm - ToleranciaDeRelogio &&
        (FimDaJanela is not DateTime fim || respondidaEm <= fim);

    public Resposta RegistrarResposta(
        string? idLocal,
        Rua rua,
        SituacaoRua situacao,
        int? pessoas,
        string? observacao,
        CanalResposta canal,
        DateTime respondidaEm,
        DateTime agora)
    {
        GarantirQueARuaPodeParticipar(rua);

        if (respondidaEm > agora + ToleranciaDeRelogio)
        {
            throw new RegraDeNegocioException("A hora da resposta está no futuro. Confira o relógio do celular.");
        }

        if (!AceitaRespostaDadaEm(respondidaEm))
        {
            throw new RegraDeNegocioException("A resposta foi dada fora do horário desta chamada.");
        }

        var resposta = new Resposta(idLocal, Id, rua.Id, situacao, pessoas, observacao, canal, respondidaEm, agora);
        _respostas.Add(resposta);
        return resposta;
    }

    public AcaoCoordenador RegistrarAcao(Rua rua, TipoAcao tipo, DateTime agora)
    {
        GarantirQueARuaPodeParticipar(rua);

        if (!EstaAbertaEm(agora))
        {
            throw new RegraDeNegocioException("Só dá para registrar ações em uma chamada aberta.");
        }

        var acao = new AcaoCoordenador(Id, rua.Id, tipo, agora);
        _acoes.Add(acao);
        return acao;
    }

    /// <summary>
    /// Encerra a chamada e guarda a foto do indicador.
    /// As respostas da chamada precisam estar carregadas.
    /// </summary>
    public void Encerrar(DateTime agora, IReadOnlyCollection<int> ruasDoBairro)
    {
        if (EncerradaEm is not null)
        {
            throw new RegraDeNegocioException("Esta chamada já foi encerrada.");
        }

        // Um treino que passou do prazo é encerrado na hora prevista, não na hora de agora.
        EncerradaEm = EncerramentoPrevistoEm is DateTime prazo && prazo < agora ? prazo : agora;
        Resultado = IndicadorDaChamada.Calcular(IniciadaEm, ruasDoBairro, _respostas);
    }

    public override string ToString() => $"Chamada {Tipo} #{Id} iniciada em {IniciadaEm:u}";

    private void GarantirQueARuaPodeParticipar(Rua rua)
    {
        ArgumentNullException.ThrowIfNull(rua);

        if (rua.BairroId != BairroId)
        {
            throw new RegraDeNegocioException($"A rua \"{rua.Nome}\" não pertence ao bairro desta chamada.");
        }

        if (!rua.Ativa)
        {
            throw new RegraDeNegocioException($"A rua \"{rua.Nome}\" está desativada.");
        }
    }
}

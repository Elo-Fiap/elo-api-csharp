using Elo.Domain.Comum;

namespace Elo.Domain.Chamadas;

/// <summary>
/// Treino da chamada (simulado). Tem prazo para acabar e deixa claro, em todas as mensagens,
/// que é um treino, para ninguém confundir com um alerta de verdade.
/// </summary>
public sealed class ChamadaTreino : Chamada
{
    public const int DuracaoPadraoMinutos = 30;
    public const int DuracaoMinimaMinutos = 5;
    public const int DuracaoMaximaMinutos = 180;

    public int DuracaoMinutos { get; private set; }

    private ChamadaTreino()
    {
        // Usado pelo EF Core.
    }

    public ChamadaTreino(int bairroId, DateTime iniciadaEm, int duracaoMinutos = DuracaoPadraoMinutos)
        : base(bairroId, iniciadaEm)
    {
        if (duracaoMinutos is < DuracaoMinimaMinutos or > DuracaoMaximaMinutos)
        {
            throw new RegraDeNegocioException(
                $"O treino deve durar de {DuracaoMinimaMinutos} a {DuracaoMaximaMinutos} minutos.");
        }

        DuracaoMinutos = duracaoMinutos;
        EncerramentoPrevistoEm = iniciadaEm.AddMinutes(duracaoMinutos);
    }

    public override TipoChamada Tipo => TipoChamada.Treino;

    public override string Pergunta =>
        "TREINO: isto é um simulado, não há alerta real. Como estaria a sua rua agora?";

    public override string MontarMensagemParaRua(string nomeDaRua) =>
        $"Elo - TREINO (simulado, não é alerta real): como está a {nomeDaRua}? Responda no app para treinar.";

    public override string DescreverAbertura() => "Treino da chamada iniciado (simulado).";

    public override int? DuracaoPrevistaEmMinutos => DuracaoMinutos;
}

using Elo.Domain.Comum;

namespace Elo.Domain.Chamadas;

/// <summary>
/// Chamada de verdade, disparada por um aviso oficial.
/// O Elo não cria alerta próprio, por isso esta chamada não existe sem o aviso que a motivou.
/// Fica aberta até o coordenador encerrar.
/// </summary>
public sealed class ChamadaReal : Chamada
{
    public AvisoOficial Aviso { get; private set; } = null!;

    private ChamadaReal()
    {
        // Usado pelo EF Core.
    }

    public ChamadaReal(int bairroId, AvisoOficial? aviso, DateTime iniciadaEm)
        : base(bairroId, iniciadaEm)
    {
        Aviso = aviso ?? throw new RegraDeNegocioException(
            "Uma chamada real precisa do aviso oficial (INMET ou Defesa Civil) que a motivou.");
    }

    public override TipoChamada Tipo => TipoChamada.Real;

    public override string Pergunta =>
        $"Aviso {Aviso.Fonte} de {Aviso.Evento} ({Aviso.Severidade.Descricao()}). Como está a sua rua agora?";

    public override string MontarMensagemParaRua(string nomeDaRua) =>
        $"Elo: há um aviso oficial de {Aviso.Evento} ({Aviso.Fonte}). Como está a {nomeDaRua}? " +
        "Responda no app: Tudo bem, Água subindo ou Preciso de ajuda.";

    public override string DescreverAbertura() => $"Chamada real aberta pelo aviso {Aviso}.";

    public override AvisoOficial? AvisoQueMotivou => Aviso;
}

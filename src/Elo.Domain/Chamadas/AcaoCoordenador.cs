using Elo.Domain.Comum;

namespace Elo.Domain.Chamadas;

/// <summary>
/// O que o coordenador fez por uma rua durante a chamada
/// ("chamou no WhatsApp", "Vou até lá"). Fica no registro, para saber depois o que foi feito.
/// </summary>
public class AcaoCoordenador : Entidade
{
    public int ChamadaId { get; private set; }
    public int RuaId { get; private set; }
    public TipoAcao Tipo { get; private set; }
    public DateTime RegistradaEm { get; private set; }

    private AcaoCoordenador()
    {
        // Usado pelo EF Core.
    }

    internal AcaoCoordenador(int chamadaId, int ruaId, TipoAcao tipo, DateTime registradaEm)
    {
        ChamadaId = chamadaId;
        RuaId = ruaId;
        Tipo = Guarda.EnumValido(tipo, "Tipo de ação");
        RegistradaEm = registradaEm;
    }
}

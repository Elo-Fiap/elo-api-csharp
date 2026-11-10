using Elo.Domain.Comum;

namespace Elo.Domain.Chamadas;

/// <summary>
/// Referência ao aviso oficial que motivou uma chamada real.
/// O Elo não cria alerta próprio: ele só registra qual aviso oficial estava valendo.
/// </summary>
public sealed record AvisoOficial
{
    public const int TamanhoMaximoFonte = 40;
    public const int TamanhoMaximoIdentificador = 80;
    public const int TamanhoMaximoEvento = 120;

    /// <summary>Quem emitiu o aviso. Ex.: INMET.</summary>
    public string Fonte { get; private init; } = string.Empty;

    /// <summary>Identificador do aviso na fonte.</summary>
    public string Identificador { get; private init; } = string.Empty;

    /// <summary>Tipo de evento. Ex.: Acumulado de Chuva.</summary>
    public string Evento { get; private init; } = string.Empty;

    public SeveridadeAviso Severidade { get; private init; }

    private AvisoOficial()
    {
        // Usado pelo EF Core.
    }

    public AvisoOficial(string fonte, string identificador, string evento, SeveridadeAviso severidade)
    {
        Fonte = Guarda.TextoObrigatorio(fonte, "Fonte do aviso", TamanhoMaximoFonte);
        Identificador = Guarda.TextoObrigatorio(identificador, "Identificador do aviso", TamanhoMaximoIdentificador);
        Evento = Guarda.TextoObrigatorio(evento, "Evento do aviso", TamanhoMaximoEvento);
        Severidade = Guarda.EnumValido(severidade, "Severidade do aviso");
    }

    public override string ToString() => $"{Fonte}: {Evento} ({Severidade.Descricao()})";
}

namespace Elo.Seguranca.Pii;

/// <summary>Os tipos de dado pessoal sensível que o módulo sabe reconhecer.</summary>
public enum TipoDePii
{
    Cpf,
    Rg,
    CartaoSus,
    Email,
    Telefone,
    Senha,
    DadoDeSaude,
    Nome
}

/// <summary>Quanto o vazamento do dado machuca a pessoa (LGPD: dado de saúde e documento são os piores).</summary>
public enum Severidade
{
    Baixa = 1,
    Media = 2,
    Alta = 3
}

/// <summary>
/// Como o texto deve ser lido.
/// Regras de identificador (CPF, RG, CNS, e-mail, telefone, senha, CID) valem nos dois modos.
/// Regras de contexto (saúde por termos, nome) só em <see cref="TextoLivre"/>: num campo estruturado
/// "Rua Dona Ana" ou um plano de evacuação com "acamados" seriam estragados à toa.
/// </summary>
public enum ModoDeAnalise
{
    CampoEstruturado,
    TextoLivre
}

/// <summary>Um trecho sensível achado no texto (posição no texto original) e a máscara que o substitui.</summary>
public sealed record AchadoDePii(TipoDePii Tipo, int Inicio, int Tamanho, string Mascara);

/// <summary>O texto já mascarado e a lista do que foi achado. Nunca guarda o dado original além das posições.</summary>
public sealed record ResultadoDaAnalise(string TextoMascarado, IReadOnlyList<AchadoDePii> Achados)
{
    public bool TemPii => Achados.Count > 0;
}

public static class TipoDePiiExtensoes
{
    /// <summary>Telefone e e-mail identificam menos (e a liderança os informa de propósito): severidade média. O resto é alta.</summary>
    public static Severidade SeveridadeDoTipo(this TipoDePii tipo) =>
        tipo is TipoDePii.Telefone or TipoDePii.Email ? Severidade.Media : Severidade.Alta;
}

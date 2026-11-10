using Elo.Domain.Comum;

namespace Elo.Domain.Ruas;

/// <summary>
/// Value object com o contato da liderança da rua: só um apelido e o WhatsApp.
/// O Elo não guarda nome completo nem dado de morador.
/// Por ser um <c>record</c>, duas lideranças com os mesmos dados são iguais (igualdade por valor).
/// </summary>
public sealed record Lideranca
{
    public const int TamanhoMaximoApelido = 60;

    public string Apelido { get; private init; } = string.Empty;

    /// <summary>Somente dígitos, com DDI e DDD. Ex.: 5511912345678.</summary>
    public string WhatsApp { get; private init; } = string.Empty;

    private Lideranca()
    {
        // Usado pelo EF Core.
    }

    public Lideranca(string apelido, string whatsApp)
    {
        Apelido = Guarda.TextoObrigatorio(apelido, "Apelido da liderança", TamanhoMaximoApelido);
        WhatsApp = NormalizarWhatsApp(whatsApp);
    }

    /// <summary>Monta o link wa.me que abre a conversa já com a mensagem escrita.</summary>
    public string GerarLinkWhatsApp(string mensagem) =>
        $"https://wa.me/{WhatsApp}?text={Uri.EscapeDataString(mensagem)}";

    private static string NormalizarWhatsApp(string? numero)
    {
        var digitos = new string((numero ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length is < 12 or > 13)
        {
            throw new RegraDeNegocioException(
                "WhatsApp deve ter DDI, DDD e número. Ex.: 5511912345678.");
        }

        return digitos;
    }
}

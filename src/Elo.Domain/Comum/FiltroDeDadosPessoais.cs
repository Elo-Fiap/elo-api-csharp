using System.Text.RegularExpressions;

namespace Elo.Domain.Comum;

/// <summary>
/// Campos livres (como a observação de "Preciso de ajuda") podem receber, sem querer,
/// CPF, telefone ou e-mail. Antes de salvar, trocamos esses dados por marcadores
/// (minimização de dados, LGPD). Nomes e doenças não dá para detectar com regex:
/// essa parte fica com o módulo de Cybersecurity da CitizenTech.
/// </summary>
public static partial class FiltroDeDadosPessoais
{
    public const string MarcadorCpf = "[CPF removido]";
    public const string MarcadorTelefone = "[telefone removido]";
    public const string MarcadorEmail = "[e-mail removido]";

    /// <summary>Devolve o texto com CPF, telefone e e-mail mascarados.</summary>
    public static string? Aplicar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return texto;
        }

        // A ordem importa: o CPF (11 dígitos) vem antes do telefone, que é mais genérico.
        var resultado = Email().Replace(texto, MarcadorEmail);
        resultado = Cpf().Replace(resultado, MarcadorCpf);
        resultado = Telefone().Replace(resultado, MarcadorTelefone);
        return resultado;
    }

    public static bool ContemDadoPessoal(string? texto) =>
        !string.IsNullOrWhiteSpace(texto) && Aplicar(texto) != texto;

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    // 123.456.789-09 ou 12345678909
    [GeneratedRegex(@"(?<!\d)\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Cpf();

    // (11) 91234-5678 | 11 912345678 | +55 11 91234-5678 | 91234-5678 | 1234-5678
    [GeneratedRegex(@"(?<!\d)(?:\+?55[\s-]?)?(?:\(?\d{2}\)?[\s-]?)?9?\d{4}[\s-]?\d{4}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Telefone();
}

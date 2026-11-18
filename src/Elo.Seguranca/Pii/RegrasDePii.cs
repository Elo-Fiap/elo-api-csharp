using System.Text.RegularExpressions;

namespace Elo.Seguranca.Pii;

/// <summary>
/// Uma regra de reconhecimento: o regex acha candidatos, a validação opcional descarta os falsos positivos
/// (ex.: 11 dígitos que não são um CPF) e o grupo diz qual trecho do candidato é o dado (ex.: só o valor da senha).
/// <see cref="NoTextoDobrado"/>: o regex foi escrito sem acento e roda no texto sem acento e em minúsculas.
/// </summary>
internal sealed record RegraDePii(
    TipoDePii Tipo,
    Regex Regex,
    Func<string, string> Mascarar,
    Func<Match, bool>? Validar = null,
    string? Grupo = null,
    bool SoTextoLivre = false,
    bool NoTextoDobrado = false);

/// <summary>As regras padrão e as máscaras de cada tipo.</summary>
internal static partial class RegrasDePii
{
    public const string MascaraRg = "[RG oculto]";
    public const string MascaraCartaoSus = "[cartão SUS oculto]";
    public const string MascaraSenha = "[senha oculta]";
    public const string MascaraSaude = "[dado de saúde oculto]";
    public const string MascaraNome = "[nome oculto]";
    public const string MascaraEmail = "[e-mail oculto]";
    public const string MascaraTelefone = "[telefone oculto]";
    public const string MascaraCpf = "[CPF oculto]";

    /// <summary>A ordem não importa para o resultado: o motor resolve as sobreposições pelo tamanho e pela severidade.</summary>
    public static readonly IReadOnlyList<RegraDePii> Padrao =
    [
        new(TipoDePii.Cpf, Cpf(), MascararCpf, Validar: CpfConta),
        new(TipoDePii.Rg, RgFormatado(), _ => MascaraRg),
        new(TipoDePii.Rg, RgComPalavra(), _ => MascaraRg, Grupo: "valor",
            Validar: m => ValidadoresDeDocumento.SoDigitos(m.Groups["valor"].Value).Length >= 5),
        new(TipoDePii.CartaoSus, CartaoSusSolto(), _ => MascaraCartaoSus,
            Validar: m => ValidadoresDeDocumento.CnsValido(ValidadoresDeDocumento.SoDigitos(m.Value))),
        new(TipoDePii.CartaoSus, CartaoSusComPalavra(), _ => MascaraCartaoSus, Grupo: "valor"),
        new(TipoDePii.Email, Email(), MascararEmail),
        new(TipoDePii.Telefone, Telefone(), MascararTelefone),
        new(TipoDePii.Senha, Senha(), _ => MascaraSenha, Grupo: "valor", Validar: SenhaConta),
        new(TipoDePii.DadoDeSaude, Cid(), _ => MascaraSaude),
        new(TipoDePii.DadoDeSaude, TermosDeSaude(), _ => MascaraSaude, SoTextoLivre: true, NoTextoDobrado: true),
        new(TipoDePii.Nome, NomeDePessoa(), _ => MascaraNome, Grupo: "valor", SoTextoLivre: true)
    ];

    /// <summary>
    /// CPF formatado (000.000.000-00) sempre conta: o exemplo "123.456.789-00" tem dígito verificador inválido e precisa ser achado.
    /// Sem a formatação, só conta com dígito verificador válido (senão qualquer número de 11 dígitos viraria CPF).
    /// </summary>
    private static bool CpfConta(Match m) =>
        CpfFormatado().IsMatch(m.Value) || ValidadoresDeDocumento.CpfValido(ValidadoresDeDocumento.SoDigitos(m.Value));

    /// <summary>"senha nenhuma" não é senha: com só um espaço entre a palavra e o valor, o valor precisa ter algum dígito.</summary>
    private static bool SenhaConta(Match m) =>
        m.Groups["sep"].Value.Trim().Length > 0 || m.Groups["valor"].Value.Any(char.IsDigit);

    /// <summary>Mantém o 2º bloco e o dígito verificador: ***.456.***-00. Se não tiver 11 dígitos, esconde tudo.</summary>
    public static string MascararCpf(string valor)
    {
        var d = ValidadoresDeDocumento.SoDigitos(valor);
        return d.Length == 11 ? $"***.{d[3..6]}.***-{d[9..11]}" : MascaraCpf;
    }

    /// <summary>j***@gmail.com: a primeira letra e o domínio ficam, para a coordenação ainda reconhecer a origem.</summary>
    public static string MascararEmail(string valor)
    {
        var arroba = valor.IndexOf('@');
        return arroba > 0 ? $"{valor[0]}***{valor[arroba..]}" : MascaraEmail;
    }

    /// <summary>(**) *****-5678: só os 4 últimos dígitos sobrevivem.</summary>
    public static string MascararTelefone(string valor)
    {
        var d = ValidadoresDeDocumento.SoDigitos(valor);
        return d.Length >= 4 ? $"(**) *****-{d[^4..]}" : MascaraTelefone;
    }

    /// <summary>Máscara de um valor inteiro já conhecido pelo tipo (usada pela taint analysis, que substitui o valor contaminado).</summary>
    public static string MascararPorTipo(TipoDePii tipo, string valor) => tipo switch
    {
        TipoDePii.Cpf => MascararCpf(valor),
        TipoDePii.Rg => MascaraRg,
        TipoDePii.CartaoSus => MascaraCartaoSus,
        TipoDePii.Email => MascararEmail(valor),
        TipoDePii.Telefone => MascararTelefone(valor),
        TipoDePii.Senha => MascaraSenha,
        TipoDePii.DadoDeSaude => MascaraSaude,
        _ => MascaraNome
    };

    // Fronteira de PALAVRA (e não só de dígito): um id hexadecimal, um GUID num caminho de arquivo ou um código
    // com letras ao redor não pode virar CPF, cartão do SUS ou telefone (falso positivo deixaria o farol vermelho à toa).
    [GeneratedRegex(@"(?<![\w.])\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\w)", RegexOptions.CultureInvariant)]
    private static partial Regex Cpf();

    [GeneratedRegex(@"^\d{3}\.\d{3}\.\d{3}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CpfFormatado();

    // 12.345.678-9 ou 12.345.678-X
    [GeneratedRegex(@"(?<![\d.])\d{1,2}\.\d{3}\.\d{3}-[\dXx](?![\dA-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex RgFormatado();

    // RG: 123456789 | RG nº 12.345.678-9 | rg 1234567
    [GeneratedRegex(@"\bRG\b\s*(?:[:.]|n[ºo°]\.?)?\s*(?<valor>[\dXx][\dXx.\-]{4,13})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RgComPalavra();

    // 898 0000 0000 0007 ou 898000000000007 (começa com 1, 2, 7, 8 ou 9)
    [GeneratedRegex(@"(?<!\w)[12789]\d{2}\s?\d{4}\s?\d{4}\s?\d{4}(?!\w)", RegexOptions.CultureInvariant)]
    private static partial Regex CartaoSusSolto();

    // "cartão SUS: 123..." ou "CNS 123...": com a palavra, não precisa validar o dígito
    [GeneratedRegex(@"\b(?:cart[aã]o\s*(?:do\s*)?sus|cns)\b\s*(?:[:.]|n[ºo°]\.?)?\s*(?<valor>\d{3}\s?\d{4}\s?\d{4}\s?\d{4})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CartaoSusComPalavra();

    // Mesma regra do FiltroDeDadosPessoais do domínio.
    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    // Mesma regra do FiltroDeDadosPessoais do domínio, mas com fronteira de palavra (veja o comentário do CPF).
    [GeneratedRegex(@"(?<!\w)(?:\+?55[\s-]?)?(?:\(?\d{2}\)?[\s-]?)?9?\d{4}[\s-]?\d{4}(?!\w)", RegexOptions.CultureInvariant)]
    private static partial Regex Telefone();

    // senha: abc123 | senha é abc123 | password=abc | pin 1234 | código de acesso: 99
    // A pontuação no fim da frase não faz parte do valor. O valor não pode começar com "[": é a própria máscara ("senha: [senha oculta]") e reanalisar não pode achar de novo.
    [GeneratedRegex(@"\b(?:senha|password|pin|c[oó]digo\s+de\s+acesso)\b(?<sep>\s*[:=]\s*|\s+(?:[eé]|eh)\s+|\s+)(?<valor>[^\s\[\]]\S*?)(?=[.,;:!?)\]]*(?:\s|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Senha();

    // CID-10: CID F32, CID: A15.0, cid-b20
    [GeneratedRegex(@"\bCID(?:\s*[-:]\s*|\s+)[A-Z]\d{2}(?:\.\d{1,2})?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Cid();

    /// <summary>
    /// Condições de saúde específicas, escritas sem acento e em minúsculas: o motor compara com o texto dobrado
    /// (mesmas posições do original). Ficam de fora palavras genéricas (saúde, remédio, médico, hospital, doente),
    /// que aparecem em texto que não identifica ninguém.
    /// </summary>
    [GeneratedRegex(
        @"\b(?:diabet(?:es|ic[oa]s?)|hipertens(?:ao|[oa]s?)|insulina|cancer|quimioterapia|hiv|aids|hemodialise|dialise|epilepsia|epileptic[oa]s?|convulsao|convulsoes|avc|derrame|alzheimer|parkinson|demencia|depressao|esquizofreni[ac]|bipolar|autism[o]|autistas?|tea|asma|asmatic[oa]s?|dpoc|oxigenio\s+domiciliar|marca-?passo|acamad[oa]s?|cadeirantes?|gestantes?|gravidas?|laudo\s+medico|tuberculose|hepatite|cardiac[oa]s?|infarto|transplantad[oa]s?|deficiencia\s+(?:visual|auditiva)|surd[oa]s?|ceg[oa]s?)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex TermosDeSaude();

    // "meu nome é Maria Souza", "me chamo José", "nome: Ana Lima", "Sra. Joana Silva". "Dona" e "Seu" ficam de fora: aparecem em nome de rua.
    // Só a pista ignora maiúsculas; o nome precisa começar com maiúscula (1 a 3 palavras, aceitando de/da/do/dos/das no meio).
    [GeneratedRegex(
        @"(?:(?i:meu\s+nome\s+[eé]|me\s+chamo|nome\s*:|\bSr[aª]?\.?))\s+(?<valor>\p{Lu}\p{Ll}+(?:\s+(?:(?:d[aeo]s?)\s+)?\p{Lu}\p{Ll}+){0,2})",
        RegexOptions.CultureInvariant)]
    private static partial Regex NomeDePessoa();
}

using System.Text;

namespace Elo.Seguranca.Pii;

/// <summary>
/// O motor de parsing de PII: acha CPF, RG, cartão SUS, e-mail, telefone, senha, dado de saúde e nome num texto
/// (regex + validação do dígito verificador) e devolve o texto mascarado. Não tem estado: uma instância serve a API inteira.
/// </summary>
public sealed class MotorDePii
{
    public static MotorDePii Padrao { get; } = new();

    private static readonly HashSet<string> ChavesDeSenha = ["senha", "password", "pin", "token"];
    private static readonly HashSet<string> ChavesDeSaude = ["laudo", "diagnostico", "cid", "doenca", "saude", "condicaodesaude"];
    private static readonly HashSet<string> ChavesIsentas =
    [
        // Contato da liderança: informado de propósito para a coordenação chamar (risco aceito e documentado).
        "whatsapp", "linkwhatsapp", "apelido",
        // Identificadores e datas técnicas: nunca são dado pessoal. "identificador" é o código do aviso do INMET.
        "id", "idlocal", "ruaid", "chamadaid", "bairroid", "sensorid", "reguaid", "criadaem", "respondidaem", "identificador"
    ];
    private static readonly HashSet<string> ChavesDeTextoLivre =
        ["observacao", "observacoes", "descricao", "detalhe", "detalhes", "comentario", "mensagem", "texto", "relato", "motivo"];

    /// <summary>
    /// Procura PII no texto. Os achados não se sobrepõem (vence o mais longo, depois o mais severo)
    /// e a máscara é aplicada do fim para o começo, para as posições não andarem.
    /// Com <c>ignorarContato</c> não procura telefone nem e-mail (usado nas respostas HTTP, onde avisos do INMET e contatos institucionais têm telefone).
    /// </summary>
    public ResultadoDaAnalise Analisar(string? texto, ModoDeAnalise modo, bool ignorarContato = false)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return new ResultadoDaAnalise(texto ?? string.Empty, []);
        }

        var candidatos = new List<AchadoDePii>();
        string? dobrado = null;

        foreach (var regra in RegrasDePii.Padrao)
        {
            if (regra.SoTextoLivre && modo != ModoDeAnalise.TextoLivre)
            {
                continue;
            }

            if (ignorarContato && regra.Tipo is TipoDePii.Telefone or TipoDePii.Email)
            {
                continue;
            }

            var alvo = texto;
            if (regra.NoTextoDobrado)
            {
                alvo = dobrado ??= Dobrar(texto);
            }

            foreach (System.Text.RegularExpressions.Match m in regra.Regex.Matches(alvo))
            {
                if (regra.Validar is not null && !regra.Validar(m))
                {
                    continue;
                }

                var trecho = regra.Grupo is null ? m : m.Groups[regra.Grupo];
                if (trecho.Length == 0)
                {
                    continue;
                }

                // A máscara é calculada sobre o texto original (o dobrado só serve para achar a posição).
                var original = texto.Substring(trecho.Index, trecho.Length);
                candidatos.Add(new AchadoDePii(regra.Tipo, trecho.Index, trecho.Length, regra.Mascarar(original)));
            }
        }

        if (candidatos.Count == 0)
        {
            return new ResultadoDaAnalise(texto, []);
        }

        var escolhidos = new List<AchadoDePii>();
        foreach (var candidato in candidatos
                     .OrderByDescending(a => a.Tamanho)
                     .ThenByDescending(a => a.Tipo.SeveridadeDoTipo())
                     .ThenBy(a => a.Inicio))
        {
            var fim = candidato.Inicio + candidato.Tamanho;
            if (!escolhidos.Any(e => candidato.Inicio < e.Inicio + e.Tamanho && e.Inicio < fim))
            {
                escolhidos.Add(candidato);
            }
        }

        escolhidos.Sort((a, b) => a.Inicio.CompareTo(b.Inicio));

        var resultado = new StringBuilder(texto);
        for (var i = escolhidos.Count - 1; i >= 0; i--)
        {
            var achado = escolhidos[i];
            resultado.Remove(achado.Inicio, achado.Tamanho).Insert(achado.Inicio, achado.Mascara);
        }

        return new ResultadoDaAnalise(resultado.ToString(), escolhidos);
    }

    /// <summary>Máscara de um valor inteiro já classificado (CPF vira ***.456.***-00, e assim por diante).</summary>
    public static string MascararValor(TipoDePii tipo, string valor) => RegrasDePii.MascararPorTipo(tipo, valor);

    /// <summary>
    /// O nome do campo já diz o que ele guarda: "senha", "laudo", "documento"... Quando diz, o valor inteiro é dado sensível.
    /// "nome" fica de fora de propósito: bairros e ruas têm nome.
    /// </summary>
    public TipoDePii? ClassificarChave(string chave)
    {
        var k = NormalizarChave(chave);
        return k switch
        {
            "cpf" or "documento" => TipoDePii.Cpf,
            "rg" => TipoDePii.Rg,
            "cartaosus" or "cns" => TipoDePii.CartaoSus,
            "email" => TipoDePii.Email,
            "telefone" or "celular" => TipoDePii.Telefone,
            _ when ChavesDeSenha.Contains(k) => TipoDePii.Senha,
            _ when ChavesDeSaude.Contains(k) => TipoDePii.DadoDeSaude,
            _ => null
        };
    }

    /// <summary>Chaves que nunca são analisadas (contato da liderança e identificadores técnicos).</summary>
    public bool EhChaveIsenta(string chave) => ChavesIsentas.Contains(NormalizarChave(chave));

    /// <summary>Chaves que guardam texto digitado pela pessoa: aí vale a análise de contexto (saúde, nome).</summary>
    public bool EhChaveDeTextoLivre(string chave) => ChavesDeTextoLivre.Contains(NormalizarChave(chave));

    public ModoDeAnalise ModoDaChave(string chave) =>
        EhChaveDeTextoLivre(chave) ? ModoDeAnalise.TextoLivre : ModoDeAnalise.CampoEstruturado;

    /// <summary>Minúsculas, sem acento, sem "_" nem "-" (e sem espaços): "Cartao_SUS" e "cartaoSus" viram "cartaosus".</summary>
    public static string NormalizarChave(string chave)
    {
        var sb = new StringBuilder(chave.Length);
        foreach (var c in Dobrar(chave))
        {
            if (c is not ('_' or '-' or ' '))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Minúsculas e sem acento, caractere a caractere: o resultado tem o mesmo comprimento do texto,
    /// então uma posição achada no texto dobrado vale no original.
    /// Só as letras latinas acentuadas (á, ç, õ...) perdem o acento; emoji e outros alfabetos ficam como estão
    /// (normalizar metade de um emoji, um "surrogate" sozinho, lançaria exceção e derrubaria o pedido de ajuda).
    /// </summary>
    internal static string Dobrar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto)
        {
            var baixo = char.ToLowerInvariant(c);
            if (baixo is >= 'À' and <= 'ɏ')
            {
                baixo = baixo.ToString().Normalize(NormalizationForm.FormD)[0];
            }

            sb.Append(baixo);
        }

        return sb.ToString();
    }
}

using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;

namespace Elo.Seguranca.Traducao;

/// <summary>
/// A tradução sem rede: mensagens prontas por saída (sink) e por tipo de dado, em duas frases simples, sem termo técnico.
/// É o que o painel mostra quando não há chave do LLM, quando o LLM está desligado ou quando ele falha,
/// então o cidadão sempre lê uma explicação (e os testes nunca acessam a rede).
/// </summary>
public sealed class TradutorPadrao : ITradutorDeRisco
{
    public const string NomeDoTradutor = "modelo padrão";

    public string Nome => NomeDoTradutor;

    public Task<string> TraduzirAsync(EventoDeSeguranca evento, CancellationToken ct) =>
        Task.FromResult(Traduzir(evento.Etapa, evento.Sink, evento.Tipos));

    /// <summary>
    /// Monta as duas frases. Com mais de um tipo no mesmo evento, fala de "uma informação pessoal":
    /// listar todos deixaria a frase comprida para quem lê.
    /// </summary>
    public static string Traduzir(EtapaDoEvento etapa, Sink? sink, IReadOnlyList<TipoDePii> tipos)
    {
        var (objeto, feminino) = DescreverDado(tipos);

        if (etapa == EtapaDoEvento.Entrada || sink is null)
        {
            return $"O Elo percebeu que você enviou {objeto}. " +
                   "Ele marcou essa informação como delicada e vai protegê-la para que não apareça em lugar nenhum.";
        }

        var sujeito = ComMaiuscula(objeto);
        return sink.Value switch
        {
            Sink.Banco =>
                $"{sujeito} que você enviou ia ficar {Concordar("gravado", feminino)} no registro interno do sistema. " +
                "O Elo escondeu esse dado antes de gravar, e ninguém consegue lê-lo.",
            Sink.Log =>
                $"{sujeito} que você enviou ia aparecer nos registros de funcionamento do sistema, que a equipe técnica consulta. " +
                "O Elo apagou esse dado antes, e ninguém consegue lê-lo.",
            Sink.RespostaHttp =>
                $"{sujeito} que você enviou ia aparecer numa tela que qualquer pessoa pode abrir. " +
                "O Elo escondeu esse dado, e só aparece o que é seguro mostrar.",
            Sink.Llm =>
                $"{sujeito} que você enviou quase foi mandad{(feminino ? "a" : "o")} para um serviço externo de inteligência artificial. " +
                "O Elo escondeu esse dado antes, e ele não saiu do sistema.",
            _ => throw new ArgumentOutOfRangeException(nameof(sink), sink, "Saída sem mensagem pronta.")
        };
    }

    /// <summary>A frase padrão de cada tipo, no feminino ou masculino, para as palavras da frase concordarem.</summary>
    private static (string Objeto, bool Feminino) DescreverDado(IReadOnlyList<TipoDePii> tipos)
    {
        var distintos = tipos.Distinct().ToArray();
        if (distintos.Length != 1)
        {
            return ("uma informação pessoal", true);
        }

        return distintos[0] switch
        {
            TipoDePii.Cpf => ("o seu CPF", false),
            TipoDePii.Rg => ("o número do seu RG", false),
            TipoDePii.CartaoSus => ("o número do seu cartão do SUS", false),
            TipoDePii.Email => ("o seu e-mail", false),
            TipoDePii.Telefone => ("o seu telefone", false),
            TipoDePii.Senha => ("uma senha", true),
            TipoDePii.DadoDeSaude => ("uma informação de saúde", true),
            TipoDePii.Nome => ("o seu nome", false),
            _ => ("uma informação pessoal", true)
        };
    }

    private static string Concordar(string masculino, bool feminino) =>
        feminino ? string.Concat(masculino.AsSpan(0, masculino.Length - 1), "a") : masculino;

    private static string ComMaiuscula(string texto) => char.ToUpperInvariant(texto[0]) + texto[1..];
}

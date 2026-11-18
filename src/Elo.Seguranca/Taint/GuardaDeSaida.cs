using System.Text.Json.Nodes;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;

namespace Elo.Seguranca.Taint;

/// <summary>
/// A camada de validação antes das saídas (sinks): log, banco, resposta HTTP e LLM passam por aqui.
/// Primeiro troca por máscara o que a entrada marcou como sensível (taint analysis) e depois roda o motor de PII
/// (rede de segurança para o que não veio nesta requisição, como dado legado do banco).
/// Cada vez que algo é barrado registra UM evento de segurança, sem o valor.
/// </summary>
public sealed class GuardaDeSaida(MotorDePii motor, IAcessoAoContexto acesso, IRegistroDeEventos registro)
{
    // Registrar o evento não pode gerar log que volte a passar pela guarda e crie outro evento (laço infinito).
    private static readonly AsyncLocal<bool> EmRegistro = new();

    /// <summary>
    /// Limpa um texto antes de ele sair. Devolve o texto limpo; se havia algo sensível, registra um evento.
    /// O <c>onde</c> é a categoria do log, "Entidade.Propriedade" do banco, a rota da resposta...
    /// </summary>
    public ResultadoDaAnalise Verificar(string conteudo, Sink sink, ModoDeAnalise modo, string onde)
    {
        var resultado = Limpar(conteudo, sink, modo);
        if (resultado.TemPii)
        {
            RegistrarBloqueio(sink, onde, resultado.Achados.Select(a => a.Tipo), resultado.Achados.Count);
        }

        return resultado;
    }

    /// <summary>
    /// Limpa todos os valores de texto de um JSON (muda o próprio nó). Pula as chaves isentas e escolhe o modo pela chave.
    /// Na resposta HTTP não procura telefone nem e-mail por padrão (avisos do INMET e contatos institucionais têm telefone):
    /// eles só são barrados se estiverem marcados como sensíveis nesta requisição.
    /// Devolve <c>null</c> quando nada mudou (quem chama mantém os bytes originais) e o JSON limpo quando mudou.
    /// Um evento por chamada, somando os achados.
    /// </summary>
    public JsonNode? VerificarJson(JsonNode raiz, Sink sink, string onde)
    {
        var tipos = new List<TipoDePii>();
        var quantidade = 0;

        var novaRaiz = LimparNo(raiz, chave: null, sink, tipos, ref quantidade);

        if (quantidade == 0)
        {
            return null;
        }

        RegistrarBloqueio(sink, onde, tipos, quantidade);
        return novaRaiz ?? raiz;
    }

    /// <summary>Devolve o nó novo quando o próprio nó é um texto que mudou (a raiz é um texto); nos demais casos muda no lugar e devolve null.</summary>
    private JsonNode? LimparNo(JsonNode? no, string? chave, Sink sink, List<TipoDePii> tipos, ref int quantidade)
    {
        switch (no)
        {
            case JsonObject objeto:
                foreach (var (nome, filho) in objeto.ToArray())
                {
                    if (motor.EhChaveIsenta(nome))
                    {
                        continue;
                    }

                    var trocado = LimparNo(filho, nome, sink, tipos, ref quantidade);
                    if (trocado is not null)
                    {
                        objeto[nome] = trocado;
                    }
                }

                return null;

            case JsonArray lista:
                for (var i = 0; i < lista.Count; i++)
                {
                    // Os itens de uma lista herdam a chave da lista ("observacoes": ["...", "..."]).
                    var trocado = LimparNo(lista[i], chave, sink, tipos, ref quantidade);
                    if (trocado is not null)
                    {
                        lista[i] = trocado;
                    }
                }

                return null;

            case JsonValue valor when valor.TryGetValue<string>(out var texto):
                var modo = chave is null ? ModoDeAnalise.CampoEstruturado : motor.ModoDaChave(chave);
                var limpo = Limpar(texto, sink, modo);
                if (!limpo.TemPii)
                {
                    return null;
                }

                tipos.AddRange(limpo.Achados.Select(a => a.Tipo));
                quantidade += limpo.Achados.Count;
                return JsonValue.Create(limpo.TextoMascarado);

            default:
                return null;
        }
    }

    /// <summary>
    /// Taint primeiro (o valor marcado some mesmo sem padrão), motor de PII depois. Não registra evento.
    /// Aqui os achados servem só para contar e dizer os tipos: as posições deles não batem com o texto original.
    /// </summary>
    private ResultadoDaAnalise Limpar(string conteudo, Sink sink, ModoDeAnalise modo)
    {
        if (string.IsNullOrEmpty(conteudo))
        {
            return new ResultadoDaAnalise(conteudo ?? string.Empty, []);
        }

        var achados = new List<AchadoDePii>();
        var texto = conteudo;

        var contaminados = acesso.Atual?.Valores;
        if (contaminados is { Count: > 0 })
        {
            // Mais longo primeiro: "123.456.789-00" tem de sumir antes de qualquer pedaço dele.
            foreach (var contaminado in contaminados.OrderByDescending(v => v.Valor.Length))
            {
                var posicao = texto.IndexOf(contaminado.Valor, StringComparison.Ordinal);
                if (posicao < 0)
                {
                    continue;
                }

                var mascara = MotorDePii.MascararValor(contaminado.Tipo, contaminado.Valor);
                while (posicao >= 0)
                {
                    achados.Add(new AchadoDePii(contaminado.Tipo, posicao, contaminado.Valor.Length, mascara));
                    posicao = texto.IndexOf(contaminado.Valor, posicao + contaminado.Valor.Length, StringComparison.Ordinal);
                }

                texto = texto.Replace(contaminado.Valor, mascara, StringComparison.Ordinal);
            }
        }

        var analise = motor.Analisar(texto, modo, ignorarContato: sink == Sink.RespostaHttp);
        achados.AddRange(analise.Achados);

        return achados.Count == 0 ? new ResultadoDaAnalise(conteudo, []) : new ResultadoDaAnalise(analise.TextoMascarado, achados);
    }

    private void RegistrarBloqueio(Sink sink, string onde, IEnumerable<TipoDePii> tipos, int quantidade)
    {
        if (EmRegistro.Value)
        {
            return;
        }

        EmRegistro.Value = true;
        try
        {
            registro.Registrar(
                EtapaDoEvento.Saida, sink, AcaoDeSeguranca.Bloqueado, [.. tipos.Distinct()], quantidade, onde);
        }
        finally
        {
            EmRegistro.Value = false;
        }
    }
}

using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Elo.Seguranca.Traducao;
using Microsoft.Extensions.Time.Testing;

namespace Elo.Tests.Unitarios.Seguranca;

public class TradutorPadraoTests
{
    private static readonly string[] TermosTecnicos =
        ["PII", "taint", "sink", "HTTP", "log", "banco de dados", "JSON", "API", "mascar", "regex", "token", "payload", "LGPD", "evento"];

    public static IEnumerable<object?[]> Combinacoes()
    {
        foreach (var tipo in Enum.GetValues<TipoDePii>())
        {
            yield return [EtapaDoEvento.Entrada, null, tipo];
            foreach (var sink in Enum.GetValues<Sink>())
            {
                yield return [EtapaDoEvento.Saida, sink, tipo];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void Toda_combinacao_de_saida_e_tipo_tem_mensagem_curta_e_sem_termo_tecnico(EtapaDoEvento etapa, Sink? sink, TipoDePii tipo)
    {
        var mensagem = TradutorPadrao.Traduzir(etapa, sink, [tipo]);

        Assert.False(string.IsNullOrWhiteSpace(mensagem));
        Assert.InRange(ContarFrases(mensagem), 1, 2);
        foreach (var termo in TermosTecnicos)
        {
            Assert.DoesNotContain(termo, mensagem, StringComparison.OrdinalIgnoreCase);
        }

        // Os nomes da enum (DadoDeSaude, Cpf...) não vazam para o texto do cidadão.
        Assert.DoesNotContain(tipo.ToString(), mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EtapaDoEvento.Entrada, null)]
    [InlineData(EtapaDoEvento.Saida, Sink.Banco)]
    [InlineData(EtapaDoEvento.Saida, Sink.Log)]
    [InlineData(EtapaDoEvento.Saida, Sink.RespostaHttp)]
    [InlineData(EtapaDoEvento.Saida, Sink.Llm)]
    public void Varios_tipos_no_mesmo_evento_viram_uma_mensagem_so_de_duas_frases(EtapaDoEvento etapa, Sink? sink)
    {
        var mensagem = TradutorPadrao.Traduzir(etapa, sink, [TipoDePii.Cpf, TipoDePii.Senha, TipoDePii.DadoDeSaude]);

        Assert.InRange(ContarFrases(mensagem), 1, 2);
        Assert.Contains("informação pessoal", mensagem);
    }

    [Fact]
    public void A_mensagem_de_saude_no_banco_diz_o_que_aconteceu_e_que_o_dado_foi_escondido()
    {
        var mensagem = TradutorPadrao.Traduzir(EtapaDoEvento.Saida, Sink.Banco, [TipoDePii.DadoDeSaude]);

        Assert.Equal(
            "Uma informação de saúde que você enviou ia ficar gravada no registro interno do sistema. " +
            "O Elo escondeu esse dado antes de gravar, e ninguém consegue lê-lo.",
            mensagem);
    }

    [Fact]
    public async Task O_tradutor_le_o_evento_e_se_apresenta_como_modelo_padrao()
    {
        var registro = new RegistroDeEventosEmMemoria(new FakeTimeProvider());
        var evento = registro.Registrar(EtapaDoEvento.Saida, Sink.Log, AcaoDeSeguranca.Bloqueado, [TipoDePii.Cpf], 1, "Categoria");
        var tradutor = new TradutorPadrao();

        var mensagem = await tradutor.TraduzirAsync(evento, CancellationToken.None);

        Assert.Equal("modelo padrão", tradutor.Nome);
        Assert.Equal(TradutorPadrao.Traduzir(EtapaDoEvento.Saida, Sink.Log, [TipoDePii.Cpf]), mensagem);
        Assert.StartsWith("O seu CPF", mensagem);
    }

    /// <summary>Uma frase termina em ponto final; nenhuma mensagem usa abreviação com ponto.</summary>
    private static int ContarFrases(string texto) => texto.Count(c => c == '.');
}

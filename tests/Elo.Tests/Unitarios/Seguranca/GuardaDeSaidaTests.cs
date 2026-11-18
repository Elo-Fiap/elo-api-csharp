using System.Text.Json.Nodes;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Microsoft.Extensions.Time.Testing;

namespace Elo.Tests.Unitarios.Seguranca;

public class GuardaDeSaidaTests
{
    private static (GuardaDeSaida guarda, RegistroDeEventosEmMemoria registro, ContextoDeContaminacao tinta) Montar()
    {
        var tinta = new ContextoDeContaminacao();
        var registro = new RegistroDeEventosEmMemoria(new FakeTimeProvider());
        return (new GuardaDeSaida(MotorDePii.Padrao, new AcessoDeTeste(tinta), registro), registro, tinta);
    }

    [Fact]
    public void Valor_contaminado_some_do_log_mesmo_sem_padrao_reconhecivel()
    {
        var (guarda, registro, tinta) = Montar();
        tinta.Marcar("abc12345", TipoDePii.Senha, "corpo $.chave");

        var r = guarda.Verificar("Falha ao validar abc12345 do usuário", Sink.Log, ModoDeAnalise.TextoLivre, "Categoria");

        Assert.Equal("Falha ao validar [senha oculta] do usuário", r.TextoMascarado);
        var evento = Assert.Single(registro.Listar(null, 10));
        Assert.Equal(Sink.Log, evento.Sink);
        Assert.Equal(EtapaDoEvento.Saida, evento.Etapa);
        Assert.Equal(AcaoDeSeguranca.Bloqueado, evento.Acao);
        Assert.Equal([TipoDePii.Senha], evento.Tipos);
    }

    [Fact]
    public void Contaminado_usa_a_mascara_do_tipo_e_todas_as_ocorrencias_somem()
    {
        var (guarda, _, tinta) = Montar();
        tinta.Marcar("123.456.789-00", TipoDePii.Cpf, "corpo $.documento");

        var r = guarda.Verificar("a 123.456.789-00 e de novo 123.456.789-00", Sink.Banco, ModoDeAnalise.CampoEstruturado, "X.Y");

        Assert.Equal("a ***.456.***-00 e de novo ***.456.***-00", r.TextoMascarado);
    }

    [Fact]
    public void Evento_nao_guarda_o_valor_em_nenhum_campo()
    {
        var (guarda, registro, tinta) = Montar();
        tinta.Marcar("segredo-super-secreto", TipoDePii.Senha, "corpo $.x");

        guarda.Verificar("tem segredo-super-secreto e 123.456.789-00 e diabética aqui", Sink.Log, ModoDeAnalise.TextoLivre, "Elo.Categoria");

        var evento = Assert.Single(registro.Listar(null, 10));
        var tudo = string.Join('|', evento.DescricaoTecnica, evento.Onde, evento.MensagemAoCidadao, string.Join(',', evento.Tipos));
        Assert.DoesNotContain("segredo", tudo);
        Assert.DoesNotContain("123.456", tudo);
        Assert.DoesNotContain("diabética", tudo);
        Assert.Equal("Taint sink bloqueado: Senha, Cpf, DadoDeSaude em Log (Elo.Categoria)", evento.DescricaoTecnica);
        Assert.Equal(Severidade.Alta, evento.Severidade);
        Assert.Equal(3, evento.Quantidade);
    }

    [Fact]
    public void Texto_limpo_nao_gera_evento()
    {
        var (guarda, registro, _) = Montar();

        var r = guarda.Verificar("Pedido de ajuda na rua 2 (chamada 7)", Sink.Log, ModoDeAnalise.TextoLivre, "C");

        Assert.False(r.TemPii);
        Assert.Empty(registro.Listar(null, 10));
    }

    [Fact]
    public void Registrar_o_evento_nao_pode_gerar_outro_evento_ao_logar_de_volta()
    {
        // Um registro que "loga" ao registrar, e o log passa pela guarda de novo: sem proteção seria um laço infinito.
        var registroReal = new RegistroDeEventosEmMemoria(new FakeTimeProvider());
        GuardaDeSaida? guarda = null;
        var chamadas = 0;
        var registro = new RegistroQueLoga(registroReal, () =>
        {
            chamadas++;
            guarda!.Verificar("CPF 123.456.789-00 de novo", Sink.Log, ModoDeAnalise.TextoLivre, "Registro");
        });
        guarda = new GuardaDeSaida(MotorDePii.Padrao, new AcessoDeTeste(), registro);

        guarda.Verificar("CPF 123.456.789-00", Sink.Log, ModoDeAnalise.TextoLivre, "C");

        Assert.Equal(1, chamadas);
        Assert.Single(registroReal.Listar(null, 10));
    }

    [Fact]
    public void Sem_contexto_de_requisicao_so_o_motor_de_padroes_age()
    {
        var registro = new RegistroDeEventosEmMemoria(new FakeTimeProvider());
        var guarda = new GuardaDeSaida(MotorDePii.Padrao, new AcessoDeTeste(), registro);

        var r = guarda.Verificar("CPF 123.456.789-00", Sink.Banco, ModoDeAnalise.CampoEstruturado, "A.B");

        Assert.Equal("CPF ***.456.***-00", r.TextoMascarado);
    }

    [Fact]
    public void Resposta_http_nao_barra_telefone_nem_email_a_menos_que_estejam_contaminados()
    {
        var (guarda, registro, tinta) = Montar();

        Assert.False(guarda.Verificar("Defesa Civil (11) 3396-1234 ou defesa@sp.gov.br", Sink.RespostaHttp, ModoDeAnalise.TextoLivre, "GET /x").TemPii);

        tinta.Marcar("(11) 91234-5678", TipoDePii.Telefone, "corpo $.telefone");
        var r = guarda.Verificar("ligar para (11) 91234-5678", Sink.RespostaHttp, ModoDeAnalise.TextoLivre, "GET /x");

        Assert.Equal("ligar para (**) *****-5678", r.TextoMascarado);
        Assert.Single(registro.Listar(null, 10));
    }

    // ---------- JSON ----------

    [Fact]
    public void Json_limpa_so_o_que_precisa_e_gera_um_evento()
    {
        var (guarda, registro, _) = Montar();
        var json = JsonNode.Parse("""
            {"id":"7","nome":"Rua Dona Ana","observacao":"CPF 123.456.789-00 e a vizinha diabética",
             "lista":[{"descricao":"RG 12.345.678-9"}],"pessoas":3,"idLocal":"123.456.789-00"}
            """)!;

        var limpo = guarda.VerificarJson(json, Sink.RespostaHttp, "GET /api/x");

        Assert.NotNull(limpo);
        Assert.Equal("Rua Dona Ana", (string?)limpo["nome"]);
        Assert.Equal("CPF ***.456.***-00 e a vizinha [dado de saúde oculto]", (string?)limpo["observacao"]);
        Assert.Equal("RG [RG oculto]", (string?)limpo["lista"]![0]!["descricao"]);
        Assert.Equal(3, (int?)limpo["pessoas"]);
        Assert.Equal("123.456.789-00", (string?)limpo["idLocal"]); // chave isenta
        var evento = Assert.Single(registro.Listar(null, 10));
        Assert.Equal(Sink.RespostaHttp, evento.Sink);
        Assert.Equal("GET /api/x", evento.Onde);
        Assert.Equal(3, evento.Quantidade);
    }

    [Fact]
    public void Json_sem_nada_sensivel_devolve_nulo_e_nao_gera_evento()
    {
        var (guarda, registro, _) = Montar();
        var json = JsonNode.Parse("""{"nome":"Rua Dona Ana","observacao":"Plano de evacuação ok","contato":"(11) 91234-5678"}""")!;

        Assert.Null(guarda.VerificarJson(json, Sink.RespostaHttp, "GET /x"));
        Assert.Empty(registro.Listar(null, 10));
    }

    [Fact]
    public void Json_em_campo_estruturado_nao_estraga_palavras_de_contexto()
    {
        var (guarda, _, _) = Montar();
        var json = JsonNode.Parse("""{"local":"Abrigo para acamados","observacao":"idoso acamado"}""")!;

        var limpo = guarda.VerificarJson(json, Sink.RespostaHttp, "GET /x");

        Assert.Equal("Abrigo para acamados", (string?)limpo!["local"]);
        Assert.Equal("idoso [dado de saúde oculto]", (string?)limpo["observacao"]);
    }

    [Fact]
    public void Contexto_de_marcacao_ignora_valores_curtos_e_nao_imprime_o_valor()
    {
        var tinta = new ContextoDeContaminacao();
        tinta.Marcar("abc", TipoDePii.Senha, "x");
        tinta.Marcar("  ", TipoDePii.Senha, "x");
        tinta.Marcar("abcd1234", TipoDePii.Senha, "corpo $.senha");

        var valor = Assert.Single(tinta.Valores);
        Assert.DoesNotContain("abcd1234", valor.ToString());
        Assert.DoesNotContain("abcd1234", tinta.ToString());
    }

    private sealed class RegistroQueLoga(IRegistroDeEventos real, Action aoRegistrar) : IRegistroDeEventos
    {
        public EventoDeSeguranca Registrar(EtapaDoEvento etapa, Sink? sink, AcaoDeSeguranca acao, IReadOnlyList<TipoDePii> tipos, int quantidade, string onde, string? descricaoTecnica = null)
        {
            aoRegistrar();
            return real.Registrar(etapa, sink, acao, tipos, quantidade, onde, descricaoTecnica);
        }

        public IReadOnlyList<EventoDeSeguranca> Listar(DateTimeOffset? desde, int limite) => real.Listar(desde, limite);
        public bool DefinirMensagem(long id, string mensagem, string traduzidaPor) => real.DefinirMensagem(id, mensagem, traduzidaPor);
        public System.Threading.Channels.ChannelReader<long> NovosEventos => real.NovosEventos;
    }
}

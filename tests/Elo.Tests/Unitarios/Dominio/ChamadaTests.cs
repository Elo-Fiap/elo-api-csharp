using Elo.Domain.Chamadas;
using Elo.Domain.Comum;
using static Elo.Tests.Fabrica;

namespace Elo.Tests.Unitarios.Dominio;

public class ChamadaTests
{
    [Fact]
    public void Chamada_real_sem_aviso_oficial_nao_pode_ser_criada()
    {
        var erro = Assert.Throws<RegraDeNegocioException>(() => new ChamadaReal(1, aviso: null, Inicio));

        Assert.Contains("aviso oficial", erro.Message);
    }

    [Fact]
    public void Treino_termina_sozinho_no_prazo()
    {
        var treino = NovoTreino(duracaoMinutos: 30);

        Assert.True(treino.EstaAbertaEm(Inicio.AddMinutes(29)));
        Assert.False(treino.EstaAbertaEm(Inicio.AddMinutes(30)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(181)]
    public void Treino_com_duracao_fora_do_limite_e_recusado(int minutos)
    {
        Assert.Throws<RegraDeNegocioException>(() => new ChamadaTreino(1, Inicio, minutos));
    }

    [Fact]
    public void Polimorfismo_cada_tipo_de_chamada_monta_a_propria_pergunta_e_mensagem()
    {
        Chamada[] chamadas = [NovaChamadaReal(), NovoTreino()];

        var mensagens = chamadas.Select(chamada => chamada.MontarMensagemParaRua("Rua A")).ToList();

        Assert.Equal([TipoChamada.Real, TipoChamada.Treino], chamadas.Select(chamada => chamada.Tipo));
        Assert.Contains("aviso oficial", mensagens[0]);
        Assert.DoesNotContain("TREINO", mensagens[0]);
        Assert.Contains("TREINO", mensagens[1]);
        Assert.StartsWith("TREINO", chamadas[1].Pergunta);
    }

    [Fact]
    public void Polimorfismo_cada_tipo_descreve_a_abertura_e_diz_o_que_tem()
    {
        Chamada[] chamadas = [NovaChamadaReal(), NovoTreino(duracaoMinutos: 20)];

        Assert.Equal(
            ["Chamada real aberta pelo aviso INMET: Acumulado de Chuva (perigo).", "Treino da chamada iniciado (simulado)."],
            chamadas.Select(chamada => chamada.DescreverAbertura()));
        Assert.Equal(new AvisoOficial?[] { NovoAviso(), null }, chamadas.Select(chamada => chamada.AvisoQueMotivou));
        Assert.Equal(new int?[] { null, 20 }, chamadas.Select(chamada => chamada.DuracaoPrevistaEmMinutos));
    }

    [Fact]
    public void Resposta_de_rua_de_outro_bairro_e_recusada()
    {
        var chamada = NovaChamadaReal(bairroId: 1);
        var ruaDeOutroBairro = NovaRua(10, bairroId: 2);

        Assert.Throws<RegraDeNegocioException>(() => Responder(chamada, ruaDeOutroBairro, SituacaoRua.TudoBem, 5));
    }

    [Fact]
    public void Resposta_de_rua_desativada_e_recusada()
    {
        var chamada = NovaChamadaReal();
        var rua = NovaRua(1);
        rua.Atualizar(rua.Nome, rua.Microarea, rua.NivelRisco, rua.Lideranca, ativa: false);

        Assert.Throws<RegraDeNegocioException>(() => Responder(chamada, rua, SituacaoRua.TudoBem, 5));
    }

    [Fact]
    public void Resposta_dada_antes_da_chamada_comecar_e_recusada()
    {
        var chamada = NovaChamadaReal();

        Assert.Throws<RegraDeNegocioException>(() => Responder(chamada, NovaRua(1), SituacaoRua.TudoBem, aposMinutos: -10));
    }

    [Fact]
    public void Resposta_com_hora_no_futuro_e_recusada()
    {
        var chamada = NovaChamadaReal();

        var erro = Assert.Throws<RegraDeNegocioException>(() => chamada.RegistrarResposta(
            Guid.NewGuid().ToString("N"), NovaRua(1), SituacaoRua.TudoBem, null, null, CanalResposta.App,
            respondidaEm: Inicio.AddMinutes(30),
            agora: Inicio.AddMinutes(10)));

        Assert.Contains("futuro", erro.Message);
    }

    [Fact]
    public void Resposta_guardada_offline_vale_mesmo_chegando_depois_do_encerramento()
    {
        var chamada = NovaChamadaReal();
        var rua = NovaRua(1);
        chamada.Encerrar(Inicio.AddMinutes(40), [rua.Id]);

        // Respondida aos 20 minutos (sem internet), recebida só aos 90.
        var resposta = chamada.RegistrarResposta(
            Guid.NewGuid().ToString("N"), rua, SituacaoRua.AguaSubindo, null, null, CanalResposta.App,
            respondidaEm: Inicio.AddMinutes(20),
            agora: Inicio.AddMinutes(90));

        Assert.Equal(Inicio.AddMinutes(20), resposta.RespondidaEm);
        Assert.Equal(Inicio.AddMinutes(90), resposta.RecebidaEm);
    }

    [Fact]
    public void Resposta_dada_depois_do_encerramento_e_recusada()
    {
        var chamada = NovaChamadaReal();
        chamada.Encerrar(Inicio.AddMinutes(40), [1]);

        Assert.Throws<RegraDeNegocioException>(() => Responder(chamada, NovaRua(1), SituacaoRua.TudoBem, aposMinutos: 50));
    }

    [Fact]
    public void Encerrar_guarda_a_foto_do_indicador()
    {
        var chamada = NovaChamadaReal();
        var (a, b, c) = (NovaRua(1, "A"), NovaRua(2, "B"), NovaRua(3, "C"));
        Responder(chamada, a, SituacaoRua.TudoBem, 3);
        Responder(chamada, b, SituacaoRua.PrecisoDeAjuda, 10, pessoas: 2);
        Responder(chamada, c, SituacaoRua.TudoBem, 25); // depois dos 15 minutos

        chamada.Encerrar(Inicio.AddMinutes(30), [1, 2, 3, 4]); // a rua 4 ficou em silêncio

        Assert.NotNull(chamada.Resultado);
        Assert.Equal(4, chamada.Resultado.TotalDeRuas);
        Assert.Equal(2, chamada.Resultado.RuasConhecidasNaJanela);
        Assert.Equal(50, chamada.Resultado.PercentualConhecidoNaJanela);
        Assert.Equal(1, chamada.Resultado.RuasEmSilencio);
    }

    [Fact]
    public void Encerrar_duas_vezes_e_recusado()
    {
        var chamada = NovaChamadaReal();
        chamada.Encerrar(Inicio.AddMinutes(30), [1]);

        Assert.Throws<RegraDeNegocioException>(() => chamada.Encerrar(Inicio.AddMinutes(31), [1]));
    }

    [Fact]
    public void Treino_vencido_e_encerrado_na_hora_prevista_e_nao_na_hora_atual()
    {
        var treino = NovoTreino(duracaoMinutos: 30);

        treino.Encerrar(Inicio.AddHours(5), [1]);

        Assert.Equal(Inicio.AddMinutes(30), treino.EncerradaEm);
    }

    [Fact]
    public void Acao_do_coordenador_em_chamada_encerrada_e_recusada()
    {
        var chamada = NovaChamadaReal();
        chamada.Encerrar(Inicio.AddMinutes(30), [1]);

        Assert.Throws<RegraDeNegocioException>(() =>
            chamada.RegistrarAcao(NovaRua(1), TipoAcao.VouAteLa, Inicio.AddMinutes(31)));
    }
}

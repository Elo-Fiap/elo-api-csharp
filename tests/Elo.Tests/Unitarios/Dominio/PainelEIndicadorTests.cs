using Elo.Domain.Chamadas;
using Elo.Domain.Painel;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;
using static Elo.Tests.Fabrica;

namespace Elo.Tests.Unitarios.Dominio;

public class PriorizadorDeRuasTests
{
    [Fact]
    public void Painel_mostra_primeiro_quem_pediu_ajuda_e_depois_quem_esta_em_silencio()
    {
        var chamada = NovaChamadaReal();
        var aguape = NovaRua(1, "Rua do Aguapé", NivelRisco.Alto);
        var garcas = NovaRua(2, "Rua das Garças", NivelRisco.Alto);
        var palafitas = NovaRua(3, "Rua das Palafitas", NivelRisco.Alto);
        var barqueiros = NovaRua(4, "Rua dos Barqueiros", NivelRisco.Alto);
        var corrego = NovaRua(5, "Rua do Córrego Seco", NivelRisco.Baixo);
        var varzea = NovaRua(6, "Rua da Várzea", NivelRisco.Medio);
        var ipe = NovaRua(7, "Rua Ipê Amarelo", NivelRisco.Baixo);
        var taboa = NovaRua(8, "Travessa da Taboa", NivelRisco.Medio);

        List<Resposta> respostas =
        [
            Responder(chamada, taboa, SituacaoRua.TudoBem, 4),
            Responder(chamada, aguape, SituacaoRua.AguaSubindo, 5),
            Responder(chamada, ipe, SituacaoRua.TudoBem, 6),
            Responder(chamada, garcas, SituacaoRua.PrecisoDeAjuda, 8, pessoas: 3),
            Responder(chamada, varzea, SituacaoRua.AguaSubindo, 12),
            Responder(chamada, aguape, SituacaoRua.PrecisoDeAjuda, 18, pessoas: 5)
        ];
        var vouAteLa = chamada.RegistrarAcao(garcas, TipoAcao.VouAteLa, Inicio.AddMinutes(10));
        var sensorAcimaDoLimite = new LeituraRecente("ESP32-01", 46, 40, true, Inicio.AddMinutes(20));

        Rua[] ruasForaDeOrdem = [taboa, corrego, ipe, palafitas, varzea, barqueiros, garcas, aguape];
        var situacoes = ruasForaDeOrdem.Select(rua => SituacaoDaRua.Montar(
            rua,
            respostas.Where(resposta => resposta.RuaId == rua.Id),
            rua == garcas ? [vouAteLa] : [],
            rua == palafitas ? sensorAcimaDoLimite : null));

        var ordem = PriorizadorDeRuas.Ordenar(situacoes).Select(situacao => situacao.Rua.Nome);

        Assert.Equal(
        [
            "Rua do Aguapé",       // pediu ajuda, ninguém a caminho
            "Rua das Garças",      // pediu ajuda, mas a coordenação já está indo
            "Rua das Palafitas",   // silêncio + sensor acima do limite
            "Rua dos Barqueiros",  // silêncio, risco alto
            "Rua do Córrego Seco", // silêncio, risco baixo
            "Rua da Várzea",       // água subindo
            "Rua Ipê Amarelo",     // tudo bem (ordem alfabética)
            "Travessa da Taboa"
        ], ordem);
    }

    [Fact]
    public void A_resposta_mais_recente_e_a_que_vale()
    {
        var chamada = NovaChamadaReal();
        var rua = NovaRua(1);
        var respostas = new[]
        {
            Responder(chamada, rua, SituacaoRua.PrecisoDeAjuda, 5, pessoas: 2),
            Responder(chamada, rua, SituacaoRua.TudoBem, 20)
        };

        var situacao = SituacaoDaRua.Montar(rua, respostas, [], null);

        Assert.Equal(StatusNoPainel.TudoBem, situacao.Status);
    }

    [Fact]
    public void Vou_ate_la_anterior_a_ultima_resposta_nao_conta_como_atendimento_a_caminho()
    {
        var chamada = NovaChamadaReal();
        var rua = NovaRua(1);
        var vouAteLa = chamada.RegistrarAcao(rua, TipoAcao.VouAteLa, Inicio.AddMinutes(5));
        var respostaDepois = Responder(chamada, rua, SituacaoRua.PrecisoDeAjuda, 12, pessoas: 4);

        var situacao = SituacaoDaRua.Montar(rua, [respostaDepois], [vouAteLa], null);

        Assert.False(situacao.AtendimentoACaminho);
    }

    [Fact]
    public void Rua_sem_resposta_fica_em_silencio()
    {
        var situacao = SituacaoDaRua.Montar(NovaRua(1), [], [], null);

        Assert.Equal(StatusNoPainel.EmSilencio, situacao.Status);
        Assert.False(situacao.SensorAcimaDoLimite);
    }
}

public class IndicadorDaChamadaTests
{
    [Fact]
    public void Calcula_o_percentual_de_ruas_conhecidas_em_15_minutos()
    {
        var chamada = NovaChamadaReal();
        var respostas = new[]
        {
            Responder(chamada, NovaRua(1), SituacaoRua.TudoBem, 3),
            Responder(chamada, NovaRua(2), SituacaoRua.PrecisoDeAjuda, 14, pessoas: 2),
            Responder(chamada, NovaRua(3), SituacaoRua.TudoBem, 20)
        };

        var indicador = IndicadorDaChamada.Calcular(Inicio, [1, 2, 3, 4], respostas);

        Assert.Equal(4, indicador.TotalDeRuas);
        Assert.Equal(3, indicador.RuasComResposta);
        Assert.Equal(2, indicador.RuasConhecidasNaJanela);
        Assert.Equal(50, indicador.PercentualConhecidoNaJanela);
        Assert.Equal(75, indicador.PercentualComResposta);
        Assert.Equal(1, indicador.RuasQuePediramAjuda);
        Assert.Equal(12.3, indicador.MinutosAtePrimeiraRespostaEmMedia);
    }

    [Fact]
    public void Resposta_guardada_offline_so_conta_quando_chega()
    {
        var chamada = NovaChamadaReal();
        // Respondida aos 5 minutos, mas só chegou aos 30: o coordenador não sabia dela em 15 minutos.
        var resposta = Responder(chamada, NovaRua(1), SituacaoRua.TudoBem, 5, chegouAposMinutos: 30);

        var indicador = IndicadorDaChamada.Calcular(Inicio, [1], [resposta]);

        Assert.Equal(0, indicador.RuasConhecidasNaJanela);
        Assert.Equal(1, indicador.RuasComResposta);
    }

    [Fact]
    public void Sem_ruas_o_percentual_e_zero_e_respostas_de_fora_sao_ignoradas()
    {
        var chamada = NovaChamadaReal();
        var respostaDeRuaDesativada = Responder(chamada, NovaRua(9), SituacaoRua.TudoBem, 2);

        var indicador = IndicadorDaChamada.Calcular(Inicio, [], [respostaDeRuaDesativada]);

        Assert.Equal(0, indicador.PercentualConhecidoNaJanela);
        Assert.Equal(0, indicador.RuasComResposta);
        Assert.Null(indicador.MinutosAtePrimeiraRespostaEmMedia);
    }
}

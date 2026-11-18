using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Risco;
using Elo.Seguranca.Taint;
using Microsoft.Extensions.Time.Testing;

namespace Elo.Tests.Unitarios.Seguranca;

public class CalculadoraDeRiscoCidadaoTests
{
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly RegistroDeEventosEmMemoria _registro;
    private readonly CalculadoraDeRiscoCidadao _calculadora;

    public CalculadoraDeRiscoCidadaoTests()
    {
        _registro = new RegistroDeEventosEmMemoria(_relogio);
        _calculadora = new CalculadoraDeRiscoCidadao(_registro, _relogio);
    }

    private EventoDeSeguranca Entrada(params TipoDePii[] tipos) =>
        _registro.Registrar(EtapaDoEvento.Entrada, null, AcaoDeSeguranca.Marcado, tipos, tipos.Length, "POST /chamadas");

    private EventoDeSeguranca Saida(Sink sink, params TipoDePii[] tipos) =>
        _registro.Registrar(EtapaDoEvento.Saida, sink, AcaoDeSeguranca.Bloqueado, tipos, tipos.Length, "Categoria");

    [Fact]
    public void Sem_eventos_o_farol_e_verde_o_indice_zero_e_o_resumo_e_o_padrao()
    {
        var risco = _calculadora.Calcular();

        Assert.Equal(Farol.Verde, risco.Farol);
        Assert.Equal(0, risco.Indice);
        Assert.Equal(CalculadoraDeRiscoCidadao.ResumoVerde, risco.Resumo);
        Assert.Equal(24, risco.JanelaEmHoras);
        Assert.Equal(new TotaisPorSaida(0, 0, 0, 0, 0), risco.Totais);
        Assert.Empty(risco.PorTipo);
        Assert.Empty(risco.UltimosEventos);
    }

    [Fact]
    public void So_eventos_de_entrada_deixam_o_farol_amarelo()
    {
        Entrada(TipoDePii.Cpf, TipoDePii.Senha);

        var risco = _calculadora.Calcular();

        Assert.Equal(Farol.Amarelo, risco.Farol);
        Assert.Equal(CalculadoraDeRiscoCidadao.ResumoAmarelo, risco.Resumo);
        Assert.Equal(new TotaisPorSaida(1, 0, 0, 0, 0), risco.Totais);
    }

    [Theory]
    [InlineData(Sink.Banco)]
    [InlineData(Sink.Log)]
    [InlineData(Sink.RespostaHttp)]
    [InlineData(Sink.Llm)]
    public void Qualquer_saida_bloqueada_deixa_o_farol_vermelho(Sink sink)
    {
        Entrada(TipoDePii.Cpf);
        Saida(sink, TipoDePii.Cpf);

        var risco = _calculadora.Calcular();

        Assert.Equal(Farol.Vermelho, risco.Farol);
        Assert.Equal(CalculadoraDeRiscoCidadao.ResumoVermelho, risco.Resumo);
        Assert.Equal(1, risco.Totais.Entrada);
        Assert.Equal(1, sink switch { Sink.Banco => risco.Totais.Banco, Sink.Log => risco.Totais.Log, Sink.RespostaHttp => risco.Totais.RespostaHttp, _ => risco.Totais.Llm });
    }

    [Theory]
    [InlineData(EtapaDoEvento.Entrada, TipoDePii.Telefone, 2)]  // média (2) x entrada (1) = 2 pontos
    [InlineData(EtapaDoEvento.Entrada, TipoDePii.Cpf, 3)]       // alta (3) x entrada (1) = 3
    [InlineData(EtapaDoEvento.Saida, TipoDePii.Email, 6)]       // média (2) x saída (3) = 6
    [InlineData(EtapaDoEvento.Saida, TipoDePii.DadoDeSaude, 9)] // alta (3) x saída (3) = 9
    public void Pontos_sao_o_peso_da_severidade_vezes_o_peso_da_etapa(EtapaDoEvento etapa, TipoDePii tipo, int pontosEsperados)
    {
        if (etapa == EtapaDoEvento.Entrada)
        {
            Entrada(tipo);
        }
        else
        {
            Saida(Sink.Log, tipo);
        }

        var risco = _calculadora.Calcular();

        Assert.Equal(pontosEsperados, risco.Pontos);
        Assert.Equal(pontosEsperados * 4, risco.Indice);
    }

    [Fact]
    public void Indice_soma_os_eventos_e_trava_em_100()
    {
        Entrada(TipoDePii.Cpf);          // 3
        Saida(Sink.Log, TipoDePii.Cpf);  // 9
        Saida(Sink.Banco, TipoDePii.Rg); // 9  -> 21 pontos = índice 84
        Assert.Equal(84, _calculadora.Calcular().Indice);

        Saida(Sink.RespostaHttp, TipoDePii.Senha); // +9 = 30 pontos = 120, mas o índice para em 100
        var risco = _calculadora.Calcular();

        Assert.Equal(30, risco.Pontos);
        Assert.Equal(100, risco.Indice);
    }

    [Fact]
    public void Evento_fora_da_janela_de_24_horas_nao_conta()
    {
        Saida(Sink.Log, TipoDePii.Cpf);
        _relogio.Advance(TimeSpan.FromHours(25));

        var risco = _calculadora.Calcular();

        Assert.Equal(Farol.Verde, risco.Farol);
        Assert.Equal(0, risco.Indice);
        Assert.Empty(risco.UltimosEventos);
    }

    [Fact]
    public void Evento_de_ate_24_horas_atras_ainda_conta_e_a_janela_pode_ser_outra()
    {
        Saida(Sink.Log, TipoDePii.Cpf);
        _relogio.Advance(TimeSpan.FromHours(23));
        Assert.Equal(Farol.Vermelho, _calculadora.Calcular().Farol);

        _relogio.Advance(TimeSpan.FromHours(2)); // 25 h
        Assert.Equal(Farol.Vermelho, _calculadora.Calcular(janelaEmHoras: 48).Farol);
        Assert.Equal(Farol.Verde, _calculadora.Calcular().Farol);
    }

    [Fact]
    public void Resumo_usa_a_mensagem_traduzida_do_evento_de_saida_mais_grave()
    {
        var telefone = Saida(Sink.Log, TipoDePii.Telefone);  // média
        var cpf = Saida(Sink.Banco, TipoDePii.Cpf);          // alta
        var entrada = Entrada(TipoDePii.Cpf);
        _registro.DefinirMensagem(telefone.Id, "mensagem do telefone", "modelo padrão");
        _registro.DefinirMensagem(cpf.Id, "mensagem do cpf", "modelo padrão");
        _registro.DefinirMensagem(entrada.Id, "mensagem da entrada", "modelo padrão");

        Assert.Equal("mensagem do cpf", _calculadora.Calcular().Resumo);
    }

    [Fact]
    public void Entre_eventos_igualmente_graves_vale_o_mais_recente()
    {
        var velho = Saida(Sink.Log, TipoDePii.Cpf);
        _relogio.Advance(TimeSpan.FromMinutes(1));
        var novo = Saida(Sink.Banco, TipoDePii.Rg);
        _registro.DefinirMensagem(velho.Id, "velha", "modelo padrão");
        _registro.DefinirMensagem(novo.Id, "nova", "modelo padrão");

        Assert.Equal("nova", _calculadora.Calcular().Resumo);
    }

    [Fact]
    public void Enquanto_a_traducao_nao_chega_o_resumo_e_a_frase_padrao_do_farol()
    {
        Saida(Sink.Log, TipoDePii.Cpf);

        Assert.Equal(CalculadoraDeRiscoCidadao.ResumoVermelho, _calculadora.Calcular().Resumo);
    }

    [Fact]
    public void Totais_por_saida_e_por_tipo_contam_eventos()
    {
        Entrada(TipoDePii.Cpf, TipoDePii.Senha);
        Saida(Sink.Log, TipoDePii.Cpf);
        Saida(Sink.Log, TipoDePii.DadoDeSaude);
        Saida(Sink.RespostaHttp, TipoDePii.Cpf);

        var risco = _calculadora.Calcular();

        Assert.Equal(new TotaisPorSaida(Entrada: 1, Banco: 0, Log: 2, RespostaHttp: 1, Llm: 0), risco.Totais);
        Assert.Equal(3, risco.PorTipo[TipoDePii.Cpf]);
        Assert.Equal(1, risco.PorTipo[TipoDePii.Senha]);
        Assert.Equal(1, risco.PorTipo[TipoDePii.DadoDeSaude]);
        Assert.False(risco.PorTipo.ContainsKey(TipoDePii.Rg));
    }

    [Fact]
    public void Ultimos_eventos_sao_no_maximo_20_do_mais_recente_para_o_mais_antigo()
    {
        for (var i = 0; i < 25; i++)
        {
            Saida(Sink.Log, TipoDePii.Cpf);
        }

        var risco = _calculadora.Calcular();

        Assert.Equal(20, risco.UltimosEventos.Count);
        Assert.Equal(25L, risco.UltimosEventos[0].Id);
        Assert.Equal(6L, risco.UltimosEventos[^1].Id);
        Assert.Equal(25, risco.Totais.Log); // os totais olham todos os eventos da janela, não só os 20
    }
}

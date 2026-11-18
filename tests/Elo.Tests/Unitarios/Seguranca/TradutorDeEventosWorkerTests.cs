using System.Net;
using Elo.Infrastructure.Seguranca;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Elo.Seguranca.Traducao;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elo.Tests.Unitarios.Seguranca;

public class TradutorDeEventosWorkerTests
{
    private static TradutorDeEventosWorker CriarWorker(CenarioDeTradutor cenario) =>
        new(cenario.Registro, cenario.Tradutor, new TradutorPadrao(), cenario.Relogio, NullLogger<TradutorDeEventosWorker>.Instance);

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (!condicao())
        {
            Assert.True(DateTime.UtcNow < limite, "A condição não foi atendida em 5 segundos.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Sem_gemini_cada_evento_novo_ganha_a_mensagem_pronta()
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo("não devia ser chamado"), new IaOptions { Habilitada = false });
        var worker = CriarWorker(cenario);
        await worker.StartAsync(CancellationToken.None);

        var evento = cenario.NovoEvento(sink: Sink.Banco, tipos: [TipoDePii.Rg]);
        await EsperarAsync(() => evento.MensagemAoCidadao is not null);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(TradutorPadrao.Traduzir(EtapaDoEvento.Saida, Sink.Banco, [TipoDePii.Rg]), evento.MensagemAoCidadao);
        Assert.Equal("modelo padrão", evento.TraduzidaPor);
        Assert.Empty(cenario.Gemini.Requisicoes);
    }

    [Fact]
    public async Task Eventos_com_a_mesma_assinatura_chamam_o_gemini_uma_vez_so()
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo("Mensagem do Gemini. Está protegido."));
        var worker = CriarWorker(cenario);
        await worker.StartAsync(CancellationToken.None);

        var a = cenario.NovoEvento(sink: Sink.Log, tipos: [TipoDePii.Cpf, TipoDePii.Senha]);
        var b = cenario.NovoEvento(sink: Sink.Log, tipos: [TipoDePii.Senha, TipoDePii.Cpf]); // mesma assinatura, outra ordem
        var c = cenario.NovoEvento(sink: Sink.Banco, tipos: [TipoDePii.Cpf, TipoDePii.Senha]); // outra saída
        await EsperarAsync(() => a.MensagemAoCidadao is not null && b.MensagemAoCidadao is not null && c.MensagemAoCidadao is not null);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, cenario.Gemini.Requisicoes.Count);
        Assert.All(new[] { a, b, c }, e =>
        {
            Assert.Equal("Mensagem do Gemini. Está protegido.", e.MensagemAoCidadao);
            Assert.Equal("gemini", e.TraduzidaPor);
        });
    }

    [Fact]
    public async Task Falha_do_gemini_usa_a_mensagem_pronta_e_o_gemini_descansa_por_um_minuto()
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Status(HttpStatusCode.TooManyRequests));
        var worker = CriarWorker(cenario);
        await worker.StartAsync(CancellationToken.None);

        var primeiro = cenario.NovoEvento(sink: Sink.Log, tipos: [TipoDePii.Cpf]);
        await EsperarAsync(() => primeiro.MensagemAoCidadao is not null);
        var segundo = cenario.NovoEvento(sink: Sink.Banco, tipos: [TipoDePii.Cpf]);
        await EsperarAsync(() => segundo.MensagemAoCidadao is not null);

        Assert.Equal("modelo padrão", primeiro.TraduzidaPor);
        Assert.Equal("modelo padrão", segundo.TraduzidaPor);
        Assert.Single(cenario.Gemini.Requisicoes); // o segundo evento nem tentou o Gemini

        cenario.Relogio.Advance(TimeSpan.FromMinutes(2));
        var terceiro = cenario.NovoEvento(sink: Sink.RespostaHttp, tipos: [TipoDePii.Cpf]);
        await EsperarAsync(() => terceiro.MensagemAoCidadao is not null);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, cenario.Gemini.Requisicoes.Count); // passado o descanso, tenta de novo
    }

    [Fact]
    public void Assinatura_nao_depende_da_ordem_dos_tipos_nem_do_lugar()
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo("x"));
        var a = cenario.NovoEvento(tipos: [TipoDePii.Cpf, TipoDePii.Rg]);
        var b = cenario.NovoEvento(tipos: [TipoDePii.Rg, TipoDePii.Cpf]);
        var entrada = cenario.NovoEvento(EtapaDoEvento.Entrada, tipos: [TipoDePii.Cpf, TipoDePii.Rg]);

        Assert.Equal(TradutorDeEventosWorker.Assinatura(a), TradutorDeEventosWorker.Assinatura(b));
        Assert.NotEqual(TradutorDeEventosWorker.Assinatura(a), TradutorDeEventosWorker.Assinatura(entrada));
    }
}

using System.Net;
using System.Text.Json.Nodes;
using Elo.Infrastructure.Seguranca;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Elo.Seguranca.Traducao;

namespace Elo.Tests.Unitarios.Seguranca;

/// <summary>O TradutorGemini com um HttpMessageHandler falso: a requisição certa, a leitura da resposta e a queda para a mensagem pronta.</summary>
public class TradutorGeminiTests
{
    private const string TextoDoGemini = "Uma informação sua ia aparecer nos registros. O Elo a escondeu e ela está protegida.";

    [Fact]
    public async Task Monta_a_requisicao_certa_e_le_a_resposta()
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo(TextoDoGemini));
        var evento = cenario.NovoEvento();

        var mensagem = await cenario.Tradutor.TraduzirAsync(evento, CancellationToken.None);

        Assert.Equal(TextoDoGemini, mensagem);
        Assert.Equal("gemini", cenario.Tradutor.Nome);

        var requisicao = Assert.Single(cenario.Gemini.Requisicoes);
        Assert.Equal(HttpMethod.Post, requisicao.Metodo);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent",
            requisicao.Endereco.ToString());
        Assert.Equal(CenarioDeTradutor.Chave, requisicao.Cabecalhos["x-goog-api-key"]);
        Assert.DoesNotContain(CenarioDeTradutor.Chave, requisicao.Endereco.ToString()); // a chave vai no header, nunca na URL

        var corpo = JsonNode.Parse(requisicao.Corpo)!;
        var prompt = corpo["contents"]![0]!["parts"]![0]!["text"]!.GetValue<string>();
        Assert.Contains(evento.DescricaoTecnica, prompt);
        Assert.Contains("cidadão leigo de 60 anos", prompt);
        Assert.Contains("no máximo 2 frases", prompt);
        Assert.Equal(0.3, corpo["generationConfig"]!["temperature"]!.GetValue<double>());
        Assert.Equal(120, corpo["generationConfig"]!["maxOutputTokens"]!.GetValue<int>());
    }

    [Fact]
    public async Task Usa_o_modelo_configurado_na_url()
    {
        var ia = new IaOptions { Gemini = { ChaveApi = CenarioDeTradutor.Chave, Modelo = "gemini-2.0-flash" } };
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo(TextoDoGemini), ia);

        await cenario.Tradutor.TraduzirAsync(cenario.NovoEvento(), CancellationToken.None);

        Assert.EndsWith("/models/gemini-2.0-flash:generateContent", Assert.Single(cenario.Gemini.Requisicoes).Endereco.ToString());
    }

    [Fact]
    public async Task Limpa_aspas_e_quebras_de_linha_da_resposta()
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo("\"Primeira frase.\nSegunda frase.\"\n"));

        var mensagem = await cenario.Tradutor.TraduzirAsync(cenario.NovoEvento(), CancellationToken.None);

        Assert.Equal("Primeira frase. Segunda frase.", mensagem);
    }

    [Fact]
    public async Task Sem_chave_nao_chama_a_rede_e_usa_a_mensagem_pronta()
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo(TextoDoGemini), new IaOptions { Habilitada = true });
        var evento = cenario.NovoEvento();

        var mensagem = await cenario.Tradutor.TraduzirAsync(evento, CancellationToken.None);

        Assert.False(cenario.Tradutor.Configurado);
        Assert.Empty(cenario.Gemini.Requisicoes);
        Assert.Equal(TradutorPadrao.Traduzir(evento.Etapa, evento.Sink, evento.Tipos), mensagem);
        Assert.Null(await cenario.Tradutor.TentarTraduzirAsync(evento, CancellationToken.None));
    }

    [Fact]
    public async Task Desabilitado_nao_chama_a_rede_mesmo_com_chave()
    {
        var ia = new IaOptions { Habilitada = false, Gemini = { ChaveApi = CenarioDeTradutor.Chave } };
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo(TextoDoGemini), ia);

        await cenario.Tradutor.TraduzirAsync(cenario.NovoEvento(), CancellationToken.None);

        Assert.False(cenario.Tradutor.Configurado);
        Assert.Empty(cenario.Gemini.Requisicoes);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Erro_http_cai_na_mensagem_pronta_sem_vazar_a_chave_no_log(HttpStatusCode codigo)
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Status(codigo, """{"error":{"message":"chave inválida"}}"""));
        var evento = cenario.NovoEvento();

        var mensagem = await cenario.Tradutor.TraduzirAsync(evento, CancellationToken.None);

        Assert.Equal(TradutorPadrao.Traduzir(evento.Etapa, evento.Sink, evento.Tipos), mensagem);
        Assert.Single(cenario.Gemini.Requisicoes);
        Assert.Contains(cenario.Log.Linhas, l => l.Contains($"HTTP {(int)codigo}"));
        Assert.DoesNotContain(cenario.Log.Linhas, l => l.Contains(CenarioDeTradutor.Chave) || l.Contains("chave inválida"));
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("{}")]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[]}}]}""")]
    [InlineData("""{"candidates":[{"finishReason":"MAX_TOKENS","content":{"role":"model"}}]}""")]
    [InlineData("""{"candidates":"texto"}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"text":"   "}]}}]}""")]
    public async Task Resposta_sem_texto_aproveitavel_cai_na_mensagem_pronta(string corpo)
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Status(HttpStatusCode.OK, corpo));
        var evento = cenario.NovoEvento();

        var mensagem = await cenario.Tradutor.TraduzirAsync(evento, CancellationToken.None);

        Assert.Equal(TradutorPadrao.Traduzir(evento.Etapa, evento.Sink, evento.Tipos), mensagem);
        Assert.Null(await cenario.Tradutor.TentarTraduzirAsync(evento, CancellationToken.None));
    }

    [Fact]
    public async Task Falha_de_rede_cai_na_mensagem_pronta()
    {
        var gemini = new GeminiFalso((_, _) => throw new HttpRequestException("sem rede, chave=" + CenarioDeTradutor.Chave));
        var cenario = new CenarioDeTradutor(gemini);
        var evento = cenario.NovoEvento();

        var mensagem = await cenario.Tradutor.TraduzirAsync(evento, CancellationToken.None);

        Assert.Equal(TradutorPadrao.Traduzir(evento.Etapa, evento.Sink, evento.Tipos), mensagem);
        Assert.DoesNotContain(cenario.Log.Linhas, l => l.Contains(CenarioDeTradutor.Chave)); // só o tipo do erro vai para o log
    }

    [Fact]
    public async Task Timeout_cai_na_mensagem_pronta()
    {
        var chamado = new TaskCompletionSource();
        var gemini = new GeminiFalso(async (_, ct) =>
        {
            chamado.SetResult();
            await Task.Delay(Timeout.Infinite, ct); // o Gemini "travou"
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var cenario = new CenarioDeTradutor(gemini);
        var evento = cenario.NovoEvento();

        var pendente = cenario.Tradutor.TraduzirAsync(evento, CancellationToken.None);
        await chamado.Task;
        cenario.Relogio.Advance(TimeSpan.FromSeconds(cenario.Opcoes.TempoLimiteEmSegundos + 1));
        var mensagem = await pendente.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(TradutorPadrao.Traduzir(evento.Etapa, evento.Sink, evento.Tipos), mensagem);
        Assert.Contains(cenario.Log.Linhas, l => l.Contains($"passou de {cenario.Opcoes.TempoLimiteEmSegundos}s"));
    }

    [Fact]
    public async Task Cancelamento_de_quem_chamou_nao_vira_mensagem_pronta()
    {
        var chamado = new TaskCompletionSource();
        var gemini = new GeminiFalso(async (_, ct) =>
        {
            chamado.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var cenario = new CenarioDeTradutor(gemini);
        using var cancelamento = new CancellationTokenSource();

        var pendente = cenario.Tradutor.TentarTraduzirAsync(cenario.NovoEvento(), cancelamento.Token);
        await chamado.Task;
        await cancelamento.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendente);
    }

    [Fact]
    public async Task O_prompt_nao_contem_o_dado_pessoal()
    {
        // O valor entra marcado (como numa requisição), sai barrado no log e vira evento; o prompt só conhece o evento.
        var tinta = new ContextoDeContaminacao();
        tinta.Marcar("segredo-super-secreto-9", TipoDePii.Senha, "corpo $.chave");
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo(TextoDoGemini), contexto: tinta);

        cenario.Guarda.Verificar(
            "Pedido de ajuda: CPF 123.456.789-00, segredo-super-secreto-9, a vizinha é diabética",
            Sink.Log, ModoDeAnalise.TextoLivre, "Elo.Application.Chamadas.RespostaService");
        var evento = Assert.Single(cenario.Registro.Listar(null, 10));

        await cenario.Tradutor.TraduzirAsync(evento, CancellationToken.None);

        var enviado = Assert.Single(cenario.Gemini.Requisicoes).Corpo;
        foreach (var valor in new[] { "123.456.789-00", "segredo-super-secreto-9", "diabética", "456.789" })
        {
            Assert.DoesNotContain(valor, enviado);
        }

        Assert.Contains("Senha", evento.DescricaoTecnica); // a descrição fala do tipo, não do valor
        Assert.Single(cenario.Registro.Listar(null, 10)); // e o prompt limpo não gerou evento novo
    }

    [Fact]
    public async Task Prompt_que_por_acaso_traga_dado_pessoal_e_limpo_pela_guarda_com_sink_llm()
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo(TextoDoGemini));
        var evento = cenario.NovoEvento(descricao: "Falha estranha com 123.456.789-00 dentro");

        await cenario.Tradutor.TraduzirAsync(evento, CancellationToken.None);

        var enviado = Assert.Single(cenario.Gemini.Requisicoes).Corpo;
        Assert.DoesNotContain("123.456.789-00", enviado);
        Assert.Contains("***.456.***-00", enviado);

        var bloqueio = Assert.Single(cenario.Registro.Listar(null, 10), e => e.Sink == Sink.Llm);
        Assert.Equal(AcaoDeSeguranca.Bloqueado, bloqueio.Acao);
        Assert.Equal(nameof(TradutorGemini), bloqueio.Onde);
    }

    [Theory]
    [InlineData(EtapaDoEvento.Entrada, null)]
    [InlineData(EtapaDoEvento.Saida, Sink.Banco)]
    [InlineData(EtapaDoEvento.Saida, Sink.Log)]
    [InlineData(EtapaDoEvento.Saida, Sink.RespostaHttp)]
    [InlineData(EtapaDoEvento.Saida, Sink.Llm)]
    public void O_prompt_de_qualquer_evento_normal_passa_limpo_pela_guarda(EtapaDoEvento etapa, Sink? sink)
    {
        var cenario = new CenarioDeTradutor(GeminiFalso.Respondendo(TextoDoGemini));

        foreach (var tipo in Enum.GetValues<TipoDePii>())
        {
            var evento = cenario.NovoEvento(etapa, sink, [tipo]);
            var prompt = MontadorDePrompt.Montar(evento);

            var analise = cenario.Guarda.Verificar(prompt, Sink.Llm, ModoDeAnalise.TextoLivre, "teste");

            Assert.False(analise.TemPii, $"O prompt de {tipo}/{sink} foi tratado como PII.");
            Assert.Contains(evento.DescricaoTecnica, prompt);
        }
    }
}

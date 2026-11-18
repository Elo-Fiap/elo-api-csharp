using System.Net;
using System.Text;
using System.Text.Json;
using Elo.Infrastructure.Seguranca;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Microsoft.Extensions.Options;

namespace Elo.Tests.Unitarios.Seguranca;

/// <summary>
/// O TradutorClaude com o SDK da Anthropic de verdade, mas com um HttpMessageHandler falso no lugar da rede:
/// confere a requisição que sairia para a API e como a resposta é lida.
/// </summary>
public class TradutorClaudeTests
{
    private const string Chave = "chave-falsa-so-para-teste-123";

    private static HttpResponseMessage RespostaDoClaude(string texto, string motivoDeParada = "end_turn") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    id = "msg_teste",
                    type = "message",
                    role = "assistant",
                    model = ClaudeOptions.ModeloPadrao,
                    content = new object[]
                    {
                        new { type = "thinking", thinking = "", signature = "assinatura" },
                        new { type = "text", text = texto }
                    },
                    stop_reason = motivoDeParada,
                    stop_sequence = (string?)null,
                    usage = new { input_tokens = 50, output_tokens = 30 }
                }),
                Encoding.UTF8,
                "application/json")
        };

    private static (TradutorClaude Tradutor, GeminiFalso Rede, CenarioDeTradutor Cenario) Montar(
        GeminiFalso rede, IaOptions? ia = null, ContextoDeContaminacao? contexto = null)
    {
        // O CenarioDeTradutor dá o relógio, o registro e a guarda; a "rede" é o handler falso.
        var cenario = new CenarioDeTradutor(rede, contexto: contexto);
        var opcoes = ia ?? new IaOptions { Habilitada = true, Claude = { ChaveApi = Chave } };
        var tradutor = new TradutorClaude(new FabricaDeHttpFalsa(rede), Options.Create(opcoes), cenario.Guarda, new LoggerDeCaptura<TradutorClaude>());
        return (tradutor, rede, cenario);
    }

    [Fact]
    public async Task Sem_chave_nao_acessa_a_rede()
    {
        var (tradutor, rede, cenario) = Montar(GeminiFalso.Respondendo("não devia ser chamado"), new IaOptions { Habilitada = true });

        Assert.False(tradutor.Configurado);
        Assert.Null(await tradutor.TentarTraduzirAsync(cenario.NovoEvento(), CancellationToken.None));
        Assert.Empty(rede.Requisicoes);
    }

    [Fact]
    public async Task Monta_a_requisicao_com_chave_modelo_fallback_e_esforco_baixo()
    {
        var (tradutor, rede, cenario) = Montar(new GeminiFalso((_, _) => Task.FromResult(RespostaDoClaude("Frase um. Frase dois."))));

        var texto = await tradutor.TentarTraduzirAsync(cenario.NovoEvento(), CancellationToken.None);

        Assert.Equal("Frase um. Frase dois.", texto);
        var requisicao = Assert.Single(rede.Requisicoes);
        Assert.Equal(HttpMethod.Post, requisicao.Metodo);
        Assert.Equal("https://api.anthropic.com/v1/messages", requisicao.Endereco.GetLeftPart(UriPartial.Path));
        // Nesta versão do SDK a chave vai como "Authorization: Bearer"; informada no código, nada é lido do ambiente.
        Assert.Equal($"Bearer {Chave}", requisicao.Cabecalhos[TradutorClaude.NomeDoHeaderDaChave]);
        Assert.Contains("server-side-fallback-2026-07-01", requisicao.Cabecalhos["anthropic-beta"]);

        using var corpo = JsonDocument.Parse(requisicao.Corpo);
        Assert.Equal(ClaudeOptions.ModeloPadrao, corpo.RootElement.GetProperty("model").GetString());
        Assert.Equal("default", corpo.RootElement.GetProperty("fallbacks").GetString());
        Assert.Equal("low", corpo.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task O_prompt_nao_leva_o_dado_contaminado()
    {
        var contexto = new ContextoDeContaminacao();
        contexto.Marcar("123.456.789-09", TipoDePii.Cpf, "corpo $.observacao");
        var (tradutor, rede, cenario) = Montar(new GeminiFalso((_, _) => Task.FromResult(RespostaDoClaude("Ok."))), contexto: contexto);

        // Mesmo que a descrição técnica trouxesse o valor, a guarda do sink Llm tira antes de sair.
        await tradutor.TentarTraduzirAsync(cenario.NovoEvento(descricao: "Falha com 123.456.789-09"), CancellationToken.None);

        var requisicao = Assert.Single(rede.Requisicoes);
        Assert.DoesNotContain("123.456.789-09", requisicao.Corpo);
        Assert.DoesNotContain(Chave, requisicao.Corpo);
    }

    [Fact]
    public async Task Recusa_do_modelo_vira_null()
    {
        var (tradutor, _, cenario) = Montar(new GeminiFalso((_, _) => Task.FromResult(RespostaDoClaude("", "refusal"))));

        Assert.Null(await tradutor.TentarTraduzirAsync(cenario.NovoEvento(), CancellationToken.None));
    }

    [Fact]
    public async Task Erro_da_api_vira_null_sem_derrubar()
    {
        // 400 não é repetido pelo SDK, então o teste não espera nenhum intervalo entre tentativas.
        var (tradutor, _, cenario) = Montar(GeminiFalso.Status(HttpStatusCode.BadRequest,
            """{"type":"error","error":{"type":"invalid_request_error","message":"x"}}"""));

        Assert.Null(await tradutor.TentarTraduzirAsync(cenario.NovoEvento(), CancellationToken.None));
    }
}

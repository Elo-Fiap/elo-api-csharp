using System.Net.Http.Json;
using System.Text.Json;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Elo.Seguranca.Traducao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elo.Infrastructure.Seguranca;

/// <summary>
/// Traduz a falha técnica com o Gemini (API REST, sem SDK). Só eventos de segurança vêm para cá, e só com metadados:
/// aviso de chuva nunca passa por LLM, e o dado pessoal nem existe no evento.
/// Sem chave, desligado, com erro ou com timeout, devolve a mensagem pronta do <see cref="TradutorPadrao"/>.
/// A chave vai no header <c>x-goog-api-key</c> (nunca na URL) e não aparece em log.
/// </summary>
public sealed class TradutorGemini(
    IHttpClientFactory fabrica,
    IOptions<IaOptions> opcoes,
    GuardaDeSaida guarda,
    TradutorPadrao padrao,
    TimeProvider relogio,
    ILogger<TradutorGemini> logger) : ITradutorDeRisco, IProvedorDeIa
{
    public const string NomeDoCliente = "gemini";
    public const string EnderecoBase = "https://generativelanguage.googleapis.com/v1beta/";
    public const string NomeDoHeaderDaChave = "x-goog-api-key";

    private const int MaximoDeCaracteres = 500;

    public string Nome => "gemini";

    /// <summary>Há chave e o LLM não está desligado. Sem isso nem tenta a rede.</summary>
    public bool Configurado => opcoes.Value.Habilitada && !string.IsNullOrWhiteSpace(opcoes.Value.Gemini.ChaveApi);

    /// <summary>A tradução do Gemini; se ele não puder, a mensagem pronta.</summary>
    public async Task<string> TraduzirAsync(EventoDeSeguranca evento, CancellationToken ct) =>
        await TentarTraduzirAsync(evento, ct) ?? await padrao.TraduzirAsync(evento, ct);

    /// <summary>
    /// Só o Gemini: devolve <c>null</c> quando ele não respondeu (sem chave, desligado, erro, timeout ou resposta vazia).
    /// O worker usa este para saber quem escreveu a mensagem e para não guardar em cache uma tradução que falhou.
    /// </summary>
    public async Task<string?> TentarTraduzirAsync(EventoDeSeguranca evento, CancellationToken ct)
    {
        if (!Configurado)
        {
            return null;
        }

        var configuracao = opcoes.Value;

        // Defesa em profundidade: o prompt só tem metadados, mas passa pela guarda como qualquer outra saída.
        var prompt = guarda.Verificar(MontadorDePrompt.Montar(evento), Sink.Llm, ModoDeAnalise.TextoLivre, nameof(TradutorGemini)).TextoMascarado;

        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(configuracao.TempoLimiteEmSegundos), relogio);
        using var ligado = CancellationTokenSource.CreateLinkedTokenSource(ct, limite.Token);

        try
        {
            var modelo = string.IsNullOrWhiteSpace(configuracao.Gemini.Modelo) ? GeminiOptions.ModeloPadrao : configuracao.Gemini.Modelo.Trim();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, $"models/{Uri.EscapeDataString(modelo)}:generateContent")
            {
                Content = JsonContent.Create(new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } },
                    // O 2.5 gasta parte dos tokens "pensando": sem desligar, 120 tokens podem acabar antes da resposta.
                    generationConfig = new { temperature = 0.3, maxOutputTokens = 120, thinkingConfig = new { thinkingBudget = 0 } }
                })
            };
            requisicao.Headers.Add(NomeDoHeaderDaChave, configuracao.Gemini.ChaveApi!.Trim());

            using var http = fabrica.CreateClient(NomeDoCliente);
            using var resposta = await http.SendAsync(requisicao, ligado.Token);

            if (!resposta.IsSuccessStatusCode)
            {
                // Só o código: o corpo do erro não vai para o log.
                logger.LogWarning("Gemini respondeu HTTP {Codigo}; usando a mensagem pronta.", (int)resposta.StatusCode);
                return null;
            }

            var texto = LerTexto(await resposta.Content.ReadAsStringAsync(ligado.Token));
            if (texto is null)
            {
                logger.LogWarning("Gemini respondeu sem texto; usando a mensagem pronta.");
            }

            return texto;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Gemini passou de {Segundos}s; usando a mensagem pronta.", configuracao.TempoLimiteEmSegundos);
            return null;
        }
        catch (Exception erro) when (erro is HttpRequestException or JsonException or InvalidOperationException)
        {
            // Só o tipo do erro: a mensagem de uma HttpRequestException pode trazer endereço e detalhes de rede.
            logger.LogWarning("Falha ao chamar o Gemini ({Erro}); usando a mensagem pronta.", erro.GetType().Name);
            return null;
        }
    }

    private static JsonElement? Propriedade(JsonElement? no, string nome) =>
        no is { ValueKind: JsonValueKind.Object } objeto && objeto.TryGetProperty(nome, out var valor) ? valor : null;

    private static JsonElement? PrimeiroItem(JsonElement? no) =>
        no is { ValueKind: JsonValueKind.Array } lista && lista.GetArrayLength() > 0 ? lista[0] : null;

    /// <summary><c>candidates[0].content.parts[0].text</c>, sem aspas nem quebras de linha, ou nulo se faltar algo.</summary>
    private static string? LerTexto(string json)
    {
        using var documento = JsonDocument.Parse(json);
        var texto = PrimeiroItem(Propriedade(documento.RootElement, "candidates")) is { } candidato
            && PrimeiroItem(Propriedade(Propriedade(candidato, "content"), "parts")) is { } parte
            && Propriedade(parte, "text") is { ValueKind: JsonValueKind.String } campo
                ? campo.GetString()
                : null;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        texto = string.Join(' ', texto.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim('"', ' ');
        return texto.Length > MaximoDeCaracteres ? texto[..MaximoDeCaracteres].TrimEnd() : texto;
    }
}

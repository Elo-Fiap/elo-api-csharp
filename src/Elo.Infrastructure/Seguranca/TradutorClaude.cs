using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Elo.Seguranca.Traducao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elo.Infrastructure.Seguranca;

/// <summary>
/// Traduz a falha técnica com o Claude, pelo SDK oficial da Anthropic (pacote <c>Anthropic</c>).
/// Só eventos de segurança vêm para cá, e só com metadados: aviso de chuva nunca passa por LLM,
/// e o dado pessoal nem existe no evento. Sem chave, com erro, recusa ou timeout, devolve <c>null</c>
/// e o worker usa a mensagem pronta do <see cref="TradutorPadrao"/>.
/// </summary>
public sealed class TradutorClaude(
    IHttpClientFactory fabrica,
    IOptions<IaOptions> opcoes,
    GuardaDeSaida guarda,
    ILogger<TradutorClaude> logger) : IProvedorDeIa
{
    public const string NomeDoCliente = "claude";
    // O SDK manda a chave como "Authorization: Bearer"; o registro do HttpClient esconde este header no log.
    public const string NomeDoHeaderDaChave = "Authorization";

    // Duas frases curtas; o resto é folga para o raciocínio do modelo, que não dá para desligar no Opus 5.5.
    private const int MaximoDeTokens = 1024;
    private const int MaximoDeCaracteres = 500;

    public string Nome => "claude";

    public bool Configurado => opcoes.Value.Habilitada && !string.IsNullOrWhiteSpace(opcoes.Value.Claude.ChaveApi);

    public async Task<string?> TentarTraduzirAsync(EventoDeSeguranca evento, CancellationToken ct)
    {
        if (!Configurado)
        {
            return null;
        }

        var configuracao = opcoes.Value;

        // Defesa em profundidade: o prompt só tem metadados, mas passa pela guarda como qualquer outra saída.
        var prompt = guarda.Verificar(MontadorDePrompt.Montar(evento), Sink.Llm, ModoDeAnalise.TextoLivre, nameof(TradutorClaude)).TextoMascarado;

        var cliente = new AnthropicClient
        {
            ApiKey = configuracao.Claude.ChaveApi!.Trim(),
            HttpClient = fabrica.CreateClient(NomeDoCliente),
            Timeout = TimeSpan.FromSeconds(configuracao.TempoLimiteEmSegundos),
            MaxRetries = 1
        };

        try
        {
            var resposta = await cliente.Beta.Messages.Create(new MessageCreateParams
            {
                Model = string.IsNullOrWhiteSpace(configuracao.Claude.Modelo) ? ClaudeOptions.ModeloPadrao : configuracao.Claude.Modelo.Trim(),
                MaxTokens = MaximoDeTokens,
                // Esforço baixo: a tarefa é curta, e menos raciocínio é resposta mais rápida e mais barata.
                OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
                // Se o modelo recusar a tradução, o servidor da Anthropic tenta outro modelo na mesma chamada.
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default(),
                Messages = [new BetaUserMessageParam { Content = prompt }]
            }, ct);

            if (resposta.StopReason == BetaStopReason.Refusal)
            {
                logger.LogWarning("Claude recusou a tradução; usando a mensagem pronta.");
                return null;
            }

            // A resposta pode trazer blocos de raciocínio antes do texto: só o texto vai para o cidadão.
            var texto = string.Join(' ', resposta.Content.Select(bloco => bloco.Value).OfType<BetaTextBlock>().Select(bloco => bloco.Text));
            var limpo = Limpar(texto);
            if (limpo is null)
            {
                logger.LogWarning("Claude respondeu sem texto; usando a mensagem pronta.");
            }

            return limpo;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Claude passou de {Segundos}s; usando a mensagem pronta.", configuracao.TempoLimiteEmSegundos);
            return null;
        }
        catch (Exception erro) when (erro is AnthropicException or HttpRequestException)
        {
            // Só o tipo do erro (ex.: AnthropicRateLimitException): a mensagem pode trazer detalhes da conta.
            logger.LogWarning("Falha ao chamar o Claude ({Erro}); usando a mensagem pronta.", erro.GetType().Name);
            return null;
        }
    }

    /// <summary>Uma linha só, sem aspas em volta, até 500 caracteres; nulo se vier vazio.</summary>
    private static string? Limpar(string texto)
    {
        texto = string.Join(' ', texto.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim('"', ' ');
        if (texto.Length == 0)
        {
            return null;
        }

        return texto.Length > MaximoDeCaracteres ? texto[..MaximoDeCaracteres].TrimEnd() : texto;
    }
}

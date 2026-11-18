using Elo.Seguranca.Eventos;
using Elo.Seguranca.Traducao;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elo.Infrastructure.Seguranca;

/// <summary>
/// Serviço em segundo plano que escreve a mensagem ao cidadão de cada evento de segurança novo.
/// Fica fora da requisição de propósito: quem chamou a API não espera o LLM.
/// Usa o LLM configurado (Claude ou Gemini, ver <see cref="IProvedorDeIa"/>) e cai na mensagem pronta se ele falhar.
/// O cache por assinatura (etapa + saída + tipos) evita chamar o LLM de novo para a mesma falha
/// (custo e limite por minuto), e depois de uma falha o LLM descansa por um minuto.
/// </summary>
public sealed class TradutorDeEventosWorker(
    IRegistroDeEventos registro,
    IProvedorDeIa llm,
    TradutorPadrao padrao,
    TimeProvider relogio,
    ILogger<TradutorDeEventosWorker> logger) : BackgroundService
{
    private static readonly TimeSpan DescansoDepoisDeFalha = TimeSpan.FromMinutes(1);

    // Só este laço lê e escreve aqui (um leitor só no canal), então não precisa de trava.
    private readonly Dictionary<string, string> _cacheDoLlm = [];
    private DateTimeOffset _semLlmAte = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var id in registro.NovosEventos.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await TraduzirAsync(id, stoppingToken);
                }
                catch (Exception erro) when (erro is not OperationCanceledException)
                {
                    // Um evento sem tradução não derruba o serviço: o painel usa a frase padrão do farol.
                    logger.LogError("Não foi possível traduzir o evento {EventoId} ({Erro}).", id, erro.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A API está desligando.
        }
    }

    private async Task TraduzirAsync(long id, CancellationToken ct)
    {
        var evento = registro.Listar(null, RegistroDeEventosEmMemoria.Capacidade).FirstOrDefault(e => e.Id == id);
        if (evento is null)
        {
            return; // já saiu do buffer
        }

        var assinatura = Assinatura(evento);
        if (_cacheDoLlm.TryGetValue(assinatura, out var guardada))
        {
            registro.DefinirMensagem(id, guardada, llm.Nome);
            return;
        }

        if (llm.Configurado && relogio.GetUtcNow() >= _semLlmAte)
        {
            var mensagem = await llm.TentarTraduzirAsync(evento, ct);
            if (mensagem is not null)
            {
                _cacheDoLlm[assinatura] = mensagem;
                registro.DefinirMensagem(id, mensagem, llm.Nome);
                return;
            }

            _semLlmAte = relogio.GetUtcNow() + DescansoDepoisDeFalha;
        }

        registro.DefinirMensagem(id, await padrao.TraduzirAsync(evento, ct), padrao.Nome);
    }

    /// <summary>Etapa + saída + tipos em ordem: dois eventos com a mesma assinatura têm a mesma explicação.</summary>
    public static string Assinatura(EventoDeSeguranca evento) =>
        $"{evento.Etapa}|{evento.Sink}|{string.Join(',', evento.Tipos.OrderBy(t => t))}";
}

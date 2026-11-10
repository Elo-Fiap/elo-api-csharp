using Elo.Application.Comum;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elo.Infrastructure.Avisos;

/// <summary>
/// Serviço em segundo plano que baixa os avisos do INMET a cada poucos minutos,
/// para o GET /avisos responder na hora, sem esperar o INMET.
/// </summary>
public sealed class AtualizadorDeAvisosWorker(
    FonteDeAvisosEmCache fonte,
    IOptions<InmetOptions> opcoes,
    TimeProvider relogio,
    ILogger<AtualizadorDeAvisosWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuracao = opcoes.Value;
        if (!configuracao.AtualizacaoAutomatica)
        {
            logger.LogInformation("Atualização automática dos avisos do INMET desligada na configuração.");
            return;
        }

        using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(configuracao.IntervaloEmMinutos), relogio);

        try
        {
            do
            {
                try
                {
                    await fonte.AtualizarAsync(stoppingToken);
                }
                catch (FonteIndisponivelException)
                {
                    // Já foi para o log; tenta de novo no próximo ciclo.
                }
            }
            while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A API está desligando.
        }
    }
}

using Elo.Application.Retencao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elo.Infrastructure.Servicos;

/// <summary>
/// Serviço em segundo plano (BackgroundService) que roda a rotina de retenção de tempos em tempos,
/// de forma assíncrona e sem travar a API.
/// </summary>
public sealed class RetencaoWorker(
    IServiceScopeFactory escopos,
    IOptions<RetencaoOptions> opcoes,
    TimeProvider relogio,
    ILogger<RetencaoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuracao = opcoes.Value;
        if (!configuracao.Habilitada)
        {
            logger.LogInformation("Rotina de retenção desabilitada na configuração.");
            return;
        }

        using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(configuracao.IntervaloEmMinutos), relogio);

        try
        {
            do
            {
                await ExecutarUmaVezAsync(stoppingToken);
            }
            while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A API está desligando: encerramos o laço sem erro.
        }
    }

    private async Task ExecutarUmaVezAsync(CancellationToken stoppingToken)
    {
        try
        {
            // O worker vive a aplicação inteira (singleton), mas o DbContext é por escopo.
            // Por isso cada execução cria o seu próprio escopo.
            await using var escopo = escopos.CreateAsyncScope();
            var servico = escopo.ServiceProvider.GetRequiredService<IRetencaoService>();
            await servico.ExecutarAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Uma falha não derruba a API: tentamos de novo no próximo ciclo.
            logger.LogError(ex, "Falha na rotina de retenção; nova tentativa no próximo ciclo.");
        }
    }
}

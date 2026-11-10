using Elo.Application.Avisos;
using Elo.Application.Comum;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elo.Infrastructure.Avisos;

/// <summary>O lado do cache que o serviço em segundo plano usa: atualizar a lista de tempos em tempos.</summary>
public interface ICacheDeAvisos
{
    /// <summary>Busca de novo na origem, mesmo com a lista guardada ainda boa.</summary>
    Task<IReadOnlyList<AvisoDoInmet>> AtualizarAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache dos avisos do INMET, no padrão Decorator: implementa a mesma <see cref="IFonteDeAvisos"/> da origem
/// que embrulha (em produção, o <see cref="ClienteDoInmet"/>) e acrescenta o cache. Quem pede avisos
/// recebe uma <see cref="IFonteDeAvisos"/> e não sabe se a lista veio do cache ou da origem.
/// <para>
/// O app pode pedir /avisos a toda hora; a origem é chamada no máximo a cada poucos minutos,
/// pelo serviço em segundo plano ou quando o cache envelhece. Se o INMET cair, segue valendo
/// a última lista boa. Só uma requisição baixa por vez (SemaphoreSlim).
/// </para>
/// </summary>
public sealed class FonteDeAvisosEmCache(
    IFonteDeAvisos origem,
    IOptions<InmetOptions> opcoes,
    TimeProvider relogio,
    ILogger<FonteDeAvisosEmCache> logger) : IFonteDeAvisos, ICacheDeAvisos, IDisposable
{
    /// <summary>Depois de uma falha, espera um pouco antes de tentar de novo (o app não fica preso esperando o INMET).</summary>
    public static readonly TimeSpan EsperaDepoisDeFalha = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _umDownloadPorVez = new(1, 1);
    private Retrato? _retrato;
    private long _ultimaFalhaEmTicks;

    private sealed record Retrato(IReadOnlyList<AvisoDoInmet> Avisos, DateTimeOffset BaixadoEm);

    /// <summary>Quando a lista guardada foi baixada (nulo enquanto nada foi baixado).</summary>
    public DateTimeOffset? BaixadoEm => Volatile.Read(ref _retrato)?.BaixadoEm;

    public async Task<IReadOnlyList<AvisoDoInmet>> ObterAvisosAsync(CancellationToken cancellationToken = default)
    {
        if (PodeUsarOGuardado(out var avisos))
        {
            return avisos;
        }

        return await BaixarAsync(somenteSeVelho: true, cancellationToken);
    }

    /// <summary>Usado pelo serviço em segundo plano: baixa de novo, mesmo com o cache ainda bom.</summary>
    public Task<IReadOnlyList<AvisoDoInmet>> AtualizarAsync(CancellationToken cancellationToken = default) =>
        BaixarAsync(somenteSeVelho: false, cancellationToken);

    public void Dispose() => _umDownloadPorVez.Dispose();

    private bool PodeUsarOGuardado(out IReadOnlyList<AvisoDoInmet> avisos)
    {
        var retrato = Volatile.Read(ref _retrato);
        avisos = retrato?.Avisos ?? [];
        if (retrato is null)
        {
            return false;
        }

        var agora = relogio.GetUtcNow();
        var aindaNovo = agora - retrato.BaixadoEm < TimeSpan.FromMinutes(opcoes.Value.IdadeMaximaEmMinutos);
        var ultimaFalha = Interlocked.Read(ref _ultimaFalhaEmTicks);
        var esperandoDepoisDeFalha = ultimaFalha != 0 && agora - new DateTimeOffset(ultimaFalha, TimeSpan.Zero) < EsperaDepoisDeFalha;

        return aindaNovo || esperandoDepoisDeFalha;
    }

    private async Task<IReadOnlyList<AvisoDoInmet>> BaixarAsync(bool somenteSeVelho, CancellationToken cancellationToken)
    {
        await _umDownloadPorVez.WaitAsync(cancellationToken);
        try
        {
            // Quem esperou na fila pode encontrar a lista já atualizada por outra requisição.
            if (somenteSeVelho && PodeUsarOGuardado(out var jaAtualizados))
            {
                return jaAtualizados;
            }

            var avisos = await origem.ObterAvisosAsync(cancellationToken);
            Volatile.Write(ref _retrato, new Retrato(avisos, relogio.GetUtcNow()));
            Interlocked.Exchange(ref _ultimaFalhaEmTicks, 0);

            logger.LogInformation("INMET: {Quantidade} avisos ativos no país.", avisos.Count);
            return avisos;
        }
        catch (Exception erro) when (!cancellationToken.IsCancellationRequested)
        {
            Interlocked.Exchange(ref _ultimaFalhaEmTicks, relogio.GetUtcNow().UtcTicks);

            var anterior = Volatile.Read(ref _retrato);
            if (anterior is not null)
            {
                logger.LogWarning(erro, "INMET sem resposta; seguem os avisos baixados em {BaixadoEm:u}.", anterior.BaixadoEm);
                return anterior.Avisos;
            }

            logger.LogError(erro, "INMET sem resposta e ainda sem avisos guardados.");
            throw new FonteIndisponivelException(
                "Os avisos do INMET estão indisponíveis no momento. Tente de novo em alguns minutos.", erro);
        }
        finally
        {
            _umDownloadPorVez.Release();
        }
    }
}

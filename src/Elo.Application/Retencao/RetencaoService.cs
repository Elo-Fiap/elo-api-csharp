using System.ComponentModel.DataAnnotations;
using Elo.Application.Abstracoes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elo.Application.Retencao;

/// <summary>Configuração da rotina de privacidade (seção "Retencao" do appsettings.json).</summary>
public sealed class RetencaoOptions
{
    public const string Secao = "Retencao";

    public bool Habilitada { get; set; } = true;

    /// <summary>A proposta promete apagar as respostas em 30 dias.</summary>
    [Range(1, 365)]
    public int DiasParaApagarRespostas { get; set; } = 30;

    /// <summary>De quanto em quanto tempo a rotina roda.</summary>
    [Range(1, 1440)]
    public int IntervaloEmMinutos { get; set; } = 60;
}

public sealed record ResultadoDaRetencao(int TreinosEncerrados, int RespostasApagadas);

public interface IRetencaoService
{
    Task<ResultadoDaRetencao> ExecutarAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Rotina de privacidade: encerra treinos que passaram do prazo (guardando o indicador)
/// e apaga respostas com mais de 30 dias. Quem chama periodicamente é um serviço em
/// segundo plano (BackgroundService) da Infraestrutura.
/// </summary>
public sealed class RetencaoService(
    IChamadaRepository chamadas,
    IRuaRepository ruas,
    IUnitOfWork unitOfWork,
    TimeProvider relogio,
    IOptions<RetencaoOptions> opcoes,
    ILogger<RetencaoService> logger) : IRetencaoService
{
    public async Task<ResultadoDaRetencao> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;

        // 1. Treino vencido é encerrado antes, para a foto do indicador não se perder.
        var vencidos = await chamadas.ListarTreinosVencidosAsync(agora, cancellationToken);
        foreach (var treino in vencidos)
        {
            var ruasDoBairro = await ruas.ListarIdsAtivosAsync(treino.BairroId, cancellationToken);
            treino.Encerrar(agora, ruasDoBairro);
        }

        if (vencidos.Count > 0)
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }

        // 2. Respostas com mais de N dias são apagadas (privacidade: uma contagem pode identificar uma casa).
        var dias = opcoes.Value.DiasParaApagarRespostas;
        var apagadas = await chamadas.ApagarRespostasRecebidasAntesDeAsync(agora.AddDays(-dias), cancellationToken);

        if (vencidos.Count > 0 || apagadas > 0)
        {
            logger.LogInformation(
                "Retenção: {Treinos} treino(s) encerrado(s) e {Respostas} resposta(s) com mais de {Dias} dias apagada(s).",
                vencidos.Count,
                apagadas,
                dias);
        }

        return new ResultadoDaRetencao(vencidos.Count, apagadas);
    }
}

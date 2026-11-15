using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Sensores;

namespace Elo.Application.PainelAdmin;

public interface IPainelAdminService
{
    /// <summary>Monta o instantâneo. <paramref name="dias"/> é a janela das chamadas, de 1 a 30 dias.</summary>
    Task<InstantaneoDto> ObterInstantaneoAsync(int dias, CancellationToken cancellationToken = default);
}

/// <summary>Consultas de leitura do painel de administração (sem N+1, sem rastrear entidades).</summary>
public interface IPainelAdminRepository
{
    /// <summary>Todos os bairros com todas as ruas (ativas e inativas), por id.</summary>
    Task<IReadOnlyList<Bairro>> ListarBairrosComRuasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Todos os sensores, cada um com as leituras de <paramref name="desde"/> em diante
    /// (as <paramref name="maximoPorSensor"/> mais recentes, da mais antiga para a mais nova).
    /// </summary>
    Task<IReadOnlyList<SensorComLeituras>> ListarSensoresComLeiturasAsync(
        DateTime desde,
        int maximoPorSensor,
        CancellationToken cancellationToken = default);

    /// <summary>Chamadas abertas em <paramref name="agora"/> ou iniciadas de <paramref name="desde"/> em diante, com respostas e ações.</summary>
    Task<IReadOnlyList<Chamada>> ListarChamadasComDetalhesAsync(
        DateTime agora,
        DateTime desde,
        CancellationToken cancellationToken = default);
}

/// <summary>Um sensor com o bairro da rua dele e as leituras da janela.</summary>
public sealed record SensorComLeituras(Sensor Sensor, int BairroId, IReadOnlyList<LeituraDoSensor> Leituras);

public sealed record LeituraDoSensor(double NivelCm, DateTime MedidaEm);

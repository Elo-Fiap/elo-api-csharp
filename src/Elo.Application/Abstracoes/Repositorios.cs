using Elo.Application.Bairros;
using Elo.Application.Chamadas;
using Elo.Application.Comum;
using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;

namespace Elo.Application.Abstracoes;

public interface IBairroRepository : IRepository<Bairro>
{
    /// <summary>Lista os bairros já projetados em DTO, com o total de ruas e a chamada aberta.</summary>
    Task<IReadOnlyList<BairroResumoDto>> ListarComResumoAsync(DateTime agora, CancellationToken cancellationToken = default);

    Task<Bairro?> ObterComRuasAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExisteComNomeAsync(string nome, string cidade, int? ignorarId, CancellationToken cancellationToken = default);

    Task<bool> PossuiChamadasAsync(int id, CancellationToken cancellationToken = default);
}

public interface IRuaRepository : IRepository<Rua>
{
    Task<IReadOnlyList<Rua>> ListarPorBairroAsync(
        int bairroId,
        string? microarea,
        NivelRisco? nivelRisco,
        bool incluirInativas,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> ListarIdsAtivosAsync(int bairroId, CancellationToken cancellationToken = default);

    Task<bool> ExisteComNomeAsync(int bairroId, string nome, int? ignorarId, CancellationToken cancellationToken = default);

    /// <summary>A rua já tem respostas ou ações? Se tiver, não pode ser excluída (só desativada).</summary>
    Task<bool> PossuiHistoricoAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Resposta>> ListarRespostasAsync(int ruaId, CancellationToken cancellationToken = default);
}

public interface IChamadaRepository : IRepository<Chamada>
{
    Task<PaginaResultado<Chamada>> ListarAsync(FiltroDeChamadas filtro, DateTime agora, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Chamada>> ListarPorBairroAsync(int bairroId, CancellationToken cancellationToken = default);

    Task<Chamada?> ObterAbertaAsync(int bairroId, DateTime agora, CancellationToken cancellationToken = default);

    Task<Chamada?> ObterComRespostasAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Chamadas sem encerramento manual (inclui treinos que já passaram do prazo).</summary>
    Task<IReadOnlyList<Chamada>> ListarNaoEncerradasAsync(int bairroId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChamadaTreino>> ListarTreinosVencidosAsync(DateTime agora, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Resposta>> ListarRespostasAsync(
        int chamadaId,
        SituacaoRua? situacao = null,
        int? ruaId = null,
        CancellationToken cancellationToken = default);

    Task<Resposta?> ObterRespostaAsync(int chamadaId, int respostaId, CancellationToken cancellationToken = default);

    Task<Resposta?> ObterRespostaPorIdLocalAsync(Guid idLocal, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AcaoCoordenador>> ListarAcoesAsync(int chamadaId, CancellationToken cancellationToken = default);

    Task<int> ApagarRespostasRecebidasAntesDeAsync(DateTime limite, CancellationToken cancellationToken = default);
}

public interface ISensorRepository : IRepository<Sensor>
{
    Task<IReadOnlyList<SensorComLeitura>> ListarComUltimaLeituraAsync(int? ruaId, CancellationToken cancellationToken = default);

    Task<SensorComLeitura?> ObterComUltimaLeituraAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExisteComCodigoAsync(string codigo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeituraSensor>> ListarLeiturasAsync(int sensorId, int quantidade, CancellationToken cancellationToken = default);

    /// <summary>Última leitura (até <paramref name="ate"/>) de cada rua do bairro que tem sensor.</summary>
    Task<IReadOnlyDictionary<int, LeituraRecente>> ObterUltimaLeituraPorRuaAsync(
        int bairroId,
        DateTime ate,
        CancellationToken cancellationToken = default);

    /// <summary>Leituras acima do limite no bairro, dentro do período.</summary>
    Task<IReadOnlyList<AlertaDeSensor>> ListarAlertasAsync(
        int bairroId,
        DateTime de,
        DateTime ate,
        CancellationToken cancellationToken = default);
}

/// <summary>Um sensor junto da sua última leitura (se houver).</summary>
public sealed record SensorComLeitura(Sensor Sensor, LeituraSensor? UltimaLeitura);

/// <summary>Leitura acima do limite, já com a rua e o código do sensor (para o registro da chamada).</summary>
public sealed record AlertaDeSensor(int RuaId, string Rua, string CodigoSensor, double NivelCm, double LimiteCm, DateTime MedidaEm);

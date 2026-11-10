using Elo.Domain.Ruas;

namespace Elo.Application.Ruas;

public interface IRuaService
{
    Task<IReadOnlyList<RuaResumoDto>> ListarPorBairroAsync(
        int bairroId,
        string? microarea,
        NivelRisco? nivelRisco,
        bool incluirInativas,
        CancellationToken cancellationToken = default);

    Task<RuaDetalheDto> ObterAsync(int id, CancellationToken cancellationToken = default);

    Task<RuaDetalheDto> CriarAsync(int bairroId, RuaRequest request, CancellationToken cancellationToken = default);

    Task<RuaDetalheDto> AtualizarAsync(int id, RuaRequest request, CancellationToken cancellationToken = default);

    Task RemoverAsync(int id, CancellationToken cancellationToken = default);

    Task<PlanoDaRuaDto> ObterPlanoAsync(int id, CancellationToken cancellationToken = default);

    Task<PlanoDaRuaDto> DefinirPlanoAsync(int id, PlanoDaRuaRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegistroMensalDto>> ObterRegistroAsync(int id, CancellationToken cancellationToken = default);
}

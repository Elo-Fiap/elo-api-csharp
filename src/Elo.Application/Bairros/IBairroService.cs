namespace Elo.Application.Bairros;

public interface IBairroService
{
    Task<IReadOnlyList<BairroResumoDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<BairroDetalheDto> ObterAsync(int id, CancellationToken cancellationToken = default);

    Task<BairroDetalheDto> CriarAsync(BairroRequest request, CancellationToken cancellationToken = default);

    Task<BairroDetalheDto> AtualizarAsync(int id, BairroRequest request, CancellationToken cancellationToken = default);

    Task RemoverAsync(int id, CancellationToken cancellationToken = default);
}

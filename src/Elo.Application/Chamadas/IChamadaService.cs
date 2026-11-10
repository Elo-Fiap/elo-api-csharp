using Elo.Application.Comum;
using Elo.Domain.Chamadas;

namespace Elo.Application.Chamadas;

public interface IChamadaService
{
    Task<PaginaResultado<ChamadaDto>> ListarAsync(FiltroDeChamadas filtro, CancellationToken cancellationToken = default);

    Task<ChamadaDto> ObterAsync(int id, CancellationToken cancellationToken = default);

    Task<ChamadaDto> AbrirAsync(int bairroId, AbrirChamadaRequest request, CancellationToken cancellationToken = default);

    Task<ChamadaDto> EncerrarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Linha do tempo da chamada: o que foi avisado e o que foi feito.</summary>
    Task<IReadOnlyList<EventoDoRegistroDto>> ObterRegistroAsync(int id, CancellationToken cancellationToken = default);
}

public interface IRespostaService
{
    Task<ResultadoDeRegistro<RespostaDto>> RegistrarAsync(
        int chamadaId,
        RegistrarRespostaRequest request,
        CancellationToken cancellationToken = default);

    Task<RespostaDto> ObterAsync(int chamadaId, int respostaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RespostaDto>> ListarAsync(
        int chamadaId,
        SituacaoRua? situacao,
        int? ruaId,
        CancellationToken cancellationToken = default);

    Task<AcaoDto> RegistrarAcaoAsync(int chamadaId, RegistrarAcaoRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AcaoDto>> ListarAcoesAsync(int chamadaId, CancellationToken cancellationToken = default);

    Task<AcaoDto> ObterAcaoAsync(int chamadaId, int acaoId, CancellationToken cancellationToken = default);
}

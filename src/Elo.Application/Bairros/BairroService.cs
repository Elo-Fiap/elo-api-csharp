using Elo.Application.Abstracoes;
using Elo.Application.Comum;
using Elo.Domain.Bairros;
using Microsoft.Extensions.Logging;

namespace Elo.Application.Bairros;

/// <summary>Casos de uso de bairro. Recebe as dependências pelo construtor (injeção de dependência).</summary>
public sealed class BairroService(
    IBairroRepository bairros,
    IUnitOfWork unitOfWork,
    TimeProvider relogio,
    ILogger<BairroService> logger) : IBairroService
{
    public Task<IReadOnlyList<BairroResumoDto>> ListarAsync(CancellationToken cancellationToken = default) =>
        bairros.ListarComResumoAsync(relogio.GetUtcNow().UtcDateTime, cancellationToken);

    public async Task<BairroDetalheDto> ObterAsync(int id, CancellationToken cancellationToken = default)
    {
        var bairro = await bairros.ObterComRuasAsync(id, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Bairro(id);

        return bairro.ParaDetalheDto();
    }

    public async Task<BairroDetalheDto> CriarAsync(BairroRequest request, CancellationToken cancellationToken = default)
    {
        await GarantirNomeLivreAsync(request, ignorarId: null, cancellationToken);

        var bairro = new Bairro(
            request.Nome,
            request.Cidade,
            request.Subprefeitura,
            request.DadosSimulados,
            request.Uf,
            request.CodigoIbge,
            request.Latitude,
            request.Longitude,
            request.PercentualSemApp,
            request.SemAppMedidoEm?.UtcDateTime);
        bairros.Adicionar(bairro);
        await unitOfWork.SalvarAsync(cancellationToken);

        logger.LogInformation("Bairro {BairroId} criado: {Nome}.", bairro.Id, bairro.Nome);
        return bairro.ParaDetalheDto();
    }

    public async Task<BairroDetalheDto> AtualizarAsync(int id, BairroRequest request, CancellationToken cancellationToken = default)
    {
        var bairro = await bairros.ObterComRuasAsync(id, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Bairro(id);

        await GarantirNomeLivreAsync(request, ignorarId: id, cancellationToken);

        bairro.Atualizar(
            request.Nome,
            request.Cidade,
            request.Subprefeitura,
            request.DadosSimulados,
            request.Uf,
            request.CodigoIbge,
            request.Latitude,
            request.Longitude,
            request.PercentualSemApp,
            request.SemAppMedidoEm?.UtcDateTime);
        await unitOfWork.SalvarAsync(cancellationToken);
        return bairro.ParaDetalheDto();
    }

    public async Task RemoverAsync(int id, CancellationToken cancellationToken = default)
    {
        var bairro = await bairros.ObterPorIdAsync(id, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Bairro(id);

        if (await bairros.PossuiChamadasAsync(id, cancellationToken))
        {
            throw new ConflitoException("O bairro tem chamadas registradas e não pode ser excluído.");
        }

        bairros.Remover(bairro);
        await unitOfWork.SalvarAsync(cancellationToken);
        logger.LogInformation("Bairro {BairroId} excluído.", id);
    }

    private async Task GarantirNomeLivreAsync(BairroRequest request, int? ignorarId, CancellationToken cancellationToken)
    {
        var nome = request.Nome.Trim();
        var cidade = request.Cidade.Trim();

        if (await bairros.ExisteComNomeAsync(nome, cidade, ignorarId, cancellationToken))
        {
            throw new ConflitoException($"Já existe o bairro \"{nome}\" em {cidade}.");
        }
    }
}

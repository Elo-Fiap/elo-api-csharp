using Elo.Application.Abstracoes;
using Elo.Application.Bairros;
using Elo.Domain.Bairros;
using Elo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Elo.Infrastructure.Repositorios;

public sealed class BairroRepository(EloDbContext contexto) : RepositoryBase<Bairro>(contexto), IBairroRepository
{
    /// <summary>
    /// Projeção direto para DTO: o EF Core gera um único SELECT com subconsultas
    /// (contagem de ruas ativas e id da chamada aberta), sem carregar entidades inteiras.
    /// </summary>
    public async Task<IReadOnlyList<BairroResumoDto>> ListarComResumoAsync(DateTime agora, CancellationToken cancellationToken = default) =>
        await Conjunto
            .AsNoTracking()
            .OrderBy(bairro => bairro.Nome)
            .Select(bairro => new BairroResumoDto(
                bairro.Id,
                bairro.Nome,
                bairro.Cidade,
                bairro.Subprefeitura,
                bairro.DadosSimulados,
                bairro.Ruas.Count(rua => rua.Ativa),
                Contexto.Chamadas
                    .Where(chamada => chamada.BairroId == bairro.Id
                        && chamada.EncerradaEm == null
                        && (chamada.EncerramentoPrevistoEm == null || chamada.EncerramentoPrevistoEm > agora))
                    .Select(chamada => (int?)chamada.Id)
                    .FirstOrDefault(),
                bairro.Uf,
                bairro.CodigoIbge,
                bairro.Latitude,
                bairro.Longitude,
                bairro.PercentualSemApp,
                bairro.SemAppMedidoEm))
            .ToListAsync(cancellationToken);

    public Task<Bairro?> ObterComRuasAsync(int id, CancellationToken cancellationToken = default) =>
        Conjunto
            .Include(bairro => bairro.Ruas)
            .FirstOrDefaultAsync(bairro => bairro.Id == id, cancellationToken);

    public Task<bool> ExisteComNomeAsync(string nome, string cidade, int? ignorarId, CancellationToken cancellationToken = default)
    {
        var nomeMinusculo = nome.ToLower();
        var cidadeMinuscula = cidade.ToLower();

        return Conjunto.AnyAsync(
            bairro => bairro.Nome.ToLower() == nomeMinusculo
                && bairro.Cidade.ToLower() == cidadeMinuscula
                && bairro.Id != ignorarId,
            cancellationToken);
    }

    public Task<bool> PossuiChamadasAsync(int id, CancellationToken cancellationToken = default) =>
        Contexto.Chamadas.AnyAsync(chamada => chamada.BairroId == id, cancellationToken);
}

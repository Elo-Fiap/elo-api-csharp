using Elo.Application.Abstracoes;
using Elo.Domain.Chamadas;
using Elo.Domain.Ruas;
using Elo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Elo.Infrastructure.Repositorios;

public sealed class RuaRepository(EloDbContext contexto) : RepositoryBase<Rua>(contexto), IRuaRepository
{
    /// <summary>Filtros opcionais montados aos poucos: o EF Core só gera o SQL no ToListAsync.</summary>
    public async Task<IReadOnlyList<Rua>> ListarPorBairroAsync(
        int bairroId,
        string? microarea,
        NivelRisco? nivelRisco,
        bool incluirInativas,
        CancellationToken cancellationToken = default)
    {
        var consulta = Conjunto.AsNoTracking().Where(rua => rua.BairroId == bairroId);

        if (!incluirInativas)
        {
            consulta = consulta.Where(rua => rua.Ativa);
        }

        if (!string.IsNullOrWhiteSpace(microarea))
        {
            var filtro = microarea.Trim();
            consulta = consulta.Where(rua => EF.Functions.Like(rua.Microarea, filtro));
        }

        if (nivelRisco is NivelRisco nivel)
        {
            consulta = consulta.Where(rua => rua.NivelRisco == nivel);
        }

        return await consulta
            .OrderBy(rua => rua.Microarea)
            .ThenBy(rua => rua.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> ListarIdsAtivosAsync(int bairroId, CancellationToken cancellationToken = default) =>
        await Conjunto
            .Where(rua => rua.BairroId == bairroId && rua.Ativa)
            .Select(rua => rua.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> ExisteComNomeAsync(int bairroId, string nome, int? ignorarId, CancellationToken cancellationToken = default)
    {
        var nomeMinusculo = nome.ToLower();

        return Conjunto.AnyAsync(
            rua => rua.BairroId == bairroId && rua.Nome.ToLower() == nomeMinusculo && rua.Id != ignorarId,
            cancellationToken);
    }

    public async Task<bool> PossuiHistoricoAsync(int id, CancellationToken cancellationToken = default) =>
        await Contexto.Respostas.AnyAsync(resposta => resposta.RuaId == id, cancellationToken)
        || await Contexto.Acoes.AnyAsync(acao => acao.RuaId == id, cancellationToken);

    public async Task<IReadOnlyList<Resposta>> ListarRespostasAsync(int ruaId, CancellationToken cancellationToken = default) =>
        await Contexto.Respostas
            .AsNoTracking()
            .Where(resposta => resposta.RuaId == ruaId)
            .OrderByDescending(resposta => resposta.RespondidaEm)
            .ToListAsync(cancellationToken);
}

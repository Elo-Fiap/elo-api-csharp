using Elo.Application.Abstracoes;
using Elo.Domain.Comum;
using Elo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Elo.Infrastructure.Repositorios;

/// <summary>
/// Implementação genérica (generics + herança) do que todo repositório faz.
/// Os repositórios específicos herdam daqui e só acrescentam as consultas próprias.
/// </summary>
public abstract class RepositoryBase<T>(EloDbContext contexto) : IRepository<T>
    where T : Entidade
{
    protected EloDbContext Contexto { get; } = contexto;

    protected DbSet<T> Conjunto => Contexto.Set<T>();

    public virtual async Task<T?> ObterPorIdAsync(int id, CancellationToken cancellationToken = default) =>
        await Conjunto.FindAsync([id], cancellationToken);

    public Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default) =>
        Conjunto.AnyAsync(entidade => entidade.Id == id, cancellationToken);

    public void Adicionar(T entidade) => Conjunto.Add(entidade);

    public void Remover(T entidade) => Conjunto.Remove(entidade);
}

using Elo.Domain.Comum;

namespace Elo.Application.Abstracoes;

/// <summary>
/// Contrato genérico de repositório: o que toda entidade precisa.
/// A Aplicação depende só desta interface; quem implementa (com EF Core) é a Infraestrutura.
/// Assim os serviços podem ser testados com um repositório falso (mock).
/// </summary>
public interface IRepository<T>
    where T : Entidade
{
    Task<T?> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default);

    void Adicionar(T entidade);

    void Remover(T entidade);
}

/// <summary>Unidade de trabalho: grava de uma vez tudo o que mudou durante a requisição.</summary>
public interface IUnitOfWork
{
    Task<int> SalvarAsync(CancellationToken cancellationToken = default);
}

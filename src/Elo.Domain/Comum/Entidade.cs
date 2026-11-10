namespace Elo.Domain.Comum;

/// <summary>
/// Base de todas as entidades do domínio (abstração + herança).
/// Toda entidade tem uma identidade (Id), gerada pelo banco quando é salva.
/// O setter é protegido: só a própria entidade (ou o EF Core) define o Id.
/// </summary>
public abstract class Entidade
{
    public int Id { get; protected set; }
}

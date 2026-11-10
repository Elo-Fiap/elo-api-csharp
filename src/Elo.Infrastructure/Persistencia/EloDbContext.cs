using Elo.Application.Abstracoes;
using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;
using Elo.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;

namespace Elo.Infrastructure.Persistencia;

/// <summary>
/// Contexto do EF Core: liga as classes do domínio às tabelas do banco.
/// Também é a unidade de trabalho (<see cref="IUnitOfWork"/>) usada pelos serviços.
/// </summary>
public sealed class EloDbContext(DbContextOptions<EloDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Bairro> Bairros => Set<Bairro>();
    public DbSet<Rua> Ruas => Set<Rua>();
    public DbSet<Chamada> Chamadas => Set<Chamada>();
    public DbSet<Resposta> Respostas => Set<Resposta>();
    public DbSet<AcaoCoordenador> Acoes => Set<AcaoCoordenador>();
    public DbSet<Sensor> Sensores => Set<Sensor>();
    public DbSet<LeituraSensor> Leituras => Set<LeituraSensor>();

    public Task<int> SalvarAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Cada entidade tem a sua classe de configuração (Fluent API) na pasta Configuracoes.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EloDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Todas as datas são gravadas e lidas como UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<DataUtcConverter>();
    }
}

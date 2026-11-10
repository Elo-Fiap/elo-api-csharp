using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elo.Infrastructure.Persistencia;

/// <summary>
/// Usada só pelas ferramentas do EF Core ("dotnet ef migrations add ...") para criar o contexto
/// sem precisar subir a API. Em execução, quem cria o contexto é a injeção de dependência.
/// </summary>
internal sealed class EloDbContextFactory : IDesignTimeDbContextFactory<EloDbContext>
{
    public EloDbContext CreateDbContext(string[] args)
    {
        var opcoes = new DbContextOptionsBuilder<EloDbContext>()
            .UseSqlite("Data Source=elo.db")
            .Options;

        return new EloDbContext(opcoes);
    }
}

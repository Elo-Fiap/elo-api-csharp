using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elo.Infrastructure.Persistencia;

public static class InicializadorDoBanco
{
    /// <summary>
    /// Aplica as migrations pendentes e, se o banco estiver vazio, cadastra os dados simulados.
    /// Assim quem for avaliar só precisa rodar a API: o banco SQLite é criado sozinho.
    /// </summary>
    public static async Task InicializarBancoAsync(this IServiceProvider servicos, CancellationToken cancellationToken = default)
    {
        await using var escopo = servicos.CreateAsyncScope();
        var provedor = escopo.ServiceProvider;

        var contexto = provedor.GetRequiredService<EloDbContext>();
        var configuracao = provedor.GetRequiredService<IConfiguration>();
        var relogio = provedor.GetRequiredService<TimeProvider>();
        var logger = provedor.GetRequiredService<ILoggerFactory>().CreateLogger("Elo.Banco");

        await contexto.Database.MigrateAsync(cancellationToken);

        var popular = configuracao.GetValue("Banco:PopularComDadosDeExemplo", defaultValue: true);
        if (popular && !await contexto.Bairros.AnyAsync(cancellationToken))
        {
            await DadosDeExemplo.PopularAsync(contexto, relogio.GetUtcNow().UtcDateTime, cancellationToken);
            logger.LogInformation("Banco populado com os dados simulados do Jardim Pantanal.");
        }

        logger.LogInformation("Banco pronto: {Banco}.", contexto.Database.GetDbConnection().DataSource);
    }
}

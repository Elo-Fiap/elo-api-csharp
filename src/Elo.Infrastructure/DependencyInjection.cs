using Elo.Application.Abstracoes;
using Elo.Application.Retencao;
using Elo.Infrastructure.Persistencia;
using Elo.Infrastructure.Repositorios;
using Elo.Infrastructure.Servicos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Elo.Infrastructure;

/// <summary>Registra banco, repositórios e serviços em segundo plano.</summary>
public static class DependencyInjection
{
    public const string NomeDaConexao = "Elo";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // A conexão é lida quando o DbContext é criado (e não no Program.cs);
        // assim os testes conseguem apontar para outro arquivo de banco pela configuração.
        services.AddDbContext<EloDbContext>((provedor, opcoes) =>
        {
            var configuracao = provedor.GetRequiredService<IConfiguration>();
            var ambiente = provedor.GetRequiredService<IHostEnvironment>();
            var conexao = MontarConexao(configuracao.GetConnectionString(NomeDaConexao), ambiente.ContentRootPath);
            opcoes.UseSqlite(conexao);
        });

        // O próprio DbContext faz o papel de unidade de trabalho.
        services.AddScoped<IUnitOfWork>(provedor => provedor.GetRequiredService<EloDbContext>());

        // Injeção de dependência: os serviços pedem a interface e recebem a implementação com EF Core.
        services.AddScoped<IBairroRepository, BairroRepository>();
        services.AddScoped<IRuaRepository, RuaRepository>();
        services.AddScoped<IChamadaRepository, ChamadaRepository>();
        services.AddScoped<ISensorRepository, SensorRepository>();

        services.AddOptions<RetencaoOptions>()
            .BindConfiguration(RetencaoOptions.Secao)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<RetencaoWorker>();

        return services;
    }

    /// <summary>Um caminho relativo do SQLite (ex.: "elo.db") vira absoluto a partir da pasta da API.</summary>
    public static string MontarConexao(string? conexao, string pastaBase)
    {
        var construtor = new SqliteConnectionStringBuilder(
            string.IsNullOrWhiteSpace(conexao) ? "Data Source=elo.db" : conexao);

        var arquivo = construtor.DataSource;
        var ehArquivoRelativo = !string.IsNullOrWhiteSpace(arquivo)
            && arquivo != ":memory:"
            && !arquivo.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            && !Path.IsPathRooted(arquivo);

        if (ehArquivoRelativo)
        {
            construtor.DataSource = Path.GetFullPath(Path.Combine(pastaBase, arquivo));
        }

        return construtor.ToString();
    }
}

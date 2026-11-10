using Elo.Application.Bairros;
using Elo.Application.Chamadas;
using Elo.Application.Painel;
using Elo.Application.Retencao;
using Elo.Application.Ruas;
using Elo.Application.Sensores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elo.Application;

/// <summary>Registra os serviços da camada de aplicação no contêiner de injeção de dependência.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped: uma instância por requisição HTTP (o mesmo tempo de vida do DbContext).
        services.AddScoped<IBairroService, BairroService>();
        services.AddScoped<IRuaService, RuaService>();
        services.AddScoped<IChamadaService, ChamadaService>();
        services.AddScoped<IRespostaService, RespostaService>();
        services.AddScoped<IPainelService, PainelService>();
        services.AddScoped<ISensorService, SensorService>();
        services.AddScoped<IRetencaoService, RetencaoService>();

        // O relógio também é injetado: nos testes trocamos por um relógio falso e controlamos o tempo.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}

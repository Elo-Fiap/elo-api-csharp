using Elo.Seguranca;
using Elo.Seguranca.Taint;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Elo.Api.Seguranca;

/// <summary>Liga o módulo de Cybersecurity à API: serviços, log protegido e os dois middlewares.</summary>
public static class ExtensoesDeSeguranca
{
    /// <summary>Módulo, acesso ao contexto da requisição (HttpContext) e o registro de eventos.</summary>
    public static IServiceCollection AddSegurancaDaApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IAcessoAoContexto, AcessoAoContextoHttp>(); // vale mais que o acesso nulo do módulo
        services.AddModuloDeSeguranca();
        return services;
    }

    /// <summary>
    /// Faz TODOS os providers de log (console, os de teste, qualquer um registrado por qualquer caminho) passarem pela guarda.
    /// A decoração acontece quando a fábrica de loggers é criada, e não em cada registro de provider: assim ela vale também
    /// para providers adicionados depois desta chamada (os testes registram um provider de captura assim). Limitação: regras de filtro por provider
    /// (ex.: "Logging:Console:LogLevel" no appsettings) deixariam de casar, porque o tipo do provider agora é o decorador;
    /// o projeto só usa "Logging:LogLevel", que continua valendo.
    /// </summary>
    public static IServiceCollection ProtegerLogs(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(provedor =>
        {
            var guarda = provedor.GetRequiredService<GuardaDeSaida>();
            var protegido = new ProvedorDeLogProtegido(provedor.GetServices<ILoggerProvider>(), guarda);

            return new LoggerFactory(
                [protegido],
                provedor.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>(),
                provedor.GetService<IOptions<LoggerFactoryOptions>>(),
                provedor.GetService<IExternalScopeProvider>());
        }));

        return services;
    }

    /// <summary>
    /// Guarda de resposta e interceptador de entrada, nesta ordem e ANTES do UseExceptionHandler:
    /// assim o ProblemDetails escrito pelo tratador de exceções também passa pela guarda de saída
    /// (se ela ficasse depois, o tratador escreveria direto no corpo real, sem passar por ela).
    /// </summary>
    public static IApplicationBuilder UseSegurancaDeEntradaESaida(this IApplicationBuilder app)
    {
        app.UseMiddleware<GuardaDeRespostaHttp>();
        app.UseMiddleware<InterceptadorDePii>();
        return app;
    }
}

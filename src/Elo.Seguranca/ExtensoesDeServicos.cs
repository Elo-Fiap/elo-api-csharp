using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Risco;
using Elo.Seguranca.Taint;
using Elo.Seguranca.Traducao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elo.Seguranca;

public static class ExtensoesDeServicos
{
    /// <summary>
    /// Registra o módulo de Cybersecurity: motor de PII, guarda de saída, registro de eventos, cálculo do risco e a tradução pronta (sem rede).
    /// Pode ser chamado mais de uma vez (só registra o que ainda não existe), então a Api e a Infrastructure chamam sem medo.
    /// A Api registra o <see cref="IAcessoAoContexto"/> de verdade (HttpContext); sem ele, o contexto é sempre nulo.
    /// </summary>
    public static IServiceCollection AddModuloDeSeguranca(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(MotorDePii.Padrao);
        services.TryAddSingleton<IAcessoAoContexto, AcessoNuloAoContexto>();
        services.TryAddSingleton<IRegistroDeEventos, RegistroDeEventosEmMemoria>();
        services.TryAddSingleton<GuardaDeSaida>();
        services.TryAddSingleton<CalculadoraDeRiscoCidadao>();
        services.TryAddSingleton<TradutorPadrao>();
        return services;
    }
}

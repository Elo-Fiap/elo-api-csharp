using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;

namespace Elo.Api.Seguranca;

/// <summary>
/// Decora TODOS os providers de log (console, debug, arquivo, os de teste...) de uma vez: cada mensagem passa
/// uma única vez pela <see cref="GuardaDeSaida"/> (sink "Log") e só então segue para cada provider original.
/// Os providers não precisam saber de nada: nem o formatter JSON do console vê o valor sensível.
/// Um provider decorador só (e não um por provider) evita registrar um evento por provider para a mesma mensagem.
/// Os providers originais continuam sendo do contêiner de DI, que os descarta: este aqui não os descarta.
/// </summary>
public sealed class ProvedorDeLogProtegido(IEnumerable<ILoggerProvider> internos, GuardaDeSaida guarda) : ILoggerProvider, ISupportExternalScope
{
    private readonly ILoggerProvider[] _internos = [.. internos];

    public ILogger CreateLogger(string categoria) =>
        new LoggerProtegido([.. _internos.Select(p => p.CreateLogger(categoria))], guarda, categoria);

    /// <summary>Repassa o provedor de escopos da fábrica aos providers originais, que o usam para os escopos (ex.: id da requisição).</summary>
    public void SetScopeProvider(IExternalScopeProvider provedor)
    {
        foreach (var interno in _internos.OfType<ISupportExternalScope>())
        {
            interno.SetScopeProvider(provedor);
        }
    }

    public void Dispose()
    {
        // Não descarta os providers internos: eles pertencem ao contêiner de DI.
    }
}

/// <summary>O logger decorado: limpa a mensagem e o texto da exceção uma vez; sem achado, repassa tudo como veio.</summary>
public sealed class LoggerProtegido(IReadOnlyList<ILogger> internos, GuardaDeSaida guarda, string categoria) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
        new EscoposJuntos([.. internos.Select(l => l.BeginScope(state)).OfType<IDisposable>()]);

    public bool IsEnabled(LogLevel nivel) => internos.Any(l => l.IsEnabled(nivel));

    public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? excecao, Func<TState, Exception?, string> formatador)
    {
        if (!IsEnabled(nivel))
        {
            return;
        }

        string mensagem;
        try
        {
            mensagem = formatador(estado, excecao);
        }
        catch (Exception)
        {
            // Formatador com defeito: deixa os providers lidarem com isso, como sempre.
            Repassar(nivel, evento, estado, excecao, formatador);
            return;
        }

        var limpa = guarda.Verificar(mensagem, Sink.Log, ModoDeAnalise.TextoLivre, categoria);

        var excecaoLimpa = excecao;
        var excecaoMudou = false;
        if (excecao is not null)
        {
            var limpaDaExcecao = guarda.Verificar(excecao.ToString(), Sink.Log, ModoDeAnalise.TextoLivre, categoria);
            if (limpaDaExcecao.TemPii)
            {
                excecaoMudou = true;
                var texto = limpaDaExcecao.TextoMascarado;
                excecaoLimpa = new ExcecaoSanitizada(texto.Split('\n')[0].TrimEnd('\r'), texto);
            }
        }

        if (!limpa.TemPii && !excecaoMudou)
        {
            Repassar(nivel, evento, estado, excecao, formatador);
            return;
        }

        // O estado original tem os valores nos argumentos; ele não chega aos providers. Vai só a mensagem limpa.
        var mensagemLimpa = limpa.TextoMascarado;
        IReadOnlyList<KeyValuePair<string, object?>> estadoLimpo = [new("{OriginalFormat}", mensagemLimpa)];
        Repassar(nivel, evento, estadoLimpo, excecaoLimpa, (_, _) => mensagemLimpa);
    }

    private void Repassar<TEstado>(LogLevel nivel, EventId evento, TEstado estado, Exception? excecao, Func<TEstado, Exception?, string> formatador)
    {
        foreach (var interno in internos)
        {
            if (interno.IsEnabled(nivel))
            {
                interno.Log(nivel, evento, estado, excecao, formatador);
            }
        }
    }

    private sealed class EscoposJuntos(IDisposable[] escopos) : IDisposable
    {
        public void Dispose()
        {
            foreach (var escopo in escopos)
            {
                escopo.Dispose();
            }
        }
    }
}

/// <summary>No lugar da exceção original quando o texto dela tinha dado sensível: guarda só a mensagem e o stack já limpos.</summary>
public sealed class ExcecaoSanitizada(string mensagem, string textoCompleto) : Exception(mensagem)
{
    public override string? StackTrace => textoCompleto;

    public override string ToString() => textoCompleto;
}

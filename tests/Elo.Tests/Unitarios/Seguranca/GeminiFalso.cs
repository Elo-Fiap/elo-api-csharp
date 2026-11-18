using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Elo.Infrastructure.Seguranca;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Elo.Seguranca.Traducao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Elo.Tests.Unitarios.Seguranca;

/// <summary>Uma requisição que o Gemini "falso" recebeu (o corpo já lido, porque o HttpClient descarta o conteúdo depois).</summary>
internal sealed record RequisicaoRecebida(HttpMethod Metodo, Uri Endereco, IReadOnlyDictionary<string, string> Cabecalhos, string Corpo);

/// <summary>O lugar do Gemini nos testes: guarda o que recebeu e responde o que o teste mandar. Nada acessa a rede.</summary>
internal sealed class GeminiFalso(Func<RequisicaoRecebida, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public ConcurrentQueue<RequisicaoRecebida> Requisicoes { get; } = new();

    public static GeminiFalso Respondendo(string texto) => new((_, _) => Task.FromResult(RespostaComTexto(texto)));

    public static GeminiFalso Status(HttpStatusCode codigo, string corpo = "{}") =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(codigo) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") }));

    public static HttpResponseMessage RespostaComTexto(string texto) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = texto } } } } } }),
                Encoding.UTF8,
                "application/json")
        };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage requisicao, CancellationToken ct)
    {
        var recebida = new RequisicaoRecebida(
            requisicao.Method,
            requisicao.RequestUri!,
            requisicao.Headers.ToDictionary(h => h.Key, h => string.Join(',', h.Value), StringComparer.OrdinalIgnoreCase),
            requisicao.Content is null ? string.Empty : await requisicao.Content.ReadAsStringAsync(ct));

        Requisicoes.Enqueue(recebida);
        return await responder(recebida, ct);
    }
}

internal sealed class FabricaDeHttpFalsa(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri(TradutorGemini.EnderecoBase) };
}

internal sealed class LoggerDeCaptura<T> : ILogger<T>
{
    public ConcurrentQueue<string> Linhas { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel nivel) => true;

    public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? excecao, Func<TState, Exception?, string> formatador) =>
        Linhas.Enqueue($"{formatador(estado, excecao)} {excecao}");
}

internal sealed class AcessoDeTeste(ContextoDeContaminacao? contexto = null) : IAcessoAoContexto
{
    public ContextoDeContaminacao? Atual { get; } = contexto;
}

/// <summary>Monta o TradutorGemini com tudo falso: relógio, handler, guarda, registro e log.</summary>
internal sealed class CenarioDeTradutor
{
    public const string Chave = "chave-falsa-so-para-teste-123";

    public CenarioDeTradutor(GeminiFalso gemini, IaOptions? ia = null, ContextoDeContaminacao? contexto = null)
    {
        Gemini = gemini;
        Opcoes = ia ?? new IaOptions { Habilitada = true, Gemini = { ChaveApi = Chave } };
        Registro = new RegistroDeEventosEmMemoria(Relogio);
        Guarda = new GuardaDeSaida(MotorDePii.Padrao, new AcessoDeTeste(contexto), Registro);
        Tradutor = new TradutorGemini(new FabricaDeHttpFalsa(gemini), Options.Create(Opcoes), Guarda, new TradutorPadrao(), Relogio, Log);
    }

    public FakeTimeProvider Relogio { get; } = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
    public GeminiFalso Gemini { get; }
    public IaOptions Opcoes { get; }
    public RegistroDeEventosEmMemoria Registro { get; }
    public GuardaDeSaida Guarda { get; }
    public LoggerDeCaptura<TradutorGemini> Log { get; } = new();
    public TradutorGemini Tradutor { get; }

    public EventoDeSeguranca NovoEvento(
        EtapaDoEvento etapa = EtapaDoEvento.Saida,
        Sink? sink = Sink.Log,
        TipoDePii[]? tipos = null,
        string? descricao = null) =>
        Registro.Registrar(
            etapa,
            etapa == EtapaDoEvento.Entrada ? null : sink,
            etapa == EtapaDoEvento.Entrada ? AcaoDeSeguranca.Marcado : AcaoDeSeguranca.Bloqueado,
            tipos ?? [TipoDePii.Cpf, TipoDePii.DadoDeSaude],
            2,
            "Elo.Application.Chamadas.RespostaService",
            descricao);
}

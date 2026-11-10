using System.Text.Json;
using Elo.Application.Avisos;
using Microsoft.Extensions.Options;

namespace Elo.Infrastructure.Avisos;

/// <summary>Baixa os avisos ativos do INMET (Brasil inteiro).</summary>
public interface IClienteDoInmet
{
    Task<IReadOnlyList<AvisoDoInmet>> BaixarAvisosAtivosAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cliente HTTP do INMET. Usa o IHttpClientFactory (cliente nomeado), então pode ser singleton
/// sem prender conexões; lê a resposta em fluxo (stream), sem carregar o texto inteiro antes.
/// </summary>
public sealed class ClienteDoInmet(IHttpClientFactory fabrica, IOptions<InmetOptions> opcoes) : IClienteDoInmet
{
    public const string NomeDoCliente = "inmet";

    public async Task<IReadOnlyList<AvisoDoInmet>> BaixarAvisosAtivosAsync(CancellationToken cancellationToken = default)
    {
        var http = fabrica.CreateClient(NomeDoCliente);

        using var resposta = await http.GetAsync(opcoes.Value.UrlDoInmet, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        resposta.EnsureSuccessStatusCode();

        await using var corpo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);

        return LeitorDeAvisosDoInmet.Ler(json.RootElement);
    }
}

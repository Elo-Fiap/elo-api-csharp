using System.Text.Json;
using Elo.Application.Avisos;
using Microsoft.Extensions.Options;

namespace Elo.Infrastructure.Avisos;

/// <summary>
/// A fonte "de verdade": vai direto ao INMET e devolve os avisos ativos do Brasil inteiro, sem cache.
/// É o componente que a <see cref="FonteDeAvisosEmCache"/> embrulha (padrão Decorator): as duas
/// implementam <see cref="IFonteDeAvisos"/>.
/// Usa o IHttpClientFactory (cliente nomeado), então pode ser singleton sem prender conexões,
/// e lê a resposta em fluxo (stream), sem carregar o texto inteiro antes.
/// </summary>
public sealed class ClienteDoInmet(IHttpClientFactory fabrica, IOptions<InmetOptions> opcoes) : IFonteDeAvisos
{
    public const string NomeDoCliente = "inmet";

    public async Task<IReadOnlyList<AvisoDoInmet>> ObterAvisosAsync(CancellationToken cancellationToken = default)
    {
        var http = fabrica.CreateClient(NomeDoCliente);

        using var resposta = await http.GetAsync(opcoes.Value.UrlDoInmet, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        resposta.EnsureSuccessStatusCode();

        await using var corpo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);

        return LeitorDeAvisosDoInmet.Ler(json.RootElement);
    }
}

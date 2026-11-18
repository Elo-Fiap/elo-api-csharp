using Elo.Seguranca.Eventos;

namespace Elo.Seguranca.Traducao;

/// <summary>
/// Traduz a falha técnica de um evento de segurança para uma mensagem que um cidadão leigo entende.
/// Só recebe o evento (metadados): o dado pessoal nunca chega até aqui.
/// </summary>
public interface ITradutorDeRisco
{
    /// <summary>Quem escreveu a mensagem ("claude", "gemini" ou "modelo padrão"). Vai para <c>EventoDeSeguranca.TraduzidaPor</c>.</summary>
    string Nome { get; }

    Task<string> TraduzirAsync(EventoDeSeguranca evento, CancellationToken ct);
}

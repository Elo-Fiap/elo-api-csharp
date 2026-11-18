using Elo.Seguranca.Eventos;

namespace Elo.Infrastructure.Seguranca;

/// <summary>
/// Um LLM que traduz a falha técnica para o cidadão. O worker só conhece esta interface,
/// então trocar de Claude para Gemini é só mudar <c>Ia:Provedor</c> na configuração.
/// </summary>
public interface IProvedorDeIa
{
    /// <summary>Quem escreveu a mensagem, para o painel mostrar ("claude" ou "gemini").</summary>
    string Nome { get; }

    /// <summary>Há chave e a IA não está desligada. Sem isso nem tenta a rede.</summary>
    bool Configurado { get; }

    /// <summary>A tradução, ou <c>null</c> quando o LLM não respondeu (o worker então usa a mensagem pronta).</summary>
    Task<string?> TentarTraduzirAsync(EventoDeSeguranca evento, CancellationToken ct);
}

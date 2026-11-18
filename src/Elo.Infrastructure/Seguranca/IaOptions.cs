using System.ComponentModel.DataAnnotations;

namespace Elo.Infrastructure.Seguranca;

/// <summary>Qual LLM traduz as falhas de segurança.</summary>
public enum ProvedorDoLlm
{
    Claude = 1,
    Gemini = 2
}

/// <summary>Configuração da tradução das falhas de segurança por IA (seção "Ia" do appsettings.json).</summary>
public sealed class IaOptions
{
    public const string Secao = "Ia";

    /// <summary>Desliga o LLM de uma vez (os testes usam falso): sobram só as mensagens prontas do <c>TradutorPadrao</c>.</summary>
    public bool Habilitada { get; set; } = true;

    /// <summary>Claude (padrão) ou Gemini. O outro fica parado mesmo que tenha chave.</summary>
    public ProvedorDoLlm Provedor { get; set; } = ProvedorDoLlm.Claude;

    /// <summary>Quanto esperar o LLM. Passou disso, a mensagem pronta é usada: o painel não pode ficar sem texto.</summary>
    [Range(1, 60)]
    public int TempoLimiteEmSegundos { get; set; } = 20;

    public ClaudeOptions Claude { get; set; } = new();

    public GeminiOptions Gemini { get; set; } = new();
}

/// <summary>Seção "Ia:Claude". A chave NUNCA fica no repositório: user-secrets ou a variável <c>Ia__Claude__ChaveApi</c>.</summary>
public sealed class ClaudeOptions
{
    public const string ModeloPadrao = "claude-opus-5-5";

    /// <summary>Vazia no appsettings.json (como a chave do ESP32). Sem chave, o LLM não é chamado.</summary>
    public string? ChaveApi { get; set; }

    public string Modelo { get; set; } = ModeloPadrao;
}

/// <summary>Seção "Ia:Gemini". A chave NUNCA fica no repositório: user-secrets ou a variável <c>Ia__Gemini__ChaveApi</c>.</summary>
public sealed class GeminiOptions
{
    public const string ModeloPadrao = "gemini-2.5-flash";

    /// <summary>Vazia no appsettings.json (como a chave do ESP32). Sem chave, o LLM não é chamado.</summary>
    public string? ChaveApi { get; set; }

    public string Modelo { get; set; } = ModeloPadrao;
}

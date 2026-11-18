using Elo.Seguranca.Pii;

namespace Elo.Seguranca.Taint;

/// <summary>Onde o dado sensível pode vazar: as saídas (sinks) da taint analysis.</summary>
public enum Sink
{
    Banco,
    Log,
    RespostaHttp,
    Llm
}

/// <summary>Um valor marcado na entrada. Guarda o valor só em memória, só pelo tempo da requisição.</summary>
public sealed record ValorContaminado(string Valor, TipoDePii Tipo, string Origem)
{
    /// <summary>Nunca imprime o valor: este registro pode acabar num log sem querer.</summary>
    public override string ToString() => $"ValorContaminado {{ Tipo = {Tipo}, Origem = {Origem} }}";
}

/// <summary>
/// A "tinta" da taint analysis, uma por requisição: na entrada (source) cada dado sensível achado é marcado aqui;
/// antes de cada saída (sink) a <see cref="GuardaDeSaida"/> troca por máscara tudo o que estiver marcado.
/// Assim o dado some mesmo quando nenhum padrão o reconheceria (ex.: a senha "abc12345" solta num log).
/// Nunca expõe o valor fora do processo.
/// </summary>
public sealed class ContextoDeContaminacao
{
    /// <summary>Valores com menos de 4 caracteres seriam trocados em qualquer lugar ("1", "sim") e estragariam o texto.</summary>
    public const int TamanhoMinimo = 4;

    private readonly object _trava = new();
    private readonly List<ValorContaminado> _valores = [];

    /// <summary>Marca um valor como sensível. A <c>origem</c> diz de onde veio, sem o valor (ex.: "corpo $.observacao" ou "query bairroId").</summary>
    public void Marcar(string valor, TipoDePii tipo, string origem)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length < TamanhoMinimo)
        {
            return;
        }

        lock (_trava)
        {
            if (!_valores.Any(v => v.Valor == valor))
            {
                _valores.Add(new ValorContaminado(valor, tipo, origem));
            }
        }
    }

    /// <summary>Uma cópia: quem lê não é afetado por marcações que chegam depois.</summary>
    public IReadOnlyCollection<ValorContaminado> Valores
    {
        get
        {
            lock (_trava)
            {
                return [.. _valores];
            }
        }
    }

    public override string ToString() => $"ContextoDeContaminacao {{ Quantidade = {Valores.Count} }}";
}

/// <summary>
/// Dá acesso ao contexto da requisição atual. A API implementa guardando o contexto em <c>HttpContext.Items</c>;
/// fora de uma requisição (semente do banco, workers) devolve <c>null</c>.
/// </summary>
public interface IAcessoAoContexto
{
    ContextoDeContaminacao? Atual { get; }
}

/// <summary>Usado quando ninguém registrou um acesso (testes e workers): sem requisição, nada marcado.</summary>
internal sealed class AcessoNuloAoContexto : IAcessoAoContexto
{
    public ContextoDeContaminacao? Atual => null;
}

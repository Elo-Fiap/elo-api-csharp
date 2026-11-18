using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;

namespace Elo.Seguranca.Eventos;

public enum EtapaDoEvento
{
    /// <summary>O dado chegou e foi reconhecido e marcado.</summary>
    Entrada,

    /// <summary>O dado tentou sair (log, banco, resposta HTTP, LLM) e foi barrado.</summary>
    Saida
}

public enum AcaoDeSeguranca
{
    Marcado,
    Bloqueado
}

/// <summary>
/// Um evento de segurança. Só tem metadados (tipos, quantidade, onde): nenhum campo guarda o dado pessoal.
/// A mensagem ao cidadão é preenchida depois, pela tradução (Parte de IA).
/// </summary>
public sealed class EventoDeSeguranca
{
    private sealed record Traducao(string Mensagem, string TraduzidaPor);

    private Traducao? _traducao;

    public EventoDeSeguranca(
        long id,
        DateTimeOffset ocorridoEm,
        EtapaDoEvento etapa,
        Sink? sink,
        AcaoDeSeguranca acao,
        IReadOnlyList<TipoDePii> tipos,
        int quantidade,
        string onde,
        Severidade severidade,
        string descricaoTecnica)
    {
        Id = id;
        OcorridoEm = ocorridoEm;
        Etapa = etapa;
        Sink = sink;
        Acao = acao;
        Tipos = tipos;
        Quantidade = quantidade;
        Onde = onde;
        Severidade = severidade;
        DescricaoTecnica = descricaoTecnica;
    }

    public long Id { get; }
    public DateTimeOffset OcorridoEm { get; }
    public EtapaDoEvento Etapa { get; }

    /// <summary>Nulo na entrada (ainda não saiu para lugar nenhum).</summary>
    public Sink? Sink { get; }

    public AcaoDeSeguranca Acao { get; }
    public IReadOnlyList<TipoDePii> Tipos { get; }
    public int Quantidade { get; }

    /// <summary>Rota com ids trocados por {id} (ex.: "POST /chamadas"), categoria do log ou propriedade do banco ("Resposta.Observacao").</summary>
    public string Onde { get; }

    /// <summary>A maior severidade entre os tipos.</summary>
    public Severidade Severidade { get; }

    /// <summary>A falha técnica, gerada só com metadados. É isto que o LLM traduz.</summary>
    public string DescricaoTecnica { get; }

    public string? MensagemAoCidadao => Volatile.Read(ref _traducao)?.Mensagem;

    /// <summary>"claude", "gemini" ou "modelo padrão".</summary>
    public string? TraduzidaPor => Volatile.Read(ref _traducao)?.TraduzidaPor;

    /// <summary>Só o registro de eventos preenche (via <see cref="IRegistroDeEventos.DefinirMensagem"/>).</summary>
    internal void DefinirTraducao(string mensagem, string traduzidaPor) =>
        Volatile.Write(ref _traducao, new Traducao(mensagem, traduzidaPor));
}

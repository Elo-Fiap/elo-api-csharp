using System.Threading.Channels;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;

namespace Elo.Seguranca.Eventos;

/// <summary>Onde os eventos de segurança ficam guardados e de onde o painel e a tradução os leem.</summary>
public interface IRegistroDeEventos
{
    /// <summary>
    /// Guarda um evento novo e avisa quem escuta <see cref="NovosEventos"/>.
    /// A <paramref name="descricaoTecnica"/> é gerada pelo registro quando não é informada.
    /// </summary>
    EventoDeSeguranca Registrar(
        EtapaDoEvento etapa,
        Sink? sink,
        AcaoDeSeguranca acao,
        IReadOnlyList<TipoDePii> tipos,
        int quantidade,
        string onde,
        string? descricaoTecnica = null);

    /// <summary>Do mais recente para o mais antigo. <paramref name="desde"/> nulo = tudo o que ainda está no buffer.</summary>
    IReadOnlyList<EventoDeSeguranca> Listar(DateTimeOffset? desde, int limite);

    /// <summary>Preenche a mensagem ao cidadão de um evento. Devolve falso se o evento já saiu do buffer.</summary>
    bool DefinirMensagem(long id, string mensagem, string traduzidaPor);

    /// <summary>
    /// Os ids dos eventos que acabaram de ser registrados, para o worker de tradução.
    /// Só deve ter UM leitor (ele consome os ids). O canal tem capacidade limitada e, se estiver cheio,
    /// descarta o id mais antigo: o evento continua na lista, só fica sem mensagem traduzida.
    /// </summary>
    ChannelReader<long> NovosEventos { get; }
}

/// <summary>
/// Registro em memória, de propósito: sem migration nem tabela nova (outra frente mexe no banco), e o painel
/// pode zerar quando a API reinicia (isso é documentado). Guarda os últimos 500 eventos num buffer circular.
/// </summary>
public sealed class RegistroDeEventosEmMemoria(TimeProvider relogio) : IRegistroDeEventos
{
    public const int Capacidade = 500;
    private const int CapacidadeDoCanal = 200;

    private readonly object _trava = new();
    private readonly LinkedList<EventoDeSeguranca> _eventos = new();
    private readonly Channel<long> _canal = Channel.CreateBounded<long>(
        new BoundedChannelOptions(CapacidadeDoCanal)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
    private long _ultimoId;

    public ChannelReader<long> NovosEventos => _canal.Reader;

    public EventoDeSeguranca Registrar(
        EtapaDoEvento etapa,
        Sink? sink,
        AcaoDeSeguranca acao,
        IReadOnlyList<TipoDePii> tipos,
        int quantidade,
        string onde,
        string? descricaoTecnica = null)
    {
        var tiposDistintos = tipos.Distinct().ToArray();
        var severidade = tiposDistintos.Length == 0 ? Severidade.Baixa : tiposDistintos.Max(t => t.SeveridadeDoTipo());

        EventoDeSeguranca evento;
        lock (_trava)
        {
            evento = new EventoDeSeguranca(
                ++_ultimoId,
                relogio.GetUtcNow(),
                etapa,
                sink,
                acao,
                tiposDistintos,
                quantidade,
                onde,
                severidade,
                descricaoTecnica ?? DescreverEvento(etapa, sink, tiposDistintos, onde));

            _eventos.AddLast(evento);
            if (_eventos.Count > Capacidade)
            {
                _eventos.RemoveFirst();
            }
        }

        _canal.Writer.TryWrite(evento.Id);
        return evento;
    }

    public IReadOnlyList<EventoDeSeguranca> Listar(DateTimeOffset? desde, int limite)
    {
        lock (_trava)
        {
            return [.. _eventos
                .Reverse()
                .Where(e => desde is null || e.OcorridoEm >= desde)
                .Take(Math.Max(0, limite))];
        }
    }

    public bool DefinirMensagem(long id, string mensagem, string traduzidaPor)
    {
        lock (_trava)
        {
            var evento = _eventos.FirstOrDefault(e => e.Id == id);
            if (evento is null)
            {
                return false;
            }

            evento.DefinirTraducao(mensagem, traduzidaPor);
            return true;
        }
    }

    /// <summary>
    /// Só metadados, nunca o valor. Ex.: "Taint sink bloqueado: DadoDeSaude, Cpf em Log (Elo.Application.Chamadas.RespostaService)".
    /// </summary>
    public static string DescreverEvento(EtapaDoEvento etapa, Sink? sink, IEnumerable<TipoDePii> tipos, string onde)
    {
        var lista = string.Join(", ", tipos);
        return etapa == EtapaDoEvento.Entrada
            ? $"Parsing de PII na entrada: {lista} marcados em {onde}"
            : $"Taint sink bloqueado: {lista} em {sink} ({onde})";
    }
}

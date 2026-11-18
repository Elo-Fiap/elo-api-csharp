using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;

namespace Elo.Seguranca.Risco;

/// <summary>A cor do farol do painel Risco Cidadão. Na tela vai sempre com texto, nunca só a cor.</summary>
public enum Farol
{
    /// <summary>Nenhum evento na janela.</summary>
    Verde,

    /// <summary>Dado sensível chegou e foi marcado, mas nada tentou sair.</summary>
    Amarelo,

    /// <summary>Pelo menos uma tentativa de saída (log, resposta HTTP, banco ou LLM) foi bloqueada.</summary>
    Vermelho
}

/// <summary>Quantos eventos (e não quantos dados) houve em cada ponto: a entrada e as quatro saídas.</summary>
public sealed record TotaisPorSaida(int Entrada, int Banco, int Log, int RespostaHttp, int Llm);

/// <summary>O retrato do risco na janela: farol, índice, frase ao cidadão e os números que a tela desenha.</summary>
public sealed record ResultadoDoRisco(
    Farol Farol,
    int Indice,
    int Pontos,
    string Resumo,
    int JanelaEmHoras,
    TotaisPorSaida Totais,
    IReadOnlyDictionary<TipoDePii, int> PorTipo,
    IReadOnlyList<EventoDeSeguranca> UltimosEventos);

/// <summary>
/// Calcula o "Risco Cidadão" a partir dos eventos de segurança da janela (padrão: 24 horas).
/// <para>
/// <b>Pontos</b> = soma, por evento, de (peso da severidade × peso da etapa).
/// Severidade: Baixa 1, Média 2, Alta 3. Etapa: entrada marcada 1, saída bloqueada 3 (sair é pior do que só chegar).
/// </para>
/// <para><b>Índice</b> (0 a 100) = o menor entre 100 e (pontos × 4). Com 25 pontos o índice já é 100.</para>
/// <para>
/// <b>Farol</b>: verde = nenhum evento; amarelo = só eventos de entrada; vermelho = pelo menos uma saída bloqueada.
/// </para>
/// </summary>
public sealed class CalculadoraDeRiscoCidadao(IRegistroDeEventos registro, TimeProvider relogio)
{
    public const int JanelaPadraoEmHoras = 24;
    public const int PontosParaCadaPontoDeIndice = 4;
    public const int LimiteDoIndice = 100;

    private const int EventosRecentes = 20;
    private const int PesoDaEntrada = 1;
    private const int PesoDaSaida = 3;

    public const string ResumoVerde = "Tudo certo: nenhuma informação sensível foi enviada ao Elo nas últimas horas.";
    public const string ResumoAmarelo = "Informações delicadas chegaram ao Elo e estão protegidas. Nada tentou sair do sistema.";
    public const string ResumoVermelho = "O Elo barrou tentativas de mostrar ou gravar informações delicadas. Os dados continuam protegidos.";

    public ResultadoDoRisco Calcular(int janelaEmHoras = JanelaPadraoEmHoras)
    {
        var desde = relogio.GetUtcNow().AddHours(-janelaEmHoras);
        var eventos = registro.Listar(desde, int.MaxValue); // do mais recente para o mais antigo

        var pontos = eventos.Sum(Pontuar);
        var indice = Math.Min(LimiteDoIndice, pontos * PontosParaCadaPontoDeIndice);

        var farol = eventos.Count == 0 ? Farol.Verde
            : eventos.Any(e => e.Etapa == EtapaDoEvento.Saida) ? Farol.Vermelho
            : Farol.Amarelo;

        return new ResultadoDoRisco(
            farol,
            indice,
            pontos,
            EscolherResumo(farol, eventos),
            janelaEmHoras,
            ContarPorSaida(eventos),
            ContarPorTipo(eventos),
            [.. eventos.Take(EventosRecentes)]);
    }

    private static int Pontuar(EventoDeSeguranca evento) =>
        (int)evento.Severidade * (evento.Etapa == EtapaDoEvento.Entrada ? PesoDaEntrada : PesoDaSaida);

    /// <summary>
    /// A frase ao cidadão é a mensagem traduzida do evento mais grave e mais recente.
    /// Só vale um evento da etapa que define a cor do farol (num farol vermelho, um evento de saída), para a frase combinar com a cor.
    /// Enquanto a tradução não chegou (ou sem eventos), usa a frase padrão do farol.
    /// </summary>
    private static string EscolherResumo(Farol farol, IReadOnlyList<EventoDeSeguranca> eventos)
    {
        var padrao = farol switch
        {
            Farol.Verde => ResumoVerde,
            Farol.Amarelo => ResumoAmarelo,
            _ => ResumoVermelho
        };

        var etapaDoFarol = farol == Farol.Vermelho ? EtapaDoEvento.Saida : EtapaDoEvento.Entrada;
        var traduzido = eventos
            .Where(e => e.Etapa == etapaDoFarol && e.MensagemAoCidadao is not null)
            .OrderByDescending(e => e.Severidade)
            .ThenByDescending(e => e.OcorridoEm)
            .ThenByDescending(e => e.Id)
            .FirstOrDefault();

        return traduzido?.MensagemAoCidadao ?? padrao;
    }

    private static TotaisPorSaida ContarPorSaida(IReadOnlyList<EventoDeSeguranca> eventos) => new(
        Entrada: eventos.Count(e => e.Etapa == EtapaDoEvento.Entrada),
        Banco: eventos.Count(e => e.Sink == Sink.Banco),
        Log: eventos.Count(e => e.Sink == Sink.Log),
        RespostaHttp: eventos.Count(e => e.Sink == Sink.RespostaHttp),
        Llm: eventos.Count(e => e.Sink == Sink.Llm));

    /// <summary>Quantos eventos citam cada tipo (um evento com CPF e senha conta uma vez para cada).</summary>
    private static Dictionary<TipoDePii, int> ContarPorTipo(IReadOnlyList<EventoDeSeguranca> eventos) =>
        eventos
            .SelectMany(e => e.Tipos.Distinct())
            .GroupBy(t => t)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());
}

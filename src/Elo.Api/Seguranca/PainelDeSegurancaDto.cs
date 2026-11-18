using System.Text.Json;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Risco;
using Elo.Seguranca.Taint;

namespace Elo.Api.Seguranca;

/// <summary>O painel Risco Cidadão: farol, índice de 0 a 100, a frase ao cidadão e os números por saída e por tipo de dado.</summary>
/// <param name="Farol">Verde (nenhum evento), Amarelo (dado sensível chegou e foi marcado) ou Vermelho (uma saída foi bloqueada).</param>
/// <param name="Indice">De 0 a 100: pontos (severidade × etapa) multiplicados por 4, no máximo 100.</param>
/// <param name="Resumo">A mensagem traduzida do evento mais grave e recente, ou a frase padrão do farol.</param>
/// <param name="JanelaEmHoras">Quantas horas para trás o painel olha.</param>
/// <param name="Totais">Quantos eventos houve na entrada e em cada saída.</param>
/// <param name="PorTipo">Quantos eventos citam cada tipo de dado (chaves em camelCase, como <c>cpf</c> e <c>dadoDeSaude</c>).</param>
/// <param name="UltimosEventos">Até 20, do mais recente para o mais antigo.</param>
public sealed record PainelDeSegurancaDto(
    Farol Farol,
    int Indice,
    string Resumo,
    int JanelaEmHoras,
    TotaisDoPainelDto Totais,
    IReadOnlyDictionary<string, int> PorTipo,
    IReadOnlyList<EventoDeSegurancaDto> UltimosEventos)
{
    public static PainelDeSegurancaDto De(ResultadoDoRisco risco) => new(
        risco.Farol,
        risco.Indice,
        risco.Resumo,
        risco.JanelaEmHoras,
        new TotaisDoPainelDto(risco.Totais.Entrada, risco.Totais.Banco, risco.Totais.Log, risco.Totais.RespostaHttp, risco.Totais.Llm),
        risco.PorTipo.ToDictionary(par => JsonNamingPolicy.CamelCase.ConvertName(par.Key.ToString()), par => par.Value),
        [.. risco.UltimosEventos.Select(EventoDeSegurancaDto.De)]);
}

/// <summary>Quantos eventos de cada ponto: <c>entrada</c> (dado marcado) e as quatro saídas barradas.</summary>
public sealed record TotaisDoPainelDto(int Entrada, int Banco, int Log, int RespostaHttp, int Llm);

/// <summary>Um evento de segurança. Só metadados: nenhum campo traz o dado pessoal.</summary>
/// <param name="Id">Cresce a cada evento (zera quando a API reinicia).</param>
/// <param name="OcorridoEm">Instante em UTC.</param>
/// <param name="Etapa">Entrada (o dado chegou e foi marcado) ou Saida (tentou sair e foi bloqueado).</param>
/// <param name="Sink">Por onde ia sair: Banco, Log, RespostaHttp ou Llm. Nulo na entrada.</param>
/// <param name="Acao">Marcado (entrada) ou Bloqueado (saída).</param>
/// <param name="Tipos">Os tipos de dado envolvidos.</param>
/// <param name="Onde">A rota (com ids como <c>{id}</c>), a categoria do log ou a propriedade do banco.</param>
/// <param name="Severidade">A maior severidade entre os tipos.</param>
/// <param name="DescricaoTecnica">A falha técnica, em linguagem de desenvolvedor.</param>
/// <param name="MensagemAoCidadao">A mesma falha em duas frases simples. Nula nos primeiros instantes, até a tradução chegar.</param>
/// <param name="TraduzidaPor"><c>gemini</c> ou <c>modelo padrão</c>.</param>
public sealed record EventoDeSegurancaDto(
    long Id,
    DateTimeOffset OcorridoEm,
    EtapaDoEvento Etapa,
    Sink? Sink,
    AcaoDeSeguranca Acao,
    IReadOnlyList<TipoDePii> Tipos,
    string Onde,
    Severidade Severidade,
    string DescricaoTecnica,
    string? MensagemAoCidadao,
    string? TraduzidaPor)
{
    public static EventoDeSegurancaDto De(EventoDeSeguranca e) => new(
        e.Id, e.OcorridoEm, e.Etapa, e.Sink, e.Acao, e.Tipos, e.Onde, e.Severidade, e.DescricaoTecnica, e.MensagemAoCidadao, e.TraduzidaPor);
}

using Elo.Application.Abstracoes;
using Elo.Domain.Chamadas;
using Elo.Domain.Painel;
using Elo.Domain.Ruas;

namespace Elo.Application.Painel;

/// <summary>A situação de cada rua ativa em uma chamada, já na ordem de prioridade.</summary>
public sealed record SituacoesDaChamada(
    IReadOnlyList<SituacaoDaRua> RuasEmOrdem,
    IReadOnlyList<Rua> RuasAtivas,
    IReadOnlyList<Resposta> Respostas);

/// <summary>
/// Busca os dados de uma chamada (ruas, respostas, ações e sensores) e aplica a regra de prioridade
/// do domínio. É usado pelo painel do coordenador e pela rota GET /situacao do app, sem repetir código.
/// </summary>
public sealed class MontadorDeSituacoes(
    IChamadaRepository chamadas,
    IRuaRepository ruas,
    ISensorRepository sensores,
    TimeProvider relogio)
{
    public async Task<SituacoesDaChamada> MontarAsync(Chamada chamada, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chamada);
        var agora = relogio.GetUtcNow().UtcDateTime;

        var ruasAtivas = await ruas.ListarPorBairroAsync(chamada.BairroId, null, null, incluirInativas: false, cancellationToken);
        var respostas = await chamadas.ListarRespostasAsync(chamada.Id, cancellationToken: cancellationToken);
        var acoes = await chamadas.ListarAcoesAsync(chamada.Id, cancellationToken);

        // Para uma chamada antiga, o sensor mostra a leitura daquela época, não a de agora.
        var referencia = chamada.FimDaJanela is DateTime fim && fim < agora ? fim : agora;
        var leituras = await sensores.ObterUltimaLeituraPorRuaAsync(chamada.BairroId, referencia, cancellationToken);

        // ToLookup agrupa uma vez só; depois cada rua pega as suas respostas e ações direto.
        var respostasPorRua = respostas.ToLookup(resposta => resposta.RuaId);
        var acoesPorRua = acoes.ToLookup(acao => acao.RuaId);

        var situacoes = ruasAtivas.Select(rua => SituacaoDaRua.Montar(
            rua,
            respostasPorRua[rua.Id],
            acoesPorRua[rua.Id],
            leituras.GetValueOrDefault(rua.Id)));

        return new SituacoesDaChamada(PriorizadorDeRuas.Ordenar(situacoes), ruasAtivas, respostas);
    }
}

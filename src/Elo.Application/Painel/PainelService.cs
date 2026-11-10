using Elo.Application.Abstracoes;
using Elo.Application.Chamadas;
using Elo.Application.Comum;
using Elo.Domain.Chamadas;
using Elo.Domain.Painel;

namespace Elo.Application.Painel;

public interface IPainelService
{
    /// <summary>Painel de uma chamada específica (aberta ou antiga).</summary>
    Task<PainelDto> ObterDaChamadaAsync(int chamadaId, CancellationToken cancellationToken = default);

    /// <summary>Painel da chamada aberta agora no bairro (tela "Bairro" do coordenador).</summary>
    Task<PainelDto> ObterAtualDoBairroAsync(int bairroId, CancellationToken cancellationToken = default);

    /// <summary>Indicador de impacto de todas as chamadas do bairro, com média por tipo.</summary>
    Task<IndicadoresDoBairroDto> ObterIndicadoresDoBairroAsync(int bairroId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Monta o painel do coordenador: busca os dados (EF Core) e aplica a regra de prioridade
/// do domínio (<see cref="PriorizadorDeRuas"/>, em LINQ).
/// </summary>
public sealed class PainelService(
    IChamadaRepository chamadas,
    IBairroRepository bairros,
    IRuaRepository ruas,
    ISensorRepository sensores,
    TimeProvider relogio) : IPainelService
{
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public async Task<PainelDto> ObterDaChamadaAsync(int chamadaId, CancellationToken cancellationToken = default)
    {
        var chamada = await chamadas.ObterPorIdAsync(chamadaId, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Chamada(chamadaId);

        return await MontarAsync(chamada, cancellationToken);
    }

    public async Task<PainelDto> ObterAtualDoBairroAsync(int bairroId, CancellationToken cancellationToken = default)
    {
        if (!await bairros.ExisteAsync(bairroId, cancellationToken))
        {
            throw RecursoNaoEncontradoException.Bairro(bairroId);
        }

        var chamada = await chamadas.ObterAbertaAsync(bairroId, Agora, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"O bairro {bairroId} não tem chamada aberta agora.");

        return await MontarAsync(chamada, cancellationToken);
    }

    public async Task<IndicadoresDoBairroDto> ObterIndicadoresDoBairroAsync(int bairroId, CancellationToken cancellationToken = default)
    {
        var bairro = await bairros.ObterPorIdAsync(bairroId, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Bairro(bairroId);

        var agora = Agora;
        var chamadasDoBairro = await chamadas.ListarPorBairroAsync(bairroId, cancellationToken);
        var ruasDoBairro = await ruas.ListarIdsAtivosAsync(bairroId, cancellationToken);

        var porChamada = new List<IndicadorDeChamadaDto>();
        foreach (var chamada in chamadasDoBairro)
        {
            // A chamada encerrada usa a foto guardada; a aberta é calculada na hora.
            var indicador = chamada.Resultado ?? IndicadorDaChamada.Calcular(
                chamada.IniciadaEm,
                ruasDoBairro,
                await chamadas.ListarRespostasAsync(chamada.Id, cancellationToken: cancellationToken));

            porChamada.Add(new IndicadorDeChamadaDto(
                chamada.Id,
                chamada.Tipo,
                chamada.IniciadaEm,
                chamada.StatusEm(agora),
                indicador.ParaDto()));
        }

        // LINQ: média do indicador por tipo de chamada (compara treino e chamada real).
        var mediaPorTipo = porChamada
            .GroupBy(item => item.Tipo)
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => new MediaPorTipoDto(
                grupo.Key,
                grupo.Count(),
                Math.Round(grupo.Average(item => item.Indicador.PercentualConhecidoEm15Min), 1),
                MediaOuNulo(grupo.Select(item => item.Indicador.MinutosAtePrimeiraRespostaEmMedia))))
            .ToList();

        return new IndicadoresDoBairroDto(bairro.Id, bairro.Nome, mediaPorTipo, porChamada);
    }

    private async Task<PainelDto> MontarAsync(Chamada chamada, CancellationToken cancellationToken)
    {
        var agora = Agora;
        var bairro = await bairros.ObterPorIdAsync(chamada.BairroId, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Bairro(chamada.BairroId);

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

        var ordenadas = PriorizadorDeRuas.Ordenar(situacoes);

        var indicador = chamada.Resultado
            ?? IndicadorDaChamada.Calcular(chamada.IniciadaEm, [.. ruasAtivas.Select(rua => rua.Id)], respostas);

        var resumo = new ResumoDoPainelDto(
            PrecisamDeAjuda: ordenadas.Count(s => s.Status == StatusNoPainel.PrecisaDeAjuda),
            EmSilencio: ordenadas.Count(s => s.Status == StatusNoPainel.EmSilencio),
            AguaSubindo: ordenadas.Count(s => s.Status == StatusNoPainel.AguaSubindo),
            TudoBem: ordenadas.Count(s => s.Status == StatusNoPainel.TudoBem),
            PessoasPrecisandoDeAjuda: ordenadas
                .Where(s => s.Status == StatusNoPainel.PrecisaDeAjuda)
                .Sum(s => s.UltimaResposta?.Pessoas ?? 0));

        return new PainelDto(
            bairro.Id,
            bairro.Nome,
            bairro.DadosSimulados,
            chamada.ParaDto(agora),
            agora,
            resumo,
            indicador.ParaDto(),
            [.. ordenadas.Select((situacao, indice) => situacao.ParaItemDto(indice + 1, chamada))]);
    }

    private static double? MediaOuNulo(IEnumerable<double?> valores)
    {
        var existentes = valores.OfType<double>().ToList();
        return existentes.Count == 0 ? null : Math.Round(existentes.Average(), 1);
    }
}

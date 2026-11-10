using System.Linq.Expressions;
using Elo.Application.Abstracoes;
using Elo.Application.Chamadas;
using Elo.Application.Comum;
using Elo.Domain.Chamadas;
using Elo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Elo.Infrastructure.Repositorios;

public sealed class ChamadaRepository(EloDbContext contexto) : RepositoryBase<Chamada>(contexto), IChamadaRepository
{
    public async Task<PaginaResultado<Chamada>> ListarAsync(
        FiltroDeChamadas filtro,
        DateTime agora,
        CancellationToken cancellationToken = default)
    {
        var consulta = Conjunto.AsNoTracking();

        if (filtro.BairroId is int bairroId)
        {
            consulta = consulta.Where(chamada => chamada.BairroId == bairroId);
        }

        // Filtro por subclasse: o EF Core traduz "is ChamadaReal" para a coluna Discriminador.
        consulta = filtro.Tipo switch
        {
            TipoChamada.Real => consulta.Where(chamada => chamada is ChamadaReal),
            TipoChamada.Treino => consulta.Where(chamada => chamada is ChamadaTreino),
            _ => consulta
        };

        consulta = filtro.Status switch
        {
            StatusChamada.Aberta => consulta.Where(FiltroAberta(agora)),
            StatusChamada.Encerrada => consulta.Where(FiltroEncerrada(agora)),
            _ => consulta
        };

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            .OrderByDescending(chamada => chamada.IniciadaEm)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .ToListAsync(cancellationToken);

        return new PaginaResultado<Chamada>(itens, filtro.Pagina, filtro.TamanhoPagina, total);
    }

    public async Task<IReadOnlyList<Chamada>> ListarPorBairroAsync(int bairroId, CancellationToken cancellationToken = default) =>
        await Conjunto
            .AsNoTracking()
            .Where(chamada => chamada.BairroId == bairroId)
            .OrderByDescending(chamada => chamada.IniciadaEm)
            .ToListAsync(cancellationToken);

    public Task<Chamada?> ObterAbertaAsync(int bairroId, DateTime agora, CancellationToken cancellationToken = default) =>
        Conjunto
            .Where(chamada => chamada.BairroId == bairroId)
            .Where(FiltroAberta(agora))
            .OrderByDescending(chamada => chamada.IniciadaEm)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Chamada?> ObterComRespostasAsync(int id, CancellationToken cancellationToken = default) =>
        Conjunto
            .Include(chamada => chamada.Respostas)
            .FirstOrDefaultAsync(chamada => chamada.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Chamada>> ListarNaoEncerradasAsync(int bairroId, CancellationToken cancellationToken = default) =>
        await Conjunto
            .Include(chamada => chamada.Respostas)
            .Where(chamada => chamada.BairroId == bairroId && chamada.EncerradaEm == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ChamadaTreino>> ListarTreinosVencidosAsync(DateTime agora, CancellationToken cancellationToken = default) =>
        await Conjunto
            .OfType<ChamadaTreino>()
            .Include(treino => treino.Respostas)
            .Where(treino => treino.EncerradaEm == null && treino.EncerramentoPrevistoEm <= agora)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Resposta>> ListarRespostasAsync(
        int chamadaId,
        SituacaoRua? situacao = null,
        int? ruaId = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = Contexto.Respostas.AsNoTracking().Where(resposta => resposta.ChamadaId == chamadaId);

        if (situacao is SituacaoRua situacaoFiltrada)
        {
            consulta = consulta.Where(resposta => resposta.Situacao == situacaoFiltrada);
        }

        if (ruaId is int ruaFiltrada)
        {
            consulta = consulta.Where(resposta => resposta.RuaId == ruaFiltrada);
        }

        return await consulta.OrderBy(resposta => resposta.RespondidaEm).ToListAsync(cancellationToken);
    }

    public Task<Resposta?> ObterRespostaAsync(int chamadaId, int respostaId, CancellationToken cancellationToken = default) =>
        Contexto.Respostas
            .AsNoTracking()
            .FirstOrDefaultAsync(resposta => resposta.ChamadaId == chamadaId && resposta.Id == respostaId, cancellationToken);

    public Task<Resposta?> ObterRespostaPorIdLocalAsync(string idLocal, CancellationToken cancellationToken = default) =>
        Contexto.Respostas
            .AsNoTracking()
            .FirstOrDefaultAsync(resposta => resposta.IdLocal == idLocal, cancellationToken);

    public Task<Chamada?> ObterDaJanelaAsync(int bairroId, DateTime momento, CancellationToken cancellationToken = default)
    {
        // A mesma regra de Chamada.AceitaRespostaDadaEm, escrita para o banco: começou antes
        // (com a tolerância do relógio do celular) e ainda não tinha terminado naquela hora.
        var inicioMaximo = momento + Chamada.ToleranciaDeRelogio;

        return Conjunto
            .Where(chamada => chamada.BairroId == bairroId
                && chamada.IniciadaEm <= inicioMaximo
                && (chamada.EncerradaEm == null || chamada.EncerradaEm >= momento)
                && (chamada.EncerramentoPrevistoEm == null || chamada.EncerramentoPrevistoEm >= momento))
            .OrderByDescending(chamada => chamada.IniciadaEm)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AcaoCoordenador>> ListarAcoesAsync(int chamadaId, CancellationToken cancellationToken = default) =>
        await Contexto.Acoes
            .AsNoTracking()
            .Where(acao => acao.ChamadaId == chamadaId)
            .OrderBy(acao => acao.RegistradaEm)
            .ToListAsync(cancellationToken);

    /// <summary>Exclusão em lote direto no banco (ExecuteDelete), sem carregar as respostas na memória.</summary>
    public Task<int> ApagarRespostasRecebidasAntesDeAsync(DateTime limite, CancellationToken cancellationToken = default) =>
        Contexto.Respostas
            .Where(resposta => resposta.RecebidaEm < limite)
            .ExecuteDeleteAsync(cancellationToken);

    // "Aberta" = sem encerramento manual e dentro do prazo (só o treino tem prazo).
    private static Expression<Func<Chamada, bool>> FiltroAberta(DateTime agora) =>
        chamada => chamada.EncerradaEm == null
            && (chamada.EncerramentoPrevistoEm == null || chamada.EncerramentoPrevistoEm > agora);

    private static Expression<Func<Chamada, bool>> FiltroEncerrada(DateTime agora) =>
        chamada => chamada.EncerradaEm != null
            || (chamada.EncerramentoPrevistoEm != null && chamada.EncerramentoPrevistoEm <= agora);
}

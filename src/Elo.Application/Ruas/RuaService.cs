using Elo.Application.Abstracoes;
using Elo.Application.Comum;
using Elo.Domain.Comum;
using Elo.Domain.Ruas;
using Microsoft.Extensions.Logging;

namespace Elo.Application.Ruas;

public sealed class RuaService(
    IRuaRepository ruas,
    IBairroRepository bairros,
    ISensorRepository sensores,
    IUnitOfWork unitOfWork,
    TimeProvider relogio,
    ILogger<RuaService> logger) : IRuaService
{
    public async Task<IReadOnlyList<RuaResumoDto>> ListarPorBairroAsync(
        int bairroId,
        string? microarea,
        NivelRisco? nivelRisco,
        bool incluirInativas,
        CancellationToken cancellationToken = default)
    {
        await GarantirQueOBairroExisteAsync(bairroId, cancellationToken);

        var lista = await ruas.ListarPorBairroAsync(bairroId, microarea, nivelRisco, incluirInativas, cancellationToken);
        return [.. lista.Select(rua => rua.ParaResumoDto())];
    }

    public async Task<RuaDetalheDto> ObterAsync(int id, CancellationToken cancellationToken = default)
    {
        var rua = await ObterRuaAsync(id, cancellationToken);
        var sensoresDaRua = await sensores.ListarComUltimaLeituraAsync(id, cancellationToken);
        return rua.ParaDetalheDto(sensoresDaRua);
    }

    public async Task<RuaDetalheDto> CriarAsync(int bairroId, RuaRequest request, CancellationToken cancellationToken = default)
    {
        await GarantirQueOBairroExisteAsync(bairroId, cancellationToken);
        await GarantirNomeLivreAsync(bairroId, request.Nome, ignorarId: null, cancellationToken);

        var rua = new Rua(
            bairroId,
            request.Nome,
            request.Microarea,
            request.NivelRisco ?? throw new RegraDeNegocioException("Informe o nível de risco da rua."),
            request.Lideranca?.ParaDominio());

        ruas.Adicionar(rua);
        await unitOfWork.SalvarAsync(cancellationToken);

        logger.LogInformation("Rua {RuaId} criada no bairro {BairroId}.", rua.Id, bairroId);
        return rua.ParaDetalheDto([]);
    }

    public async Task<RuaDetalheDto> AtualizarAsync(int id, RuaRequest request, CancellationToken cancellationToken = default)
    {
        var rua = await ObterRuaAsync(id, cancellationToken);
        await GarantirNomeLivreAsync(rua.BairroId, request.Nome, ignorarId: id, cancellationToken);

        rua.Atualizar(
            request.Nome,
            request.Microarea,
            request.NivelRisco ?? throw new RegraDeNegocioException("Informe o nível de risco da rua."),
            request.Lideranca?.ParaDominio(),
            request.Ativa);

        await unitOfWork.SalvarAsync(cancellationToken);
        return await ObterAsync(id, cancellationToken);
    }

    public async Task RemoverAsync(int id, CancellationToken cancellationToken = default)
    {
        var rua = await ObterRuaAsync(id, cancellationToken);

        if (await ruas.PossuiHistoricoAsync(id, cancellationToken))
        {
            throw new ConflitoException(
                "A rua já tem respostas ou ações registradas. Desative-a (ativa = false) em vez de excluir.");
        }

        ruas.Remover(rua);
        await unitOfWork.SalvarAsync(cancellationToken);
        logger.LogInformation("Rua {RuaId} excluída.", id);
    }

    public async Task<PlanoDaRuaDto> ObterPlanoAsync(int id, CancellationToken cancellationToken = default)
    {
        var rua = await ObterRuaAsync(id, cancellationToken);

        return rua.Plano?.ParaDto()
            ?? throw new RecursoNaoEncontradoException($"A rua {id} ainda não tem plano cadastrado.");
    }

    public async Task<PlanoDaRuaDto> DefinirPlanoAsync(int id, PlanoDaRuaRequest request, CancellationToken cancellationToken = default)
    {
        var rua = await ObterRuaAsync(id, cancellationToken);

        var plano = new PlanoDaRua(
            request.Rota,
            request.PontoDeEncontro,
            request.Abrigo,
            request.ItensParaLevar,
            relogio.GetUtcNow().UtcDateTime);

        rua.DefinirPlano(plano);
        await unitOfWork.SalvarAsync(cancellationToken);
        return plano.ParaDto();
    }

    /// <summary>Tela "Registro": as respostas da rua agrupadas por mês, do mais recente para o mais antigo.</summary>
    public async Task<IReadOnlyList<RegistroMensalDto>> ObterRegistroAsync(int id, CancellationToken cancellationToken = default)
    {
        await ObterRuaAsync(id, cancellationToken);
        var respostas = await ruas.ListarRespostasAsync(id, cancellationToken);

        return [.. respostas
            .GroupBy(resposta => new { resposta.RespondidaEm.Year, resposta.RespondidaEm.Month })
            .OrderByDescending(mes => mes.Key.Year)
            .ThenByDescending(mes => mes.Key.Month)
            .Select(mes => new RegistroMensalDto(
                Mes: $"{mes.Key.Year:D4}-{mes.Key.Month:D2}",
                TotalDeRespostas: mes.Count(),
                PedidosDeAjuda: mes.Count(resposta => resposta.PediuAjuda),
                Respostas: [.. mes.OrderByDescending(resposta => resposta.RespondidaEm).Select(resposta => resposta.ParaDto())]))];
    }

    private async Task<Rua> ObterRuaAsync(int id, CancellationToken cancellationToken) =>
        await ruas.ObterPorIdAsync(id, cancellationToken) ?? throw RecursoNaoEncontradoException.Rua(id);

    private async Task GarantirQueOBairroExisteAsync(int bairroId, CancellationToken cancellationToken)
    {
        if (!await bairros.ExisteAsync(bairroId, cancellationToken))
        {
            throw RecursoNaoEncontradoException.Bairro(bairroId);
        }
    }

    private async Task GarantirNomeLivreAsync(int bairroId, string nome, int? ignorarId, CancellationToken cancellationToken)
    {
        if (await ruas.ExisteComNomeAsync(bairroId, nome.Trim(), ignorarId, cancellationToken))
        {
            throw new ConflitoException($"Já existe a rua \"{nome.Trim()}\" neste bairro.");
        }
    }
}

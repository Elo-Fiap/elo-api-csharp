using Elo.Application.Abstracoes;
using Elo.Application.Comum;
using Elo.Domain.Chamadas;
using Elo.Domain.Comum;
using Elo.Domain.Ruas;
using Microsoft.Extensions.Logging;

namespace Elo.Application.Chamadas;

/// <summary>Respostas das ruas e ações do coordenador dentro de uma chamada.</summary>
public sealed class RespostaService(
    IChamadaRepository chamadas,
    IRuaRepository ruas,
    IUnitOfWork unitOfWork,
    TimeProvider relogio,
    ILogger<RespostaService> logger) : IRespostaService
{
    public async Task<ResultadoDeRegistro<RespostaDto>> RegistrarAsync(
        int chamadaId,
        RegistrarRespostaRequest request,
        CancellationToken cancellationToken = default)
    {
        // Reenvio offline: se o celular já mandou esta resposta, devolvemos a salva (idempotência).
        var idLocal = string.IsNullOrWhiteSpace(request.IdLocal) ? null : request.IdLocal.Trim();
        if (idLocal is not null)
        {
            var existente = await chamadas.ObterRespostaPorIdLocalAsync(idLocal, cancellationToken);
            if (existente is not null)
            {
                if (existente.ChamadaId != chamadaId)
                {
                    throw new ConflitoException("Este idLocal já foi usado em outra chamada.");
                }

                return new ResultadoDeRegistro<RespostaDto>(existente.ParaDto(), Criado: false);
            }
        }

        var chamada = await ObterChamadaAsync(chamadaId, cancellationToken);
        var rua = await ObterRuaAsync(request.RuaId, cancellationToken);
        var agora = relogio.GetUtcNow().UtcDateTime;

        var resposta = chamada.RegistrarResposta(
            idLocal,
            rua,
            request.Situacao ?? throw new RegraDeNegocioException("Informe a situação da rua."),
            request.Pessoas,
            request.Observacao,
            request.Canal,
            request.RespondidaEm?.UtcDateTime ?? agora,
            agora);

        await unitOfWork.SalvarAsync(cancellationToken);

        // A observação não vai para o log: ela pode ter dado sensível.
        logger.LogInformation(
            "Resposta {Situacao} da rua {RuaId} na chamada {ChamadaId} (canal {Canal}).",
            resposta.Situacao,
            rua.Id,
            chamadaId,
            resposta.Canal);

        return new ResultadoDeRegistro<RespostaDto>(resposta.ParaDto(), Criado: true);
    }

    public async Task<RespostaDto> ObterAsync(int chamadaId, int respostaId, CancellationToken cancellationToken = default)
    {
        var resposta = await chamadas.ObterRespostaAsync(chamadaId, respostaId, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Resposta(respostaId);

        return resposta.ParaDto();
    }

    public async Task<IReadOnlyList<RespostaDto>> ListarAsync(
        int chamadaId,
        SituacaoRua? situacao,
        int? ruaId,
        CancellationToken cancellationToken = default)
    {
        await GarantirQueAChamadaExisteAsync(chamadaId, cancellationToken);

        var respostas = await chamadas.ListarRespostasAsync(chamadaId, situacao, ruaId, cancellationToken);
        return [.. respostas.Select(resposta => resposta.ParaDto())];
    }

    public async Task<AcaoDto> RegistrarAcaoAsync(int chamadaId, RegistrarAcaoRequest request, CancellationToken cancellationToken = default)
    {
        var chamada = await ObterChamadaAsync(chamadaId, cancellationToken);
        var rua = await ObterRuaAsync(request.RuaId, cancellationToken);

        var acao = chamada.RegistrarAcao(
            rua,
            request.Tipo ?? throw new RegraDeNegocioException("Informe o tipo da ação."),
            relogio.GetUtcNow().UtcDateTime);

        await unitOfWork.SalvarAsync(cancellationToken);

        logger.LogInformation("Ação {Tipo} na rua {RuaId} (chamada {ChamadaId}).", acao.Tipo, rua.Id, chamadaId);
        return acao.ParaDto();
    }

    public async Task<IReadOnlyList<AcaoDto>> ListarAcoesAsync(int chamadaId, CancellationToken cancellationToken = default)
    {
        await GarantirQueAChamadaExisteAsync(chamadaId, cancellationToken);

        var acoes = await chamadas.ListarAcoesAsync(chamadaId, cancellationToken);
        return [.. acoes.Select(acao => acao.ParaDto())];
    }

    public async Task<AcaoDto> ObterAcaoAsync(int chamadaId, int acaoId, CancellationToken cancellationToken = default)
    {
        var acao = await chamadas.ObterAcaoAsync(chamadaId, acaoId, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Acao(acaoId);

        return acao.ParaDto();
    }

    private async Task<Chamada> ObterChamadaAsync(int chamadaId, CancellationToken cancellationToken) =>
        await chamadas.ObterPorIdAsync(chamadaId, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Chamada(chamadaId);

    private async Task<Rua> ObterRuaAsync(int? ruaId, CancellationToken cancellationToken)
    {
        var id = ruaId ?? throw new RegraDeNegocioException("Informe a rua.");
        return await ruas.ObterPorIdAsync(id, cancellationToken) ?? throw RecursoNaoEncontradoException.Rua(id);
    }

    private async Task GarantirQueAChamadaExisteAsync(int chamadaId, CancellationToken cancellationToken)
    {
        if (!await chamadas.ExisteAsync(chamadaId, cancellationToken))
        {
            throw RecursoNaoEncontradoException.Chamada(chamadaId);
        }
    }
}

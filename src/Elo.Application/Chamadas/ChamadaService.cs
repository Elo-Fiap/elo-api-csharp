using Elo.Application.Abstracoes;
using Elo.Application.Comum;
using Elo.Domain.Chamadas;
using Elo.Domain.Comum;
using Microsoft.Extensions.Logging;

namespace Elo.Application.Chamadas;

public sealed class ChamadaService(
    IChamadaRepository chamadas,
    IBairroRepository bairros,
    IRuaRepository ruas,
    ISensorRepository sensores,
    IUnitOfWork unitOfWork,
    TimeProvider relogio,
    ILogger<ChamadaService> logger) : IChamadaService
{
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public async Task<PaginaResultado<ChamadaDto>> ListarAsync(FiltroDeChamadas filtro, CancellationToken cancellationToken = default)
    {
        var agora = Agora;
        var pagina = await chamadas.ListarAsync(filtro, agora, cancellationToken);
        return pagina.Mapear(chamada => chamada.ParaDto(agora));
    }

    public async Task<ChamadaDto> ObterAsync(int id, CancellationToken cancellationToken = default)
    {
        var chamada = await chamadas.ObterPorIdAsync(id, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Chamada(id);

        return chamada.ParaDto(Agora);
    }

    public async Task<ChamadaDto> AbrirAsync(int bairroId, AbrirChamadaRequest request, CancellationToken cancellationToken = default)
    {
        if (!await bairros.ExisteAsync(bairroId, cancellationToken))
        {
            throw RecursoNaoEncontradoException.Bairro(bairroId);
        }

        var agora = Agora;
        await GarantirQueNaoHaChamadaAbertaAsync(bairroId, agora, cancellationToken);

        // Polimorfismo na criação: cada tipo valida o que precisa.
        Chamada chamada = request.Tipo switch
        {
            TipoChamada.Real => CriarChamadaReal(bairroId, request, agora),
            TipoChamada.Treino => CriarChamadaTreino(bairroId, request, agora),
            _ => throw new RegraDeNegocioException("Informe o tipo da chamada: Real ou Treino.")
        };

        chamadas.Adicionar(chamada);
        await unitOfWork.SalvarAsync(cancellationToken);

        logger.LogInformation("Chamada {ChamadaId} ({Tipo}) aberta no bairro {BairroId}.", chamada.Id, chamada.Tipo, bairroId);
        return chamada.ParaDto(agora);
    }

    public async Task<ChamadaDto> EncerrarAsync(int id, CancellationToken cancellationToken = default)
    {
        var chamada = await chamadas.ObterComRespostasAsync(id, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Chamada(id);

        var ruasDoBairro = await ruas.ListarIdsAtivosAsync(chamada.BairroId, cancellationToken);
        var agora = Agora;

        chamada.Encerrar(agora, ruasDoBairro);
        await unitOfWork.SalvarAsync(cancellationToken);

        logger.LogInformation(
            "Chamada {ChamadaId} encerrada: {Percentual}% das ruas conhecidas em 15 minutos.",
            id,
            chamada.Resultado?.PercentualConhecidoNaJanela);

        return chamada.ParaDto(agora);
    }

    public async Task<IReadOnlyList<EventoDoRegistroDto>> ObterRegistroAsync(int id, CancellationToken cancellationToken = default)
    {
        var chamada = await chamadas.ObterPorIdAsync(id, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Chamada(id);

        var agora = Agora;
        var ruasDoBairro = await ruas.ListarPorBairroAsync(chamada.BairroId, null, null, incluirInativas: true, cancellationToken);
        var respostas = await chamadas.ListarRespostasAsync(id, cancellationToken: cancellationToken);
        var acoes = await chamadas.ListarAcoesAsync(id, cancellationToken);

        var fim = chamada.FimDaJanela is DateTime fimDaJanela && fimDaJanela < agora ? fimDaJanela : agora;
        var alertas = await sensores.ListarAlertasAsync(chamada.BairroId, chamada.IniciadaEm, fim, cancellationToken);

        var nomesDasRuas = ruasDoBairro.ToDictionary(rua => rua.Id, rua => rua.Nome);
        return LinhaDoTempo.Montar(chamada, respostas, acoes, alertas, nomesDasRuas, agora);
    }

    private static ChamadaReal CriarChamadaReal(int bairroId, AbrirChamadaRequest request, DateTime agora)
    {
        if (request.DuracaoMinutos is not null)
        {
            throw new RegraDeNegocioException("Chamada real não tem duração: ela fica aberta até o coordenador encerrar.");
        }

        return new ChamadaReal(bairroId, request.Aviso?.ParaDominio(), agora);
    }

    private static ChamadaTreino CriarChamadaTreino(int bairroId, AbrirChamadaRequest request, DateTime agora)
    {
        if (request.Aviso is not null)
        {
            throw new RegraDeNegocioException("Treino não usa aviso oficial: ele é só um simulado.");
        }

        return new ChamadaTreino(bairroId, agora, request.DuracaoMinutos ?? ChamadaTreino.DuracaoPadraoMinutos);
    }

    /// <summary>
    /// Regra: só uma chamada aberta por bairro. Um treino que passou do prazo
    /// (mas ninguém encerrou) é encerrado aqui, guardando o indicador dele.
    /// </summary>
    private async Task GarantirQueNaoHaChamadaAbertaAsync(int bairroId, DateTime agora, CancellationToken cancellationToken)
    {
        var pendentes = await chamadas.ListarNaoEncerradasAsync(bairroId, cancellationToken);
        if (pendentes.Count == 0)
        {
            return;
        }

        var aberta = pendentes.FirstOrDefault(chamada => chamada.EstaAbertaEm(agora));
        if (aberta is not null)
        {
            throw new ConflitoException(
                $"O bairro já tem a chamada {aberta.Id} aberta. Encerre-a antes de abrir outra.");
        }

        var ruasDoBairro = await ruas.ListarIdsAtivosAsync(bairroId, cancellationToken);
        foreach (var vencida in pendentes)
        {
            vencida.Encerrar(agora, ruasDoBairro);
        }

        // Salva antes de abrir a nova, por causa do índice único "uma chamada aberta por bairro".
        await unitOfWork.SalvarAsync(cancellationToken);
    }
}

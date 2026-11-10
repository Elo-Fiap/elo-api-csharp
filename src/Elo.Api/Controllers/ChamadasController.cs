using Elo.Application.Chamadas;
using Elo.Application.Comum;
using Elo.Application.Painel;
using Elo.Domain.Chamadas;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Api.Controllers;

/// <summary>Chamadas (real ou treino), respostas das ruas, ações do coordenador, painel e registro.</summary>
[ApiController]
[Route("api/chamadas")]
[Produces("application/json")]
public sealed class ChamadasController(
    IChamadaService chamadas,
    IRespostaService respostas,
    IPainelService painel) : ControllerBase
{
    /// <summary>Lista as chamadas com filtros e paginação.</summary>
    [HttpGet]
    [ProducesResponseType<PaginaResultado<ChamadaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaResultado<ChamadaDto>>> Listar([FromQuery] FiltroDeChamadas filtro, CancellationToken cancellationToken) =>
        Ok(await chamadas.ListarAsync(filtro, cancellationToken));

    /// <summary>Detalha uma chamada.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ChamadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChamadaDto>> Obter(int id, CancellationToken cancellationToken) =>
        Ok(await chamadas.ObterAsync(id, cancellationToken));

    /// <summary>Abre uma chamada no bairro.</summary>
    /// <remarks>
    /// Tipo "Real" exige o aviso oficial (o Elo não cria alerta próprio).
    /// Tipo "Treino" é um simulado com prazo (padrão: 30 minutos).
    /// Só pode haver uma chamada aberta por bairro (senão: 409).
    /// </remarks>
    [HttpPost("/api/bairros/{bairroId:int}/chamadas")]
    [ProducesResponseType<ChamadaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ChamadaDto>> Abrir(int bairroId, AbrirChamadaRequest request, CancellationToken cancellationToken)
    {
        var chamada = await chamadas.AbrirAsync(bairroId, request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = chamada.Id }, chamada);
    }

    /// <summary>Encerra a chamada e guarda a foto do indicador.</summary>
    [HttpPost("{id:int}/encerramento")]
    [ProducesResponseType<ChamadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ChamadaDto>> Encerrar(int id, CancellationToken cancellationToken) =>
        Ok(await chamadas.EncerrarAsync(id, cancellationToken));

    /// <summary>Painel de uma chamada: as ruas na ordem de prioridade e o indicador de 15 minutos.</summary>
    [HttpGet("{id:int}/painel")]
    [ProducesResponseType<PainelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PainelDto>> Painel(int id, CancellationToken cancellationToken) =>
        Ok(await painel.ObterDaChamadaAsync(id, cancellationToken));

    /// <summary>Registro da chamada: linha do tempo do que foi avisado e do que foi feito.</summary>
    [HttpGet("{id:int}/registro")]
    [ProducesResponseType<IReadOnlyList<EventoDoRegistroDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EventoDoRegistroDto>>> Registro(int id, CancellationToken cancellationToken) =>
        Ok(await chamadas.ObterRegistroAsync(id, cancellationToken));

    /// <summary>Lista as respostas da chamada.</summary>
    /// <param name="id">Id da chamada.</param>
    /// <param name="situacao">Filtra por situação: TudoBem, AguaSubindo ou PrecisoDeAjuda.</param>
    /// <param name="ruaId">Filtra por rua.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("{id:int}/respostas")]
    [ProducesResponseType<IReadOnlyList<RespostaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RespostaDto>>> ListarRespostas(
        int id,
        [FromQuery] SituacaoRua? situacao,
        [FromQuery] int? ruaId,
        CancellationToken cancellationToken) =>
        Ok(await respostas.ListarAsync(id, situacao, ruaId, cancellationToken));

    /// <summary>Detalha uma resposta.</summary>
    [HttpGet("{id:int}/respostas/{respostaId:int}")]
    [ProducesResponseType<RespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespostaDto>> ObterResposta(int id, int respostaId, CancellationToken cancellationToken) =>
        Ok(await respostas.ObterAsync(id, respostaId, cancellationToken));

    /// <summary>Registra a resposta de uma rua (um toque).</summary>
    /// <remarks>
    /// Mande um "idLocal" gerado no celular. Se a mesma resposta for reenviada quando a internet voltar,
    /// a API devolve 200 com a resposta já salva, sem duplicar. Resposta nova devolve 201.
    /// O campo "pessoas" é opcional (de 1 a 500 quando vier). CPF, telefone e e-mail na observação são mascarados.
    /// </remarks>
    [HttpPost("{id:int}/respostas")]
    [ProducesResponseType<RespostaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<RespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RespostaDto>> RegistrarResposta(int id, RegistrarRespostaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await respostas.RegistrarAsync(id, request, cancellationToken);

        return resultado.Criado
            ? CreatedAtAction(nameof(ObterResposta), new { id, respostaId = resultado.Valor.Id }, resultado.Valor)
            : Ok(resultado.Valor);
    }

    /// <summary>Lista as ações do coordenador na chamada.</summary>
    [HttpGet("{id:int}/acoes")]
    [ProducesResponseType<IReadOnlyList<AcaoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AcaoDto>>> ListarAcoes(int id, CancellationToken cancellationToken) =>
        Ok(await respostas.ListarAcoesAsync(id, cancellationToken));

    /// <summary>Detalha uma ação do coordenador.</summary>
    [HttpGet("{id:int}/acoes/{acaoId:int}")]
    [ProducesResponseType<AcaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AcaoDto>> ObterAcao(int id, int acaoId, CancellationToken cancellationToken) =>
        Ok(await respostas.ObterAcaoAsync(id, acaoId, cancellationToken));

    /// <summary>Registra uma ação do coordenador: ContatoWhatsApp, Ligacao ou VouAteLa.</summary>
    /// <remarks>"VouAteLa" faz a rua descer na fila do painel: já tem alguém indo até lá.</remarks>
    [HttpPost("{id:int}/acoes")]
    [ProducesResponseType<AcaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AcaoDto>> RegistrarAcao(int id, RegistrarAcaoRequest request, CancellationToken cancellationToken)
    {
        var acao = await respostas.RegistrarAcaoAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(ObterAcao), new { id, acaoId = acao.Id }, acao);
    }
}

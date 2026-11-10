using Elo.Application.Ruas;
using Elo.Domain.Ruas;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Api.Controllers;

/// <summary>Ruas do bairro, com a liderança, o plano da rua e o registro de respostas.</summary>
[ApiController]
[Route("api/ruas")]
[Produces("application/json")]
public sealed class RuasController(IRuaService ruas) : ControllerBase
{
    /// <summary>Lista as ruas de um bairro.</summary>
    /// <param name="bairroId">Id do bairro.</param>
    /// <param name="microarea">Filtra por microárea. Ex.: Microárea 1.</param>
    /// <param name="nivelRisco">Filtra por nível de risco: Baixo, Medio ou Alto.</param>
    /// <param name="incluirInativas">Inclui as ruas desativadas.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("/api/bairros/{bairroId:int}/ruas")]
    [ProducesResponseType<IReadOnlyList<RuaResumoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RuaResumoDto>>> Listar(
        int bairroId,
        [FromQuery] string? microarea,
        [FromQuery] NivelRisco? nivelRisco,
        [FromQuery] bool incluirInativas = false,
        CancellationToken cancellationToken = default) =>
        Ok(await ruas.ListarPorBairroAsync(bairroId, microarea, nivelRisco, incluirInativas, cancellationToken));

    /// <summary>Cadastra uma rua no bairro.</summary>
    [HttpPost("/api/bairros/{bairroId:int}/ruas")]
    [ProducesResponseType<RuaDetalheDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RuaDetalheDto>> Criar(int bairroId, RuaRequest request, CancellationToken cancellationToken)
    {
        var rua = await ruas.CriarAsync(bairroId, request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = rua.Id }, rua);
    }

    /// <summary>Detalha uma rua, com plano e sensores (e a última leitura de cada um).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<RuaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuaDetalheDto>> Obter(int id, CancellationToken cancellationToken) =>
        Ok(await ruas.ObterAsync(id, cancellationToken));

    /// <summary>Atualiza uma rua. Use "ativa": false para tirá-la do painel sem perder o histórico.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<RuaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RuaDetalheDto>> Atualizar(int id, RuaRequest request, CancellationToken cancellationToken) =>
        Ok(await ruas.AtualizarAsync(id, request, cancellationToken));

    /// <summary>Exclui uma rua sem histórico (com histórico, desative-a).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(int id, CancellationToken cancellationToken)
    {
        await ruas.RemoverAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Plano da rua: rota, ponto de encontro, abrigo e o que levar.</summary>
    /// <remarks>O app guarda uma cópia para abrir sem internet; "atualizadoEm" diz se a cópia está velha.</remarks>
    [HttpGet("{id:int}/plano")]
    [ProducesResponseType<PlanoDaRuaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanoDaRuaDto>> ObterPlano(int id, CancellationToken cancellationToken) =>
        Ok(await ruas.ObterPlanoAsync(id, cancellationToken));

    /// <summary>Cria ou substitui o plano da rua.</summary>
    [HttpPut("{id:int}/plano")]
    [ProducesResponseType<PlanoDaRuaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PlanoDaRuaDto>> DefinirPlano(int id, PlanoDaRuaRequest request, CancellationToken cancellationToken) =>
        Ok(await ruas.DefinirPlanoAsync(id, request, cancellationToken));

    /// <summary>Registro da rua: as respostas agrupadas por mês (tela "Registro").</summary>
    [HttpGet("{id:int}/registro")]
    [ProducesResponseType<IReadOnlyList<RegistroMensalDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RegistroMensalDto>>> Registro(int id, CancellationToken cancellationToken) =>
        Ok(await ruas.ObterRegistroAsync(id, cancellationToken));
}

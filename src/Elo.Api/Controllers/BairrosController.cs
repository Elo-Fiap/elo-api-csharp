using Elo.Application.Bairros;
using Elo.Application.Painel;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Api.Controllers;

/// <summary>Bairros acompanhados pelo Elo, com o painel do coordenador e o indicador de impacto.</summary>
[ApiController]
[Route("api/bairros")]
[Produces("application/json")]
public sealed class BairrosController(IBairroService bairros, IPainelService painel) : ControllerBase
{
    /// <summary>Lista os bairros.</summary>
    /// <remarks>Traz o total de ruas ativas e o id da chamada aberta agora (se houver).</remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BairroResumoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BairroResumoDto>>> Listar(CancellationToken cancellationToken) =>
        Ok(await bairros.ListarAsync(cancellationToken));

    /// <summary>Detalha um bairro com as suas ruas.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<BairroDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BairroDetalheDto>> Obter(int id, CancellationToken cancellationToken) =>
        Ok(await bairros.ObterAsync(id, cancellationToken));

    /// <summary>Cadastra um bairro.</summary>
    [HttpPost]
    [ProducesResponseType<BairroDetalheDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BairroDetalheDto>> Criar(BairroRequest request, CancellationToken cancellationToken)
    {
        var bairro = await bairros.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = bairro.Id }, bairro);
    }

    /// <summary>Atualiza um bairro.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<BairroDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BairroDetalheDto>> Atualizar(int id, BairroRequest request, CancellationToken cancellationToken) =>
        Ok(await bairros.AtualizarAsync(id, request, cancellationToken));

    /// <summary>Exclui um bairro que ainda não tem chamadas.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(int id, CancellationToken cancellationToken)
    {
        await bairros.RemoverAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Painel da chamada aberta agora no bairro (tela "Bairro" do coordenador).</summary>
    /// <remarks>
    /// Ordem (feita com LINQ): primeiro quem pediu ajuda, depois quem está em silêncio
    /// (sensor acima do limite e ruas de maior risco na frente), depois água subindo e, por fim, tudo bem.
    /// Traz também o indicador: percentual de ruas com situação conhecida em 15 minutos.
    /// </remarks>
    [HttpGet("{id:int}/painel")]
    [ProducesResponseType<PainelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PainelDto>> Painel(int id, CancellationToken cancellationToken) =>
        Ok(await painel.ObterAtualDoBairroAsync(id, cancellationToken));

    /// <summary>Indicador de impacto de todas as chamadas do bairro, com a média por tipo (Real x Treino).</summary>
    [HttpGet("{id:int}/indicadores")]
    [ProducesResponseType<IndicadoresDoBairroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IndicadoresDoBairroDto>> Indicadores(int id, CancellationToken cancellationToken) =>
        Ok(await painel.ObterIndicadoresDoBairroAsync(id, cancellationToken));
}

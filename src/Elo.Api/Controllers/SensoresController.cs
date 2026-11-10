using Elo.Application.Sensores;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Api.Controllers;

/// <summary>Sensores ESP32 (HC-SR04 + buzzer) que medem o nível da água no ponto crítico da rua.</summary>
[ApiController]
[Route("api/sensores")]
[Produces("application/json")]
public sealed class SensoresController(ISensorService sensores) : ControllerBase
{
    /// <summary>Lista os sensores, com a última leitura de cada um.</summary>
    /// <param name="ruaId">Filtra pelos sensores de uma rua.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SensorDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SensorDto>>> Listar([FromQuery] int? ruaId, CancellationToken cancellationToken) =>
        Ok(await sensores.ListarAsync(ruaId, cancellationToken));

    /// <summary>Detalha um sensor.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<SensorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SensorDto>> Obter(int id, CancellationToken cancellationToken) =>
        Ok(await sensores.ObterAsync(id, cancellationToken));

    /// <summary>Cadastra um sensor em uma rua.</summary>
    [HttpPost]
    [ProducesResponseType<SensorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SensorDto>> Criar(CriarSensorRequest request, CancellationToken cancellationToken)
    {
        var sensor = await sensores.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = sensor.Id }, sensor);
    }

    /// <summary>Atualiza local, limite de alerta ou situação (ativo) de um sensor.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<SensorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SensorDto>> Atualizar(int id, AtualizarSensorRequest request, CancellationToken cancellationToken) =>
        Ok(await sensores.AtualizarAsync(id, request, cancellationToken));

    /// <summary>Exclui um sensor e as suas leituras.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(int id, CancellationToken cancellationToken)
    {
        await sensores.RemoverAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Recebe uma leitura do ESP32.</summary>
    /// <remarks>
    /// Exemplo de corpo: { "nivelCm": 42.5 }. A API marca "acimaDoLimite" comparando com o limite do sensor.
    /// O buzzer toca no próprio ESP32, mesmo sem internet; aqui a leitura entra no painel e no registro.
    /// </remarks>
    [HttpPost("{id:int}/leituras")]
    [ProducesResponseType<LeituraDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LeituraDto>> RegistrarLeitura(int id, RegistrarLeituraRequest request, CancellationToken cancellationToken)
    {
        var leitura = await sensores.RegistrarLeituraAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(ListarLeituras), new { id }, leitura);
    }

    /// <summary>Últimas leituras do sensor (da mais nova para a mais antiga).</summary>
    /// <param name="id">Id do sensor.</param>
    /// <param name="quantidade">Quantas leituras trazer (1 a 500, padrão 50).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("{id:int}/leituras")]
    [ProducesResponseType<IReadOnlyList<LeituraDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LeituraDto>>> ListarLeituras(
        int id,
        [FromQuery] int quantidade = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await sensores.ListarLeiturasAsync(id, quantidade, cancellationToken));
}

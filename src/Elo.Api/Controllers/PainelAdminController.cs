using System.ComponentModel.DataAnnotations;
using Elo.Application.Comum;
using Elo.Application.PainelAdmin;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Api.Controllers;

/// <summary>Painel de administração: uma leitura só com tudo o que a tela precisa.</summary>
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
public sealed class PainelAdminController(IPainelAdminService painelAdmin) : ControllerBase
{
    /// <summary>Instantâneo do painel: bairros, ruas, réguas, chamadas e avisos do INMET.</summary>
    /// <remarks>
    /// Traz todos os bairros com todas as ruas (ativas e inativas, com o traçado no mapa), as réguas com as leituras
    /// das últimas 72 horas (no máximo 500 por régua, da mais antiga para a mais nova, em metros), as chamadas abertas
    /// agora ou abertas dentro da janela de <c>dias</c> (com todas as respostas e ações) e os avisos ativos do INMET
    /// para o município de cada bairro. Se o INMET estiver fora do ar, <c>avisos</c> vem vazio (esta rota nunca devolve 503).
    /// Datas em UTC (ISO 8601). O formato completo está em <c>docs/contrato-painel.md</c>.
    /// </remarks>
    /// <param name="dias">Janela das chamadas, de 1 a 30 dias (padrão 30).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("instantaneo")]
    [ProducesResponseType<InstantaneoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InstantaneoDto>> Instantaneo(
        [FromQuery, Range(PainelAdminService.DiasMinimos, PainelAdminService.DiasMaximos, ErrorMessage = Mensagens.Intervalo)] int dias = PainelAdminService.DiasMaximos,
        CancellationToken cancellationToken = default) =>
        Ok(await painelAdmin.ObterInstantaneoAsync(dias, cancellationToken));
}

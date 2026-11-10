using System.ComponentModel.DataAnnotations;
using Elo.Api.Configuracao;
using Elo.Application.Avisos;
using Elo.Application.Comum;
using Elo.Application.ContratoDoApp;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Api.Controllers;

/// <summary>
/// Rotas do contrato do app (docs/contrato-api.md do repositório elo-app), sem o prefixo /api.
/// O app chama EXPO_PUBLIC_API_URL + "/situacao", "/chamadas", "/regua", "/ruas/{id}/plano" e "/avisos";
/// o ESP32 chama "/leituras".
/// </summary>
[ApiController]
[Produces("application/json")]
[Tags("Contrato do app (elo-app e ESP32)")]
public sealed class ContratoDoAppController(IContratoDoAppService contrato, IAvisoService avisos) : ControllerBase
{
    /// <summary>Avisos do INMET que valem para o município, já filtrados e limpos.</summary>
    /// <remarks>
    /// O INMET devolve o Brasil inteiro, com polígonos e ícones (centenas de KB). A API baixa a cada poucos minutos,
    /// guarda em cache e devolve só os avisos do município (algumas centenas de bytes), do mais grave ao mais leve.
    /// Os campos são os do tipo Aviso do app: id, titulo, severidade, gravidade, inicio, fim, quando, riscos e instrucoes.
    /// </remarks>
    /// <param name="codigoIbge">Código IBGE do município, com 7 dígitos. Ex.: 3550308 (São Paulo).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("/avisos")]
    [ProducesResponseType<IReadOnlyList<AvisoDoAppDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<AvisoDoAppDto>>> Avisos(
        [FromQuery]
        [Required(ErrorMessage = Mensagens.Obrigatorio)]
        [RegularExpression("^[0-9]{7}$", ErrorMessage = "Informe o código IBGE do município com 7 dígitos. Ex.: 3550308.")]
        string? codigoIbge,
        CancellationToken cancellationToken) =>
        Ok(await avisos.ListarDoMunicipioAsync(codigoIbge!, cancellationToken));

    /// <summary>Situação do bairro na chamada atual (tela "Bairro" do app).</summary>
    /// <remarks>
    /// "status" é tudo_bem, agua_subindo ou preciso_ajuda; nulo = silêncio. As ruas vêm na ordem do painel
    /// (quem pediu ajuda, depois quem está em silêncio). Sem chamada aberta, "chamadaIniciadaEm" é nulo.
    /// </remarks>
    /// <param name="bairroId">Opcional. Sem ele, vale o bairro configurado em ContratoDoApp:BairroId.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("/situacao")]
    [ProducesResponseType<SituacaoDoBairroApp>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SituacaoDoBairroApp>> Situacao([FromQuery] int? bairroId, CancellationToken cancellationToken) =>
        Ok(await contrato.ObterSituacaoAsync(bairroId, cancellationToken));

    /// <summary>Recebe o que a liderança respondeu no app (a "chamada" do app).</summary>
    /// <remarks>
    /// O "id" é criado no app: o mesmo id enviado de novo (internet voltou) devolve 200 com o registro que já existia.
    /// A resposta entra na chamada que estava aberta no bairro na hora do "criadaEm"; sem chamada, 422.
    /// </remarks>
    [HttpPost("/chamadas")]
    [ProducesResponseType<ChamadaDoAppDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ChamadaDoAppDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ChamadaDoAppDto>> RegistrarChamada(ChamadaDoAppRequest request, CancellationToken cancellationToken)
    {
        var resultado = await contrato.RegistrarChamadaAsync(request, cancellationToken);

        // O app sempre lê o corpo de um 200/201 como JSON, por isso a resposta nunca vem vazia.
        return resultado.Criado
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : Ok(resultado.Valor);
    }

    /// <summary>Última leitura da régua de nível, em metros (o app mostra "acima do limite" quando o nível chega ao limite).</summary>
    /// <param name="reguaId">Opcional. Sem ele, vale a régua configurada em ContratoDoApp:ReguaPadrao.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("/regua")]
    [ProducesResponseType<LeituraDaReguaApp>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeituraDaReguaApp>> Regua([FromQuery] string? reguaId, CancellationToken cancellationToken) =>
        Ok(await contrato.ObterUltimaLeituraAsync(reguaId, cancellationToken));

    /// <summary>Recebe a leitura do ESP32 (régua de nível).</summary>
    /// <remarks>Exige o header X-Chave-Dispositivo. Resposta de sucesso: 204, sem corpo, como a régua espera.</remarks>
    /// <param name="chaveDoDispositivo">Chave do ESP32 (header X-Chave-Dispositivo), conferida pelo filtro antes de tudo.</param>
    /// <param name="request">Leitura enviada pelo ESP32.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpPost("/leituras")]
    [ExigeChaveDeDispositivo]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReceberLeitura(
        [FromHeader(Name = ExigeChaveDeDispositivoAttribute.Cabecalho)] string? chaveDoDispositivo,
        LeituraDoEsp32Request request,
        CancellationToken cancellationToken)
    {
        await contrato.RegistrarLeituraAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Plano de saída da rua (ponto de encontro, rota, abrigo, o que levar e contatos de emergência).</summary>
    /// <remarks>404 quando a rua não tem plano: o app mostra o estado vazio.</remarks>
    [HttpGet("/ruas/{id}/plano")]
    [ProducesResponseType<PlanoDaRuaApp>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanoDaRuaApp>> Plano(string id, CancellationToken cancellationToken) =>
        Ok(await contrato.ObterPlanoAsync(id, cancellationToken));
}

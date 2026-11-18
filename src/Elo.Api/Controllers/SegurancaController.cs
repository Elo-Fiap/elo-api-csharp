using System.ComponentModel.DataAnnotations;
using Elo.Api.Seguranca;
using Elo.Application.Comum;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Risco;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Api.Controllers;

/// <summary>Módulo de Cybersecurity: o painel Risco Cidadão e os eventos de segurança por trás dele.</summary>
[ApiController]
[Route("api/seguranca")]
[Produces("application/json")]
public sealed class SegurancaController(CalculadoraDeRiscoCidadao calculadora, IRegistroDeEventos registro) : ControllerBase
{
    /// <summary>O painel Risco Cidadão: farol, índice de 0 a 100 e a mensagem ao cidadão.</summary>
    /// <remarks>
    /// Olha as últimas 24 horas. O farol é <c>Verde</c> sem nenhum evento, <c>Amarelo</c> quando só chegou dado sensível
    /// (e foi marcado, nada tentou sair) e <c>Vermelho</c> quando pelo menos uma saída (log, resposta HTTP, banco ou IA) foi bloqueada.
    /// O índice é a soma dos pontos de cada evento (severidade: média 2, alta 3; etapa: entrada 1, saída 3) vezes 4, no máximo 100.
    /// A mensagem ao cidadão é escrita em segundo plano (Gemini, ou mensagens prontas sem chave), então um evento novo
    /// aparece com <c>mensagemAoCidadao</c> nula por alguns instantes. Os eventos ficam só em memória e zeram quando a API reinicia.
    /// Nenhum campo traz o dado pessoal: só tipo, quantidade e lugar.
    /// </remarks>
    [HttpGet("painel")]
    [ProducesResponseType<PainelDeSegurancaDto>(StatusCodes.Status200OK)]
    public ActionResult<PainelDeSegurancaDto> Painel() =>
        Ok(PainelDeSegurancaDto.De(calculadora.Calcular()));

    /// <summary>Os eventos de segurança mais recentes, do mais novo para o mais antigo.</summary>
    /// <remarks>Só metadados (nunca o dado pessoal). A API guarda no máximo os últimos 500 eventos, em memória.</remarks>
    /// <param name="limite">Quantos eventos devolver, de 1 a 500 (padrão 50).</param>
    [HttpGet("eventos")]
    [ProducesResponseType<IReadOnlyList<EventoDeSegurancaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<EventoDeSegurancaDto>> Eventos(
        [FromQuery, Range(1, RegistroDeEventosEmMemoria.Capacidade, ErrorMessage = Mensagens.Intervalo)] int limite = 50) =>
        Ok(registro.Listar(null, limite).Select(EventoDeSegurancaDto.De).ToList());
}

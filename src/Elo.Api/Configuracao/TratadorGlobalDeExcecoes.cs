using Elo.Application.Comum;
using Elo.Domain.Comum;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Elo.Api.Configuracao;

/// <summary>
/// Converte as exceções em respostas HTTP padronizadas (ProblemDetails).
/// Os controllers ficam limpos: não precisam de try/catch.
/// </summary>
public sealed class TratadorGlobalDeExcecoes(
    IProblemDetailsService problemDetails,
    ILogger<TratadorGlobalDeExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // O cliente desistiu da requisição: não há para quem responder.
            return true;
        }

        // Pattern matching: cada tipo de exceção vira um status HTTP.
        var (status, titulo, detalhe) = exception switch
        {
            RecursoNaoEncontradoException => (StatusCodes.Status404NotFound, "Recurso não encontrado", exception.Message),
            ConflitoException => (StatusCodes.Status409Conflict, "Conflito", exception.Message),
            RegraDeNegocioException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", exception.Message),
            DbUpdateException => (StatusCodes.Status409Conflict, "Conflito ao salvar",
                "Os dados conflitam com um registro existente (ex.: nome ou código repetido)."),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado. Tente novamente.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro não tratado em {Metodo} {Caminho}.", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Requisição recusada com {Status}: {Mensagem}", status, exception.Message);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = detalhe,
                Instance = httpContext.Request.Path
            }
        });
    }
}

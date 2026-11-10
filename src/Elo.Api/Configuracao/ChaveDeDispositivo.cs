using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Elo.Api.Configuracao;

/// <summary>Configuração da chave dos dispositivos (seção "Dispositivos").</summary>
public sealed class DispositivosOptions
{
    public const string Secao = "Dispositivos";

    /// <summary>
    /// Chave que o ESP32 manda no header X-Chave-Dispositivo. Fica fora do repositório, como pede o contrato:
    /// variável de ambiente <c>Dispositivos__Chave</c> ou <c>dotnet user-secrets</c>.
    /// </summary>
    public string? Chave { get; set; }
}

/// <summary>
/// Confere o header X-Chave-Dispositivo antes de a requisição chegar ao controller (filtro de autorização).
/// Sem chave configurada, só o ambiente de desenvolvimento aceita, para dar para testar no Swagger.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExigeChaveDeDispositivoAttribute : Attribute, IAuthorizationFilter
{
    public const string Cabecalho = "X-Chave-Dispositivo";

    private static bool _avisouSemChave;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var servicos = context.HttpContext.RequestServices;
        var chaveEsperada = servicos.GetRequiredService<IOptions<DispositivosOptions>>().Value.Chave;

        if (string.IsNullOrWhiteSpace(chaveEsperada))
        {
            if (servicos.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                AvisarUmaVez(servicos);
                return;
            }

            context.Result = NaoAutorizado(context.HttpContext, "A chave dos dispositivos não está configurada no servidor.");
            return;
        }

        var recebida = context.HttpContext.Request.Headers[Cabecalho].ToString();
        if (!Iguais(recebida, chaveEsperada))
        {
            context.Result = NaoAutorizado(context.HttpContext, $"Envie a chave do dispositivo no header {Cabecalho}.");
        }
    }

    // Comparação em tempo constante: o tempo de resposta não dá pistas da chave.
    private static bool Iguais(string recebida, string esperada) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recebida), Encoding.UTF8.GetBytes(esperada));

    private static ObjectResult NaoAutorizado(HttpContext http, string detalhe) =>
        new(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Não autorizado",
            Detail = detalhe,
            Instance = http.Request.Path
        })
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentTypes = { "application/problem+json" }
        };

    private static void AvisarUmaVez(IServiceProvider servicos)
    {
        if (_avisouSemChave)
        {
            return;
        }

        _avisouSemChave = true;
        servicos.GetRequiredService<ILoggerFactory>()
            .CreateLogger<ExigeChaveDeDispositivoAttribute>()
            .LogWarning("Dispositivos:Chave não configurada: em desenvolvimento, POST /leituras aceita sem chave.");
    }
}

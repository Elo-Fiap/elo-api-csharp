using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Taint;

namespace Elo.Api.Seguranca;

/// <summary>
/// Middleware de SAÍDA (sink "RespostaHttp"): segura o corpo da resposta, passa o JSON pela <see cref="GuardaDeSaida"/>
/// e só então entrega. A API não tem login de usuário: toda resposta é tratada como não autenticada.
/// Se nada mudou, os bytes originais seguem intactos (não re-serializa à toa: a forma do JSON dos contratos não pode mudar).
/// Fica ANTES do tratador de exceções no pipeline para que o ProblemDetails de um erro também passe por aqui.
/// </summary>
public sealed class GuardaDeRespostaHttp(RequestDelegate proximo, GuardaDeSaida guarda)
{
    private static readonly string[] Prefixos = ["/openapi", "/swagger", "/scalar", "/health"];

    private static readonly JsonSerializerOptions Escrita = new()
    {
        // Sem isso o System.Text.Json escreve "ã" como ã.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task InvokeAsync(HttpContext http)
    {
        if (DeveIgnorar(http.Request.Path))
        {
            await proximo(http);
            return;
        }

        var corpoReal = http.Response.Body;
        using var retido = new MemoryStream();
        http.Response.Body = retido;

        try
        {
            await proximo(http);
        }
        finally
        {
            http.Response.Body = corpoReal;
        }

        var bytes = retido.GetBuffer().AsMemory(0, (int)retido.Length);
        if (bytes.Length > 0 && EhJson(http.Response.ContentType))
        {
            var limpo = Limpar(bytes, http);
            if (limpo is not null)
            {
                http.Response.ContentLength = limpo.Length;
                await corpoReal.WriteAsync(limpo, http.RequestAborted);
                return;
            }
        }

        if (bytes.Length > 0)
        {
            await corpoReal.WriteAsync(bytes, http.RequestAborted);
        }
    }

    /// <summary>Os bytes do JSON limpo, ou nulo se não mudou nada (ou se o corpo não é um JSON válido).</summary>
    private byte[]? Limpar(ReadOnlyMemory<byte> bytes, HttpContext http)
    {
        JsonNode? raiz;
        try
        {
            raiz = JsonNode.Parse(bytes.Span);
        }
        catch (JsonException)
        {
            return null;
        }

        if (raiz is null)
        {
            return null;
        }

        var onde = NormalizadorDeRota.Normalizar(http.Request.Method, http.Request.Path);
        var resultado = guarda.VerificarJson(raiz, Sink.RespostaHttp, onde);
        return resultado is null ? null : JsonSerializer.SerializeToUtf8Bytes(resultado, Escrita);
    }

    private static bool EhJson(string? tipo) =>
        tipo is not null
        && (tipo.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            || tipo.StartsWith("application/problem+json", StringComparison.OrdinalIgnoreCase));

    /// <summary>Documentação, health check e arquivos estáticos (qualquer caminho com extensão) não são respostas da API.</summary>
    private static bool DeveIgnorar(PathString caminho) =>
        Prefixos.Any(p => caminho.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase))
        || Path.HasExtension(caminho.Value);
}

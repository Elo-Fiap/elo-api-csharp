using Scalar.AspNetCore;

namespace Elo.Api.Configuracao;

/// <summary>
/// Documentação dos endpoints: o documento OpenAPI é gerado pelo próprio ASP.NET Core
/// e exibido em duas interfaces, Swagger UI (/swagger) e Scalar (/scalar).
/// </summary>
public static class DocumentacaoDaApi
{
    private const string Descricao = """
        API do **Elo**, a CitizenTech da equipe na Global Solution 2026.2.

        Durante a chuva forte, a liderança de cada rua responde em um toque (*Tudo bem*, *Água subindo* ou
        *Preciso de ajuda*) e o coordenador do bairro vê primeiro quem pediu ajuda e depois quem ficou em silêncio.

        **Os dados de exemplo são simulados** (Jardim Pantanal, ruas, apelidos e telefones fictícios).

        Roteiro rápido para testar:
        1. `GET /api/bairros` para ver o bairro e a chamada aberta;
        2. `GET /api/bairros/1/painel` para ver as ruas na ordem de prioridade;
        3. `POST /api/chamadas/{id}/respostas` para uma rua responder;
        4. `GET /api/chamadas/{id}/registro` para ver o que foi avisado e feito;
        5. `POST /api/sensores/1/leituras` para simular o ESP32.
        """;

    public static IServiceCollection AddDocumentacaoDaApi(this IServiceCollection services)
    {
        services.AddOpenApi(opcoes =>
        {
            opcoes.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info.Title = "Elo API";
                documento.Info.Version = "v1";
                documento.Info.Description = Descricao;
                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static WebApplication UseDocumentacaoDaApi(this WebApplication app)
    {
        app.MapOpenApi(); // /openapi/v1.json

        app.UseSwaggerUI(opcoes =>
        {
            opcoes.SwaggerEndpoint("/openapi/v1.json", "Elo API v1");
            opcoes.DocumentTitle = "Elo API - Swagger";
        });

        app.MapScalarApiReference(opcoes => opcoes.WithTitle("Elo API - Scalar"));

        return app;
    }
}

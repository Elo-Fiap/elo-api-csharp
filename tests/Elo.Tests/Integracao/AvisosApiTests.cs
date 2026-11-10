using System.Net;
using System.Text.Json;
using Elo.Application.Avisos;
using Elo.Application.Comum;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Tests.Integracao;

/// <summary>GET /avisos com a fonte falsa (o fixture real do INMET), sem acessar a rede.</summary>
public class AvisosApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Avisos_do_municipio_vem_com_os_campos_do_tipo_Aviso_do_app()
    {
        var resposta = await _cliente.GetAsync("/avisos?codigoIbge=3550308");
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var avisos = json.RootElement.EnumerateArray().ToList();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(["56273", "56272", "56278"], avisos.Select(aviso => aviso.GetProperty("id").GetString()));
        Assert.Equal(
            ["fim", "gravidade", "id", "inicio", "instrucoes", "quando", "riscos", "severidade", "titulo"],
            avisos[0].EnumerateObject().Select(campo => campo.Name).Order());
        Assert.Equal("2026-11-05T00:01:00-03:00", avisos[0].GetProperty("inicio").GetString());
        Assert.Equal(JsonValueKind.Array, avisos[0].GetProperty("riscos").ValueKind);
        Assert.Equal("futuro", avisos[2].GetProperty("quando").GetString());
    }

    [Fact]
    public async Task Municipio_sem_aviso_devolve_lista_vazia()
    {
        var resposta = await _cliente.GetAsync("/avisos?codigoIbge=1100015");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("[]", await resposta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/avisos?codigoIbge=12345")]
    [InlineData("/avisos?codigoIbge=abcdefg")]
    [InlineData("/avisos")]
    public async Task Codigo_IBGE_invalido_devolve_400(string caminho)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.GetAsync(caminho)).StatusCode);
    }
}

/// <summary>Uma fábrica em que o INMET está fora do ar e não há nada guardado.</summary>
public sealed class EloApiFactorySemInmet : EloApiFactory
{
    protected override IFonteDeAvisos CriarFonteDeAvisos() =>
        new FonteDeAvisosFalsa(() => throw new FonteIndisponivelException("Os avisos do INMET estão indisponíveis no momento."));
}

public class AvisosSemInmetApiTests(EloApiFactorySemInmet fabrica) : IClassFixture<EloApiFactorySemInmet>
{
    [Fact]
    public async Task INMET_fora_do_ar_e_sem_lista_guardada_devolve_503()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/avisos?codigoIbge=3550308");
        var problema = await resposta.LerAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Contains("INMET", problema.Detail);
    }
}

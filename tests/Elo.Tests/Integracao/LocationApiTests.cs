using System.Net;
using Elo.Application.Chamadas;
using Elo.Application.Sensores;
using Elo.Domain.Chamadas;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Tests.Integracao;

/// <summary>Todo 201 diz, no Location, onde está o recurso criado, e esse endereço responde.</summary>
public class LocationApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Location_da_acao_criada_leva_ao_detalhe_dela()
    {
        var criada = await _cliente.EnviarAsync("/api/chamadas/2/acoes", new RegistrarAcaoRequest { RuaId = 5, Tipo = TipoAcao.Ligacao });
        var acao = await criada.LerAsync<AcaoDto>();

        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        Assert.Equal($"/api/chamadas/2/acoes/{acao.Id}", criada.Headers.Location!.AbsolutePath);

        var detalhe = await (await _cliente.GetAsync(criada.Headers.Location)).LerAsync<AcaoDto>();
        Assert.Equal((acao.Id, 5, TipoAcao.Ligacao), (detalhe.Id, detalhe.RuaId, detalhe.Tipo));

        // A ação é da chamada 2: pedida dentro de outra chamada, não existe.
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync($"/api/chamadas/1/acoes/{acao.Id}")).StatusCode);
    }

    [Fact]
    public async Task Location_da_leitura_criada_leva_ao_detalhe_dela()
    {
        var criada = await _cliente.EnviarAsync("/api/sensores/2/leituras", new RegistrarLeituraRequest { NivelCm = 90 });
        var leitura = await criada.LerAsync<LeituraDto>();

        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        Assert.Equal($"/api/sensores/2/leituras/{leitura.Id}", criada.Headers.Location!.AbsolutePath);

        var detalhe = await (await _cliente.GetAsync(criada.Headers.Location)).LerAsync<LeituraDto>();
        Assert.Equal((leitura.Id, 90.0, false), (detalhe.Id, detalhe.NivelCm, detalhe.AcimaDoLimite));

        // A leitura é do sensor 2: pedida em outro sensor, não existe.
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync($"/api/sensores/1/leituras/{leitura.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("/api/chamadas/2/acoes/999", "Ação 999 não encontrada.")]
    [InlineData("/api/sensores/2/leituras/999", "Leitura 999 não encontrada.")]
    public async Task Detalhe_inexistente_devolve_404_em_problem_details(string caminho, string detalhe)
    {
        var resposta = await _cliente.GetAsync(caminho);
        var problema = await resposta.LerAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal(detalhe, problema.Detail);
    }
}

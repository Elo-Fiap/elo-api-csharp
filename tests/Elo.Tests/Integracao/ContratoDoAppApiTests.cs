using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Elo.Application.ContratoDoApp;

namespace Elo.Tests.Integracao;

/// <summary>
/// As rotas do contrato do app (docs/contrato-api.md do elo-app), chamadas como o app e o ESP32 chamam:
/// sem /api, com status em snake_case, ids em texto e datas no formato do toISOString().
/// </summary>
internal static class Contrato
{
    public static readonly string[] Status = ["tudo_bem", "agua_subindo", "preciso_ajuda"];

    /// <summary>O app manda a data com Date.toISOString(): "2026-11-05T15:00:00.000Z".</summary>
    public static string ComoNoApp(DateTime utc) =>
        utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}

/// <summary>Só leitura sobre os dados simulados.</summary>
public class ContratoDoAppLeituraApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Situacao_vem_no_formato_do_app()
    {
        var resposta = await _cliente.GetAsync("/situacao");
        var situacao = await resposta.LerAsync<SituacaoDoBairroApp>();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Jardim Pantanal (simulado)", situacao.Bairro);
        Assert.NotNull(situacao.ChamadaIniciadaEm);
        Assert.Equal(8, situacao.Ruas.Count);
        Assert.Equal(("2", "Rua do Aguapé", "preciso_ajuda"), (situacao.Ruas[0].Id, situacao.Ruas[0].Nome, situacao.Ruas[0].Status));
        Assert.Equal(3, situacao.Ruas.Count(rua => rua.Status is null));
        Assert.All(situacao.Ruas.Where(rua => rua.Status is null), rua => Assert.Null(rua.RespondidaEm));
        Assert.All(situacao.Ruas.Where(rua => rua.Status is not null), rua => Assert.Contains(rua.Status, Contrato.Status));
        Assert.EndsWith("Z", situacao.ChamadaIniciadaEm);
    }

    [Fact]
    public async Task Regua_padrao_e_a_do_ESP32_da_equipe_em_metros()
    {
        var regua = await (await _cliente.GetAsync("/regua")).LerAsync<LeituraDaReguaApp>();

        Assert.Equal("regua-exemplo", regua.ReguaId);
        Assert.Equal(1.42, regua.NivelMetros);
        Assert.Equal(1.2, regua.LimiteMetros);
        Assert.True(regua.NivelMetros >= regua.LimiteMetros); // o app mostra "acima do limite"
    }

    [Fact]
    public async Task Plano_da_rua_vem_com_os_nomes_do_app_e_os_contatos_de_emergencia()
    {
        var resposta = await _cliente.GetAsync("/ruas/1/plano");
        var plano = await resposta.LerAsync<PlanoDaRuaApp>();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(("1", "Rua das Garças"), (plano.RuaId, plano.RuaNome));
        Assert.False(string.IsNullOrWhiteSpace(plano.PontoEncontro));
        Assert.False(string.IsNullOrWhiteSpace(plano.RotaSaida));
        Assert.NotEmpty(plano.ItensParaLevar);
        Assert.Contains(plano.Contatos, contato => contato.Telefone == "199");
        Assert.Contains(plano.Contatos, contato => contato.Telefone == "193");
    }

    [Theory]
    [InlineData("/ruas/4/plano")]     // a rua existe, mas não tem plano: o app mostra o estado vazio
    [InlineData("/ruas/rua-3/plano")] // id que não é desta API
    [InlineData("/ruas/999/plano")]
    public async Task Rua_sem_plano_devolve_404(string caminho)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync(caminho)).StatusCode);
    }
}

/// <summary>Escrita: a "chamada" do app (resposta da rua) e a leitura do ESP32.</summary>
public class ContratoDoAppEscritaApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Chamada_do_app_entra_uma_vez_e_aparece_na_situacao()
    {
        var criadaEm = Contrato.ComoNoApp(DateTime.UtcNow);
        var corpo = new { id = "m3k9x2a-7f4q1b2c", ruaId = "6", status = "preciso_ajuda", pessoas = (int?)null, observacao = (string?)null, criadaEm };

        var primeira = await _cliente.PostAsJsonAsync("/chamadas", corpo);
        var reenvio = await _cliente.PostAsJsonAsync("/chamadas", corpo);

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reenvio.StatusCode);

        // O app lê o corpo de um 200/201 como JSON: ele precisa existir e ser válido.
        var registrada = await primeira.LerAsync<ChamadaDoAppDto>();
        Assert.Equal(("m3k9x2a-7f4q1b2c", "6", "preciso_ajuda", criadaEm), (registrada.Id, registrada.RuaId, registrada.Status, registrada.CriadaEm));

        var situacao = await (await _cliente.GetAsync("/situacao")).LerAsync<SituacaoDoBairroApp>();
        var rua = Assert.Single(situacao.Ruas, rua => rua.Id == "6");
        Assert.Equal("preciso_ajuda", rua.Status);
        Assert.Equal(criadaEm, rua.RespondidaEm); // o app compara com o criadaEm que ele mesmo mandou
    }

    [Theory]
    [InlineData("""{"id":"x1","ruaId":"6","status":"inundado"}""", HttpStatusCode.BadRequest)]
    [InlineData("""{"ruaId":"6","status":"tudo_bem"}""", HttpStatusCode.BadRequest)]
    [InlineData("""{"id":"x2","ruaId":"999","status":"tudo_bem"}""", HttpStatusCode.NotFound)]
    [InlineData("""{"id":"x3","ruaId":"rua-3","status":"tudo_bem"}""", HttpStatusCode.NotFound)]
    public async Task Chamada_fora_do_contrato_e_recusada(string json, HttpStatusCode esperado)
    {
        var resposta = await _cliente.PostAsync("/chamadas", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(esperado, resposta.StatusCode);
    }

    [Fact]
    public async Task Observacao_com_mais_de_200_caracteres_e_recusada()
    {
        var corpo = new { id = "x4", ruaId = "6", status = "tudo_bem", observacao = new string('a', 201), criadaEm = Contrato.ComoNoApp(DateTime.UtcNow) };

        var resposta = await _cliente.PostAsJsonAsync("/chamadas", corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Leitura_do_ESP32_exige_a_chave_do_dispositivo()
    {
        var corpo = new { reguaId = "regua-exemplo", nivelMetros = 1.55 };

        var semChave = await _cliente.PostAsJsonAsync("/leituras", corpo);
        var chaveErrada = await EnviarLeituraAsync(corpo, "chave-errada");
        var comChave = await EnviarLeituraAsync(corpo, EloApiFactory.ChaveDeDispositivo);

        Assert.Equal(HttpStatusCode.Unauthorized, semChave.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, chaveErrada.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, comChave.StatusCode);

        // O reguaId é comparado sem diferenciar maiúsculas.
        var regua = await (await _cliente.GetAsync("/regua?reguaId=REGUA-EXEMPLO")).LerAsync<LeituraDaReguaApp>();
        Assert.Equal(1.55, regua.NivelMetros);
    }

    [Fact]
    public async Task Leitura_sem_nivel_ou_de_regua_desconhecida_e_recusada()
    {
        var semNivel = await EnviarLeituraAsync(new { reguaId = "regua-exemplo", nivelMetros = (double?)null }, EloApiFactory.ChaveDeDispositivo);
        var desconhecida = await EnviarLeituraAsync(new { reguaId = "regua-que-nao-existe", nivelMetros = 1.0 }, EloApiFactory.ChaveDeDispositivo);

        Assert.Equal(HttpStatusCode.BadRequest, semNivel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, desconhecida.StatusCode);
    }

    private Task<HttpResponseMessage> EnviarLeituraAsync(object corpo, string chave)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/leituras") { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add("X-Chave-Dispositivo", chave);
        return _cliente.SendAsync(requisicao);
    }
}

/// <summary>Sem chamada aberta: o app vê todas as ruas em silêncio e a resposta não tem onde entrar.</summary>
public class ContratoSemChamadaApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Sem_chamada_aberta_as_ruas_ficam_em_silencio_e_a_resposta_e_recusada()
    {
        Assert.Equal(HttpStatusCode.OK, (await _cliente.PostAsync("/api/chamadas/2/encerramento", null)).StatusCode);

        var situacao = await (await _cliente.GetAsync("/situacao")).LerAsync<SituacaoDoBairroApp>();
        Assert.Null(situacao.ChamadaIniciadaEm);
        Assert.All(situacao.Ruas, rua => Assert.Null(rua.Status));

        var corpo = new { id = "depois-do-fim", ruaId = "1", status = "tudo_bem", criadaEm = Contrato.ComoNoApp(DateTime.UtcNow.AddSeconds(30)) };
        var resposta = await _cliente.PostAsJsonAsync("/chamadas", corpo);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }
}

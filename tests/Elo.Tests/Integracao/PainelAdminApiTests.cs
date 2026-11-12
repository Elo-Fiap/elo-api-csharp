using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elo.Application.Avisos;
using Elo.Application.Bairros;
using Elo.Application.Comum;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Elo.Tests.Integracao;

/// <summary>GET /api/admin/instantaneo sobre os dados do piloto (os bairros extras ficam desligados nesta fábrica).</summary>
public class PainelAdminApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    private async Task<JsonDocument> ObterAsync(string caminho = "/api/admin/instantaneo")
    {
        var resposta = await _cliente.GetAsync(caminho);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static IEnumerable<string> Campos(JsonElement objeto) => objeto.EnumerateObject().Select(campo => campo.Name);

    [Fact]
    public async Task Formato_da_resposta_segue_o_contrato_do_painel()
    {
        using var json = await ObterAsync();
        var raiz = json.RootElement;

        Assert.Equal(["geradoEm", "bairros", "sensores", "chamadas", "avisos"], Campos(raiz));

        var bairro = Assert.Single(raiz.GetProperty("bairros").EnumerateArray());
        Assert.Equal(
            ["id", "nome", "cidade", "uf", "codigoIbge", "latitude", "longitude", "dadosSimulados", "percentualSemApp", "semAppMedidoEm", "ruas"],
            Campos(bairro));
        Assert.Equal(1, bairro.GetProperty("id").GetInt32());
        Assert.Equal("Jardim Pantanal (simulado)", bairro.GetProperty("nome").GetString());
        Assert.Equal("SP", bairro.GetProperty("uf").GetString());
        Assert.Equal("3550308", bairro.GetProperty("codigoIbge").GetString());
        Assert.Equal(-23.47939, bairro.GetProperty("latitude").GetDouble());
        Assert.Equal(-46.40912, bairro.GetProperty("longitude").GetDouble());
        Assert.True(bairro.GetProperty("dadosSimulados").GetBoolean());
        Assert.Equal(31, bairro.GetProperty("percentualSemApp").GetDouble());

        var rua = bairro.GetProperty("ruas")[0];
        Assert.Equal(["id", "nome", "microarea", "nivelRisco", "ativa", "latitude", "longitude", "tracado"], Campos(rua));
        Assert.Equal("Alto", rua.GetProperty("nivelRisco").GetString());

        var sensor = raiz.GetProperty("sensores")[0];
        Assert.Equal(
            ["id", "codigo", "ruaId", "bairroId", "local", "latitude", "longitude", "limiteMetros", "ativo", "leituras"],
            Campos(sensor));
        Assert.Equal(["nivelMetros", "medidaEm"], Campos(sensor.GetProperty("leituras")[0]));

        var chamada = raiz.GetProperty("chamadas")[0];
        Assert.Equal(
            ["id", "bairroId", "tipo", "abertaEm", "encerradaEm", "avisoTitulo", "respostas", "acoes"],
            Campos(chamada));
        Assert.Equal(["id", "ruaId", "situacao", "pessoas", "respondidaEm", "recebidaEm"], Campos(chamada.GetProperty("respostas")[0]));
        Assert.Equal(["ruaId", "tipo", "em"], Campos(chamada.GetProperty("acoes")[0]));

        var aviso = raiz.GetProperty("avisos")[0];
        Assert.Equal(["id", "titulo", "severidade", "gravidade", "inicio", "fim", "ufs"], Campos(aviso));
    }

    [Fact]
    public async Task Datas_vem_em_UTC_com_Z_e_o_bairro_traz_a_medicao_sem_o_app_de_45_dias_atras()
    {
        using var json = await ObterAsync();
        var raiz = json.RootElement;

        var geradoEm = raiz.GetProperty("geradoEm").GetDateTime();
        var medidoEm = raiz.GetProperty("bairros")[0].GetProperty("semAppMedidoEm").GetDateTime();

        Assert.EndsWith("Z", raiz.GetProperty("geradoEm").GetString());
        Assert.EndsWith("Z", raiz.GetProperty("bairros")[0].GetProperty("semAppMedidoEm").GetString());
        Assert.InRange((geradoEm - medidoEm).TotalDays, 44.99, 45.01);

        foreach (var chamada in raiz.GetProperty("chamadas").EnumerateArray())
        {
            Assert.EndsWith("Z", chamada.GetProperty("abertaEm").GetString());
        }
    }

    [Fact]
    public async Task Piloto_tem_as_8_ruas_com_traçado_e_ponto()
    {
        using var json = await ObterAsync();
        var ruas = json.RootElement.GetProperty("bairros")[0].GetProperty("ruas").EnumerateArray().ToList();

        Assert.Equal(8, ruas.Count);
        Assert.All(ruas, rua =>
        {
            Assert.True(rua.GetProperty("ativa").GetBoolean());
            Assert.NotEqual(JsonValueKind.Null, rua.GetProperty("latitude").ValueKind);

            var tracado = rua.GetProperty("tracado").EnumerateArray().ToList();
            Assert.True(tracado.Count >= 2);
            Assert.Equal(2, tracado[0].GetArrayLength());
            Assert.InRange(tracado[0][0].GetDouble(), -47, -46); // longitude primeiro
            Assert.InRange(tracado[0][1].GetDouble(), -24, -23); // depois a latitude
        });
    }

    [Fact]
    public async Task Leituras_vem_em_metros_da_mais_antiga_para_a_mais_nova()
    {
        using var json = await ObterAsync();
        var regua = json.RootElement.GetProperty("sensores").EnumerateArray().First(s => s.GetProperty("codigo").GetString() == "regua-exemplo");

        Assert.Equal(1.2, regua.GetProperty("limiteMetros").GetDouble());
        Assert.Equal(-23.476551, regua.GetProperty("latitude").GetDouble());
        Assert.Equal(-46.41135, regua.GetProperty("longitude").GetDouble());
        Assert.Equal(1, regua.GetProperty("bairroId").GetInt32());

        var leituras = regua.GetProperty("leituras").EnumerateArray().ToList();
        Assert.Equal(6, leituras.Count);
        Assert.Equal(0.8, leituras[0].GetProperty("nivelMetros").GetDouble());
        Assert.Equal(1.42, leituras[^1].GetProperty("nivelMetros").GetDouble());

        var datas = leituras.Select(leitura => leitura.GetProperty("medidaEm").GetDateTime()).ToList();
        Assert.Equal(datas.Order(), datas);
    }

    [Fact]
    public async Task Chamadas_trazem_respostas_acoes_e_o_titulo_do_aviso()
    {
        using var json = await ObterAsync();
        var chamadas = json.RootElement.GetProperty("chamadas").EnumerateArray().ToList();

        Assert.Equal([2, 1], chamadas.Select(c => c.GetProperty("id").GetInt32())); // a mais recente primeiro

        var real = chamadas[0];
        Assert.Equal("Real", real.GetProperty("tipo").GetString());
        Assert.Equal(JsonValueKind.Null, real.GetProperty("encerradaEm").ValueKind);
        Assert.Equal("Acumulado de Chuva", real.GetProperty("avisoTitulo").GetString());
        Assert.Equal(6, real.GetProperty("respostas").GetArrayLength());
        Assert.Equal(["VouAteLa", "ContatoWhatsApp"], real.GetProperty("acoes").EnumerateArray().Select(a => a.GetProperty("tipo").GetString()));
        Assert.Contains(real.GetProperty("respostas").EnumerateArray(), r => r.GetProperty("situacao").GetString() == "PrecisoDeAjuda" && r.GetProperty("pessoas").GetInt32() == 5);

        var treino = chamadas[1];
        Assert.Equal("Treino", treino.GetProperty("tipo").GetString());
        Assert.NotEqual(JsonValueKind.Null, treino.GetProperty("encerradaEm").ValueKind);
        Assert.Equal(JsonValueKind.Null, treino.GetProperty("avisoTitulo").ValueKind);
        Assert.Equal(7, treino.GetProperty("respostas").GetArrayLength());
    }

    [Fact]
    public async Task Janela_de_dias_deixa_de_fora_o_treino_antigo_mas_mantem_a_chamada_aberta()
    {
        using var umDia = await ObterAsync("/api/admin/instantaneo?dias=1");
        using var seisDias = await ObterAsync("/api/admin/instantaneo?dias=6");
        using var oitoDias = await ObterAsync("/api/admin/instantaneo?dias=8");

        Assert.Equal([2], umDia.RootElement.GetProperty("chamadas").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()));
        Assert.Equal([2], seisDias.RootElement.GetProperty("chamadas").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()));
        Assert.Equal([2, 1], oitoDias.RootElement.GetProperty("chamadas").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Avisos_do_INMET_valem_para_o_municipio_do_bairro_com_a_UF_dele()
    {
        using var json = await ObterAsync();
        var avisos = json.RootElement.GetProperty("avisos").EnumerateArray().ToList();

        Assert.Equal(["56273", "56272", "56278"], avisos.Select(a => a.GetProperty("id").GetString()));
        Assert.All(avisos, aviso => Assert.Equal(["SP"], aviso.GetProperty("ufs").EnumerateArray().Select(u => u.GetString())));
        Assert.EndsWith("Z", avisos[0].GetProperty("inicio").GetString());
        Assert.Equal(new DateTime(2026, 11, 5, 3, 1, 0, DateTimeKind.Utc), avisos[0].GetProperty("inicio").GetDateTime().ToUniversalTime()); // 00:01 -03:00
    }

    [Theory]
    [InlineData("dias=0")]
    [InlineData("dias=31")]
    [InlineData("dias=-1")]
    [InlineData("dias=abc")]
    public async Task Dias_fora_de_1_a_30_devolve_400(string consulta)
    {
        var resposta = await _cliente.GetAsync($"/api/admin/instantaneo?{consulta}");
        var problema = await resposta.LerAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("dias", problema.Errors.Keys);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public async Task Limites_da_janela_sao_aceitos(int dias)
    {
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync($"/api/admin/instantaneo?dias={dias}")).StatusCode);
    }

    [Fact]
    public async Task CORS_continua_liberando_qualquer_origem()
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/admin/instantaneo");
        pedido.Headers.Add("Origin", "http://localhost:5173");

        var resposta = await _cliente.SendAsync(pedido);

        Assert.Equal("*", Assert.Single(resposta.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}

/// <summary>POST/PUT de bairro com os dados novos. Fábrica própria, porque estes testes gravam no banco.</summary>
public class BairroGeograficoApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Bairro_aceita_e_devolve_os_dados_novos_e_recusa_os_invalidos()
    {
        var corpo = new
        {
            nome = "Vila Teste", cidade = "Recife", subprefeitura = "Centro", dadosSimulados = true,
            uf = "pe", codigoIbge = "2611606", latitude = -8.05, longitude = -34.88,
            percentualSemApp = 28.5, semAppMedidoEm = "2026-09-28T05:00:00Z"
        };

        var criado = await _cliente.EnviarAsync("/api/bairros", corpo);
        var detalhe = await criado.LerAsync<BairroDetalheDto>();

        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        Assert.Equal("PE", detalhe.Uf);
        Assert.Equal("2611606", detalhe.CodigoIbge);
        Assert.Equal(28.5, detalhe.PercentualSemApp);
        Assert.Equal(new DateTime(2026, 9, 28, 5, 0, 0, DateTimeKind.Utc), detalhe.SemAppMedidoEm);

        var lista = await (await _cliente.GetAsync("/api/bairros")).LerAsync<List<BairroResumoDto>>();
        Assert.Equal(-8.05, lista.Single(b => b.Id == detalhe.Id).Latitude);

        var atualizado = await _cliente.PutAsJsonAsync($"/api/bairros/{detalhe.Id}", new { corpo.nome, corpo.cidade, corpo.subprefeitura, corpo.dadosSimulados }, EloApiFactory.Json);
        Assert.Null((await atualizado.LerAsync<BairroDetalheDto>()).PercentualSemApp); // campos opcionais: sem eles, ficam vazios

        // Validação de borda (DataAnnotations): 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.EnviarAsync("/api/bairros", new { nome = "X1", cidade = "Y", subprefeitura = "Z", uf = "SPP" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.EnviarAsync("/api/bairros", new { nome = "X2", cidade = "Y", subprefeitura = "Z", codigoIbge = "123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.EnviarAsync("/api/bairros", new { nome = "X3", cidade = "Y", subprefeitura = "Z", latitude = 91, longitude = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.EnviarAsync("/api/bairros", new { nome = "X4", cidade = "Y", subprefeitura = "Z", percentualSemApp = 101 })).StatusCode);

        // Regra do domínio que o DataAnnotations não cobre: latitude sem longitude (422).
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _cliente.EnviarAsync("/api/bairros", new { nome = "X5", cidade = "Y", subprefeitura = "Z", latitude = 10 })).StatusCode);
    }
}

/// <summary>Com o INMET fora do ar e sem lista guardada, a rota do painel segue funcionando (nunca 503).</summary>
public class PainelAdminSemInmetApiTests(EloApiFactorySemInmet fabrica) : IClassFixture<EloApiFactorySemInmet>
{
    [Fact]
    public async Task Avisos_vem_vazio_e_o_resto_da_resposta_continua()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/api/admin/instantaneo");
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Empty(json.RootElement.GetProperty("avisos").EnumerateArray());
        Assert.Single(json.RootElement.GetProperty("bairros").EnumerateArray());
    }
}

/// <summary>Fábrica com a semente extra do painel ligada (a padrão do projeto), para conferir os 12 bairros.</summary>
public sealed class EloApiFactoryComBairrosDoPainel : EloApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // A última configuração vence: religa os extras que a fábrica base desliga.
        builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(
            new Dictionary<string, string?> { ["DadosDeExemplo:IncluirBairrosDoPainel"] = "true" }));
    }
}

public class PainelAdminComBairrosDoPainelApiTests(EloApiFactoryComBairrosDoPainel fabrica) : IClassFixture<EloApiFactoryComBairrosDoPainel>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Semente_extra_cria_12_bairros_com_as_ruas_reguas_e_chamadas_do_arquivo()
    {
        var resposta = await _cliente.GetAsync("/api/admin/instantaneo?dias=30");
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var raiz = json.RootElement;

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var bairros = raiz.GetProperty("bairros").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, 12), bairros.Select(b => b.GetProperty("id").GetInt32()));
        Assert.Equal("Jardim Pantanal (simulado)", bairros[0].GetProperty("nome").GetString());
        Assert.Equal("Vila Nova Esperança", bairros[1].GetProperty("nome").GetString());
        Assert.Equal("PE", bairros[1].GetProperty("uf").GetString());
        Assert.Equal("2611606", bairros[1].GetProperty("codigoIbge").GetString());
        Assert.Equal(28, bairros[1].GetProperty("percentualSemApp").GetDouble());
        Assert.All(bairros, b => Assert.Equal(JsonValueKind.String, b.GetProperty("semAppMedidoEm").ValueKind));

        // 8 do piloto + 78 do arquivo. Os extras não têm traçado (nulo), o piloto tem.
        var ruas = bairros.SelectMany(b => b.GetProperty("ruas").EnumerateArray()).ToList();
        Assert.Equal(86, ruas.Count);
        Assert.Equal(8, ruas.Count(r => r.GetProperty("tracado").ValueKind == JsonValueKind.Array));

        // 2 do piloto + 7 réguas do arquivo (a "regua-exemplo" do arquivo é a do piloto), com 433 leituras cada, 432 ou 433 dentro da janela (72 h, de 10 em 10 minutos).
        var sensores = raiz.GetProperty("sensores").EnumerateArray().ToList();
        Assert.Equal(9, sensores.Count);
        Assert.Contains(sensores, s => s.GetProperty("codigo").GetString() == "regua-2" && s.GetProperty("bairroId").GetInt32() == 2);
        // A primeira leitura do arquivo é de exatamente 72 h antes da semente: alguns segundos depois ela já saiu da janela.
        Assert.All(sensores.Where(s => s.GetProperty("codigo").GetString() != "regua-exemplo" && s.GetProperty("codigo").GetString() != "regua-barqueiros"),
            s => Assert.InRange(s.GetProperty("leituras").GetArrayLength(), 432, 433));

        var chamadas = raiz.GetProperty("chamadas").EnumerateArray().ToList();
        Assert.Equal(21, chamadas.Count); // 19 do arquivo (fora o bairro 1) + 2 do piloto
        Assert.Equal(4, chamadas.Count(c => c.GetProperty("encerradaEm").ValueKind == JsonValueKind.Null)); // piloto, bairros 2, 3 e 8
        var extras = chamadas.Where(c => c.GetProperty("bairroId").GetInt32() != 1).ToList();
        Assert.Equal(124, extras.Sum(c => c.GetProperty("respostas").GetArrayLength()));
        Assert.Equal(19, extras.Sum(c => c.GetProperty("acoes").GetArrayLength()));
        Assert.All(extras.Where(c => c.GetProperty("tipo").GetString() == "Real"), c => Assert.NotEqual(JsonValueKind.Null, c.GetProperty("avisoTitulo").ValueKind));
    }

    [Fact]
    public async Task Janela_de_1_dia_so_traz_as_chamadas_abertas_e_as_do_ultimo_dia()
    {
        var resposta = await _cliente.GetAsync("/api/admin/instantaneo?dias=1");
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var chamadas = json.RootElement.GetProperty("chamadas").EnumerateArray().ToList();

        // Abertas (4) mais as fechadas que começaram nas últimas 24 h: ch-006 (9 h) e ch-007 (14 h).
        Assert.Equal(6, chamadas.Count);
    }

    [Fact]
    public async Task Cada_rua_do_arquivo_aparece_na_API_pelo_nome_do_bairro()
    {
        var lista = await (await _cliente.GetAsync("/api/bairros")).LerAsync<List<BairroResumoDto>>();

        Assert.Equal(12, lista.Count);
        Assert.Equal(7, lista.Single(b => b.Nome == "Beira Rio").TotalDeRuasAtivas);
        Assert.All(lista, b => Assert.NotNull(b.CodigoIbge));
    }
}

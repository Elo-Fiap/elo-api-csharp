using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elo.Infrastructure.Seguranca;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Risco;
using Elo.Tests.Unitarios.Seguranca;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elo.Tests.Integracao;

/// <summary>A API de verdade com o Gemini "falso" no lugar da rede: chave de mentira e um handler que responde o que o teste quiser.</summary>
public class FabricaComGeminiFalso : FabricaComCapturaDeLog
{
    public const string TextoDoGemini = "Uma informação sua ia aparecer onde não devia. O Elo a escondeu a tempo.";

    internal GeminiFalso Gemini { get; } = GeminiFalso.Respondendo(TextoDoGemini);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ia:Habilitada"] = "true",
            ["Ia:Provedor"] = "Gemini",
            ["Ia:Gemini:ChaveApi"] = CenarioDeTradutor.Chave
        }));
        builder.ConfigureTestServices(servicos =>
            servicos.AddHttpClient(TradutorGemini.NomeDoCliente).ConfigurePrimaryHttpMessageHandler(() => Gemini));
    }
}

/// <summary>
/// O painel Risco Cidadão ponta a ponta (cenário 5) e o worker de tradução com o Gemini falso (cenário 6).
/// Cada teste usa a própria fábrica: assim os números do painel são exatos (os eventos ficam em memória).
/// </summary>
public class PainelDeSegurancaApiTests
{
    private const string Cpf = "123.456.789-00";
    private const string Rg = "12.345.678-9";
    private const string Senha = "abc12345";
    private const string Saude = "diabética";

    private static Task<HttpResponseMessage> EnviarPedidoDeAjuda(HttpClient cliente) =>
        cliente.PostAsJsonAsync("/chamadas", new
        {
            id = $"painel-{Guid.NewGuid():N}",
            ruaId = "6",
            status = "preciso_ajuda",
            pessoas = 2,
            observacao = $"CPF {Cpf}, RG {Rg}, senha: {Senha}, a vizinha é {Saude}",
            criadaEm = Contrato.ComoNoApp(DateTime.UtcNow)
        });

    private static async Task<JsonElement> LerPainelAsync(HttpClient cliente)
    {
        var resposta = await cliente.GetAsync("/api/seguranca/painel");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    /// <summary>Espera o worker escrever as mensagens (a tradução roda em segundo plano, fora da requisição).</summary>
    private static async Task<JsonElement> EsperarTraducaoAsync(HttpClient cliente, int minimoDeEventos)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var painel = await LerPainelAsync(cliente);
            var eventos = painel.GetProperty("ultimosEventos").EnumerateArray().ToList();
            if (eventos.Count >= minimoDeEventos && eventos.All(e => e.GetProperty("mensagemAoCidadao").ValueKind == JsonValueKind.String))
            {
                return painel;
            }

            Assert.True(DateTime.UtcNow < limite, "As mensagens não foram escritas em 10 segundos.");
            await Task.Delay(25);
        }
    }

    [Fact]
    public async Task Cenario5_sem_eventos_o_painel_e_verde()
    {
        using var fabrica = new FabricaComCapturaDeLog();
        using var cliente = fabrica.CreateClient();

        var painel = await LerPainelAsync(cliente);

        Assert.Equal("Verde", painel.GetProperty("farol").GetString());
        Assert.Equal(0, painel.GetProperty("indice").GetInt32());
        Assert.Equal(CalculadoraDeRiscoCidadao.ResumoVerde, painel.GetProperty("resumo").GetString());
        Assert.Equal(24, painel.GetProperty("janelaEmHoras").GetInt32());
        Assert.Equal(0, painel.GetProperty("totais").GetProperty("entrada").GetInt32());
        Assert.Empty(painel.GetProperty("ultimosEventos").EnumerateArray());
        Assert.Empty(fabrica.Eventos.Listar(null, 10)); // ler o painel não gera evento
    }

    [Fact]
    public async Task Cenario5_depois_do_pedido_com_dado_sensivel_o_painel_fica_vermelho_com_mensagens_prontas()
    {
        using var fabrica = new FabricaComCapturaDeLog();
        using var cliente = fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.Created, (await EnviarPedidoDeAjuda(cliente)).StatusCode);
        var painel = await EsperarTraducaoAsync(cliente, minimoDeEventos: 3);
        var bruto = painel.GetRawText();

        Assert.Equal("Vermelho", painel.GetProperty("farol").GetString());

        // O índice segue a fórmula: severidade (alta 3) x etapa (entrada 1, saída 3), vezes 4.
        var eventos = painel.GetProperty("ultimosEventos").EnumerateArray().ToList();
        var pontos = eventos.Sum(e => (e.GetProperty("severidade").GetString() == "Alta" ? 3 : 2) * (e.GetProperty("etapa").GetString() == "Entrada" ? 1 : 3));
        Assert.Equal(Math.Min(100, pontos * 4), painel.GetProperty("indice").GetInt32());
        Assert.InRange(painel.GetProperty("indice").GetInt32(), 1, 100);

        var totais = painel.GetProperty("totais");
        Assert.Equal(1, totais.GetProperty("entrada").GetInt32());
        Assert.True(totais.GetProperty("log").GetInt32() >= 1);
        Assert.True(totais.GetProperty("banco").GetInt32() >= 1);
        Assert.Equal(0, totais.GetProperty("respostaHttp").GetInt32());
        Assert.Equal(0, totais.GetProperty("llm").GetInt32());

        var porTipo = painel.GetProperty("porTipo");
        Assert.True(porTipo.GetProperty("cpf").GetInt32() >= 1);
        Assert.True(porTipo.GetProperty("dadoDeSaude").GetInt32() >= 1);
        Assert.True(porTipo.TryGetProperty("senha", out _));
        Assert.True(porTipo.TryGetProperty("rg", out _));

        // Mensagens do TradutorPadrao (a IA está desligada nos testes) e o resumo é uma delas.
        Assert.All(eventos, e => Assert.Equal("modelo padrão", e.GetProperty("traduzidaPor").GetString()));
        var mensagens = eventos.Select(e => e.GetProperty("mensagemAoCidadao").GetString()).ToList();
        Assert.Contains(painel.GetProperty("resumo").GetString(), mensagens);
        Assert.DoesNotContain(CalculadoraDeRiscoCidadao.ResumoVermelho, mensagens);

        // O painel nunca mostra o dado, nem fala difícil com o cidadão.
        foreach (var valor in new[] { Cpf, Rg, Senha, Saude, "456.789" })
        {
            Assert.DoesNotContain(valor, bruto);
        }

        foreach (var mensagem in mensagens)
        {
            Assert.DoesNotContain("PII", mensagem);
            Assert.DoesNotContain("taint", mensagem, StringComparison.OrdinalIgnoreCase);
        }

        // Ler o painel (que passa pela guarda de resposta) não pode criar evento novo: seria um laço sem fim.
        var antes = fabrica.Eventos.Listar(null, 500).Count;
        await LerPainelAsync(cliente);
        await cliente.GetAsync("/api/seguranca/eventos");
        Assert.Equal(antes, fabrica.Eventos.Listar(null, 500).Count);
    }

    [Fact]
    public async Task Cenario5_entrada_sem_saida_bloqueada_deixa_o_painel_amarelo()
    {
        using var fabrica = new FabricaComCapturaDeLog();
        using var cliente = fabrica.CreateClient();

        // Corpo com chaves extras (exemplo do professor): marcado na entrada, mas nada tenta sair (nem vai para a observação).
        var resposta = await cliente.PostAsync(
            "/api/chamadas/2/respostas",
            new StringContent(
                $$"""{"idLocal":"painel-{{Guid.NewGuid():N}}","ruaId":6,"situacao":"TudoBem","documento":"{{Cpf}}","senha":"{{Senha}}"}""",
                System.Text.Encoding.UTF8,
                "application/json"));
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var painel = await EsperarTraducaoAsync(cliente, minimoDeEventos: 1);

        Assert.Equal("Amarelo", painel.GetProperty("farol").GetString());
        Assert.Equal(1, painel.GetProperty("totais").GetProperty("entrada").GetInt32());
        Assert.Equal(12, painel.GetProperty("indice").GetInt32()); // um evento só (CPF e senha juntos): alta 3 x entrada 1 = 3 pontos, x 4
    }

    [Fact]
    public async Task Eventos_lista_do_mais_recente_e_respeita_o_limite()
    {
        using var fabrica = new FabricaComCapturaDeLog();
        using var cliente = fabrica.CreateClient();
        await EnviarPedidoDeAjuda(cliente);
        await EsperarTraducaoAsync(cliente, minimoDeEventos: 3);

        var resposta = await cliente.GetAsync("/api/seguranca/eventos?limite=2");
        var lista = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(2, lista.Count);
        Assert.True(lista[0].GetProperty("id").GetInt64() > lista[1].GetProperty("id").GetInt64());
        // Na entrada o dado ainda não saiu para lugar nenhum: sink nulo; nas saídas, o nome da saída.
        Assert.All(lista, e => Assert.Equal(e.GetProperty("etapa").GetString() == "Entrada", e.GetProperty("sink").ValueKind == JsonValueKind.Null));

        foreach (var invalido in new[] { 0, 501 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync($"/api/seguranca/eventos?limite={invalido}")).StatusCode);
        }
    }

    [Fact]
    public async Task A_pagina_do_painel_e_servida_sem_recursos_de_fora()
    {
        using var fabrica = new FabricaComCapturaDeLog();
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/risco-cidadao.html");
        var html = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("/api/seguranca/painel", html);
        Assert.DoesNotContain("https://", html);
        Assert.DoesNotContain("http://", html);

        var inicio = await (await cliente.GetAsync("/")).Content.ReadAsStringAsync();
        Assert.Contains("/risco-cidadao.html", inicio);
    }

    [Fact]
    public async Task Cenario6_worker_usa_o_gemini_com_a_chave_no_header_e_sem_o_dado_pessoal()
    {
        using var fabrica = new FabricaComGeminiFalso();
        using var cliente = fabrica.CreateClient();

        await EnviarPedidoDeAjuda(cliente);
        var painel = await EsperarTraducaoAsync(cliente, minimoDeEventos: 3);

        var eventos = painel.GetProperty("ultimosEventos").EnumerateArray().ToList();
        Assert.All(eventos, e =>
        {
            Assert.Equal("gemini", e.GetProperty("traduzidaPor").GetString());
            Assert.Equal(FabricaComGeminiFalso.TextoDoGemini, e.GetProperty("mensagemAoCidadao").GetString());
        });
        Assert.Equal(FabricaComGeminiFalso.TextoDoGemini, painel.GetProperty("resumo").GetString());

        // Uma chamada por assinatura (etapa + saída + tipos): o cache evita repetir a mesma falha.
        var assinaturas = fabrica.Eventos.Listar(null, 500).Select(TradutorDeEventosWorker.Assinatura).Distinct().Count();
        Assert.Equal(assinaturas, fabrica.Gemini.Requisicoes.Count);

        foreach (var requisicao in fabrica.Gemini.Requisicoes)
        {
            Assert.Equal(CenarioDeTradutor.Chave, requisicao.Cabecalhos["x-goog-api-key"]);
            Assert.DoesNotContain(CenarioDeTradutor.Chave, requisicao.Endereco.ToString());
            foreach (var valor in new[] { Cpf, Rg, Senha, Saude, "456.789" })
            {
                Assert.DoesNotContain(valor, requisicao.Corpo);
            }
        }

        // A chave não aparece em nenhum log nem no painel.
        Assert.DoesNotContain(fabrica.Captura.Linhas, l => l.Contains(CenarioDeTradutor.Chave));
        Assert.DoesNotContain(CenarioDeTradutor.Chave, painel.GetRawText());
    }

    [Fact]
    public async Task O_painel_nao_barra_a_mensagem_da_ia_nem_cria_evento_ao_ser_lido()
    {
        using var fabrica = new FabricaComIaQueFalaDeSenha();
        using var cliente = fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.Created, (await EnviarPedidoDeAjuda(cliente)).StatusCode);
        await EsperarTraducaoAsync(cliente, minimoDeEventos: 3);

        // A frase da IA tem "senha e ...", que o motor de PII lê como uma senha declarada. Ela é texto do sistema,
        // escrita só com metadados: o painel tem de entregar a frase inteira e não pode registrar bloqueio de si mesmo.
        var antes = fabrica.Eventos.Listar(null, 500).Count;
        var painel = await LerPainelAsync(cliente);
        var lista = await (await cliente.GetAsync("/api/seguranca/eventos")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(antes, fabrica.Eventos.Listar(null, 500).Count);
        Assert.Equal(0, painel.GetProperty("totais").GetProperty("respostaHttp").GetInt32());
        Assert.Equal(FabricaComIaQueFalaDeSenha.Texto, painel.GetProperty("resumo").GetString());
        Assert.All(painel.GetProperty("ultimosEventos").EnumerateArray(), e =>
            Assert.Equal(FabricaComIaQueFalaDeSenha.Texto, e.GetProperty("mensagemAoCidadao").GetString()));
        Assert.All(lista.EnumerateArray(), e =>
            Assert.Equal(FabricaComIaQueFalaDeSenha.Texto, e.GetProperty("mensagemAoCidadao").GetString()));
    }
}

/// <summary>Como a <see cref="FabricaComGeminiFalso"/>, mas a IA escreve "senha e ...": frase que o motor de PII confunde com uma senha.</summary>
public class FabricaComIaQueFalaDeSenha : FabricaComCapturaDeLog
{
    public const string Texto = "O sistema viu que a mensagem tinha seu CPF e sua senha e protegeu os dois antes de qualquer coisa.";

    internal GeminiFalso Gemini { get; } = GeminiFalso.Respondendo(Texto);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ia:Habilitada"] = "true",
            ["Ia:Provedor"] = "Gemini",
            ["Ia:Gemini:ChaveApi"] = CenarioDeTradutor.Chave
        }));
        builder.ConfigureTestServices(servicos =>
            servicos.AddHttpClient(TradutorGemini.NomeDoCliente).ConfigurePrimaryHttpMessageHandler(() => Gemini));
    }
}

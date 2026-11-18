using System.Net;
using System.Text;
using Elo.Api.Seguranca;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elo.Tests.Unitarios.Seguranca;

/// <summary>Os middlewares e o log protegido, num pipeline mínimo (sem o banco da API).</summary>
public class PipelineDeSegurancaTests
{
    /// <summary>Um tratador de exceções como o da API: escreve um ProblemDetails que cita um dado que veio na requisição.</summary>
    private sealed class TratadorQueVaza(IProblemDetailsService problemas) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken ct)
        {
            http.Response.StatusCode = 422;
            return await problemas.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = new ProblemDetails { Status = 422, Title = "Erro", Detail = excecao.Message }
            });
        }
    }

    private static async Task<(IHost host, HttpClient cliente)> SubirAsync(Action<IApplicationBuilder> rotas)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(servicos =>
                {
                    servicos.AddRouting();
                    servicos.AddProblemDetails();
                    servicos.AddExceptionHandler<TratadorQueVaza>();
                    servicos.AddSegurancaDaApi();
                })
                .Configure(app =>
                {
                    app.UseSegurancaDeEntradaESaida();
                    app.UseExceptionHandler();
                    rotas(app);
                }))
            .StartAsync();
        return (host, host.GetTestClient());
    }

    [Fact]
    public async Task Problem_details_do_tratador_de_excecoes_tambem_passa_pela_guarda()
    {
        var (host, cliente) = await SubirAsync(app => app.Run(ctx =>
            throw new InvalidOperationException("Valor recusado: 123.456.789-00 e senha abc12345")));
        using var _ = host;

        var resposta = await cliente.PostAsync(
            "/x", new StringContent("""{"documento":"123.456.789-00","senha":"abc12345"}""", Encoding.UTF8, "application/json"));
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, resposta.StatusCode);
        Assert.DoesNotContain("123.456.789-00", corpo);
        Assert.DoesNotContain("abc12345", corpo);
        Assert.Contains("***.456.***-00", corpo);
        Assert.Contains("[senha oculta]", corpo);
        Assert.Equal(Encoding.UTF8.GetByteCount(corpo), resposta.Content.Headers.ContentLength);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

        var registro = host.Services.GetRequiredService<IRegistroDeEventos>();
        Assert.Contains(registro.Listar(null, 10), e => e.Sink == Sink.RespostaHttp && e.Onde == "POST /x");
    }

    [Fact]
    public async Task Resposta_sem_nada_sensivel_segue_com_os_mesmos_bytes_e_nao_json_nem_e_tocado()
    {
        const string original = """{"b":1,"a":"ação  com   espaços","c":[3,2,1]}""";
        var (host, cliente) = await SubirAsync(app => app.Run(async ctx =>
        {
            ctx.Response.ContentType = ctx.Request.Path == "/json" ? "application/json" : "text/plain";
            await ctx.Response.WriteAsync(original);
        }));
        using var _ = host;

        Assert.Equal(original, await cliente.GetStringAsync("/json")); // ordem, acentos e espaços intactos
        Assert.Equal(original, await cliente.GetStringAsync("/texto"));
        Assert.Empty(host.Services.GetRequiredService<IRegistroDeEventos>().Listar(null, 10));
    }

    [Fact]
    public async Task Documentacao_e_arquivos_com_extensao_nao_passam_pela_guarda()
    {
        var (host, cliente) = await SubirAsync(app => app.Run(async ctx =>
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"observacao":"CPF 123.456.789-00"}""");
        }));
        using var _ = host;

        Assert.Contains("123.456.789-00", await cliente.GetStringAsync("/openapi/v1.json"));
        Assert.Contains("123.456.789-00", await cliente.GetStringAsync("/dados.json"));
        Assert.DoesNotContain("123.456.789-00", await cliente.GetStringAsync("/outra"));
    }

    [Fact]
    public async Task Query_string_e_marcada_na_entrada()
    {
        var (host, cliente) = await SubirAsync(app => app.Run(ctx => ctx.Response.WriteAsync("ok")));
        using var _ = host;

        await cliente.GetAsync("/x?cpf=123.456.789-00&bairroId=1");

        var entrada = Assert.Single(host.Services.GetRequiredService<IRegistroDeEventos>().Listar(null, 10));
        Assert.Equal(EtapaDoEvento.Entrada, entrada.Etapa);
        Assert.Equal([TipoDePii.Cpf], entrada.Tipos);
        Assert.Equal("GET /x", entrada.Onde);
    }

    [Fact]
    public async Task Numero_sem_aspas_e_marcado_so_quando_a_chave_diz_o_que_ele_e()
    {
        var (host, cliente) = await SubirAsync(app => app.Run(ctx => ctx.Response.WriteAsync("ok")));
        using var _ = host;

        await cliente.PostAsync("/x", new StringContent(
            """{"pin":98765,"cpf":12345678909,"pessoas":12345678,"criadoEmMs":1731900000000}""", Encoding.UTF8, "application/json"));

        var entrada = Assert.Single(host.Services.GetRequiredService<IRegistroDeEventos>().Listar(null, 10));
        Assert.Equal([TipoDePii.Cpf, TipoDePii.Senha], entrada.Tipos.OrderBy(t => t));
        Assert.Equal(2, entrada.Quantidade); // contagem e data em milissegundos não viram telefone
    }

    // ---------- Log protegido ----------

    private sealed class Capturador : ILoggerProvider
    {
        public List<(string Mensagem, Exception? Excecao, string Formato)> Linhas { get; } = [];

        public ILogger CreateLogger(string categoria) => new Logger(this);
        public void Dispose() { }

        private sealed class Logger(Capturador dono) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel nivel) => true;

            public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? excecao, Func<TState, Exception?, string> formatador)
            {
                var formato = estado is IEnumerable<KeyValuePair<string, object?>> pares
                    ? pares.FirstOrDefault(p => p.Key == "{OriginalFormat}").Value?.ToString() ?? ""
                    : "";
                dono.Linhas.Add((formatador(estado, excecao), excecao, formato));
            }
        }
    }

    private static (ILogger logger, Capturador a, Capturador b, RegistroDeEventosEmMemoria registro) MontarLog()
    {
        var registro = new RegistroDeEventosEmMemoria(TimeProvider.System);
        var guarda = new GuardaDeSaida(MotorDePii.Padrao, new AcessoDeTeste(), registro);
        var a = new Capturador();
        var b = new Capturador();
        var provedor = new ProvedorDeLogProtegido([a, b], guarda);
        return (provedor.CreateLogger("Elo.Teste"), a, b, registro);
    }

    [Fact]
    public void Log_mascara_a_mensagem_para_todos_os_providers_e_registra_um_evento_so()
    {
        var (logger, a, b, registro) = MontarLog();

        logger.LogWarning("Pedido de ajuda na rua {RuaId}: {Observacao}", 6, "CPF 123.456.789-00 e a vizinha diabética");

        foreach (var capturador in new[] { a, b })
        {
            var linha = Assert.Single(capturador.Linhas);
            Assert.Equal("Pedido de ajuda na rua 6: CPF ***.456.***-00 e a vizinha [dado de saúde oculto]", linha.Mensagem);
            Assert.DoesNotContain("123.456", linha.Formato);
            Assert.DoesNotContain("diabética", linha.Formato);
        }

        var evento = Assert.Single(registro.Listar(null, 10));
        Assert.Equal(Sink.Log, evento.Sink);
        Assert.Equal("Elo.Teste", evento.Onde);
    }

    [Fact]
    public void Log_limpo_chega_ao_provider_intacto_com_o_estado_original()
    {
        var (logger, a, _, registro) = MontarLog();

        logger.LogInformation("Resposta {Situacao} da rua {RuaId}", "TudoBem", 2);

        var linha = Assert.Single(a.Linhas);
        Assert.Equal("Resposta TudoBem da rua 2", linha.Mensagem);
        Assert.Equal("Resposta {Situacao} da rua {RuaId}", linha.Formato);
        Assert.Empty(registro.Listar(null, 10));
    }

    [Fact]
    public void Log_com_excecao_que_cita_dado_sensivel_vai_com_excecao_sanitizada()
    {
        var (logger, a, _, _) = MontarLog();

        logger.LogError(new InvalidOperationException("falha com CPF 123.456.789-00"), "Erro ao salvar");

        var linha = Assert.Single(a.Linhas);
        var excecao = Assert.IsType<ExcecaoSanitizada>(linha.Excecao);
        Assert.DoesNotContain("123.456.789-00", excecao.Message);
        Assert.DoesNotContain("123.456.789-00", excecao.ToString());
        Assert.Contains("***.456.***-00", excecao.ToString());
    }
}

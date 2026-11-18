using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elo.Tests.Integracao;

/// <summary>Um provider de log que só guarda o que receberia (a mensagem final e a exceção), como o console imprimiria.</summary>
internal sealed class ProvedorDeLogDeCaptura : ILoggerProvider
{
    public ConcurrentQueue<string> Linhas { get; } = new();

    public ILogger CreateLogger(string categoria) => new Capturador(this, categoria);
    public void Dispose() { }

    private sealed class Capturador(ProvedorDeLogDeCaptura dono, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel nivel) => true;

        public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? excecao, Func<TState, Exception?, string> formatador) =>
            dono.Linhas.Enqueue($"[{nivel}] {categoria}: {formatador(estado, excecao)} {excecao}");
    }
}

/// <summary>A API de verdade, com um provider de log de captura registrado "por fora" (como um provider qualquer de produção).</summary>
public class FabricaComCapturaDeLog : EloApiFactory
{
    internal ProvedorDeLogDeCaptura Captura { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(servicos => servicos.AddSingleton<ILoggerProvider>(Captura));
    }

    public string ArquivoDoBanco =>
        new SqliteConnectionStringBuilder(Services.GetRequiredService<IConfiguration>()["ConnectionStrings:Elo"]).DataSource;

    public IRegistroDeEventos Eventos => Services.GetRequiredService<IRegistroDeEventos>();
}

/// <summary>
/// Taint analysis ponta a ponta: o dado sensível entra pelo JSON, é marcado na entrada e não pode sair em
/// nenhum sink (banco, log, resposta HTTP). Também protege o contrato do app e do painel.
/// </summary>
public class TaintAnalysisApiTests(FabricaComCapturaDeLog fabrica) : IClassFixture<FabricaComCapturaDeLog>
{
    private const string Cpf = "123.456.789-00";
    private const string Rg = "12.345.678-9";
    private const string Senha = "abc12345";
    private const string Saude = "diabética";

    private readonly HttpClient _cliente = fabrica.CreateClient();

    private long UltimoIdDeEvento() => fabrica.Eventos.Listar(null, 1).Select(e => e.Id).FirstOrDefault();

    private List<EventoDeSeguranca> EventosDepoisDe(long id) =>
        [.. fabrica.Eventos.Listar(null, 500).Where(e => e.Id > id)];

    private string LerObservacaoDoBanco(string idLocal)
    {
        using var conexao = new SqliteConnection($"Data Source={fabrica.ArquivoDoBanco}");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT Observacao FROM Respostas WHERE IdLocal = $id";
        comando.Parameters.AddWithValue("$id", idLocal);
        return (string)comando.ExecuteScalar()!;
    }

    [Fact]
    public async Task Cenario1_dado_sensivel_na_observacao_nao_chega_ao_banco_ao_log_nem_a_resposta()
    {
        var antes = UltimoIdDeEvento();
        var idLocal = $"taint-{Guid.NewGuid():N}";
        var observacao = $"CPF {Cpf}, RG {Rg}, senha: {Senha}, a vizinha é {Saude}";

        var resposta = await _cliente.PostAsJsonAsync("/chamadas", new
        {
            id = idLocal,
            ruaId = "6",
            status = "preciso_ajuda",
            pessoas = 2,
            observacao,
            criadaEm = Contrato.ComoNoApp(DateTime.UtcNow)
        });
        var corpoDaResposta = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        // Banco: nenhum dos valores, só as máscaras.
        var noBanco = LerObservacaoDoBanco(idLocal);
        // Resposta HTTP e log também.
        var log = string.Join('\n', fabrica.Captura.Linhas);

        foreach (var valor in new[] { Cpf, Rg, Senha, Saude })
        {
            Assert.DoesNotContain(valor, noBanco);
            Assert.DoesNotContain(valor, corpoDaResposta);
            Assert.DoesNotContain(valor, log);
        }

        Assert.Contains("[RG oculto]", noBanco);
        Assert.Contains("[senha oculta]", noBanco);
        Assert.Contains("[dado de saúde oculto]", noBanco);

        // O log operacional "Pedido de ajuda" existe e saiu mascarado (inclusive o CPF, que o domínio ainda não tinha mascarado).
        var linhaDoPedido = Assert.Single(fabrica.Captura.Linhas, l => l.Contains("Pedido de ajuda na rua 6") && l.Contains("***.456.***-00"));
        Assert.Contains("***.456.***-00", linhaDoPedido);
        Assert.Contains("[RG oculto]", linhaDoPedido);
        Assert.Contains("[dado de saúde oculto]", linhaDoPedido);
        Assert.StartsWith("[Warning]", linhaDoPedido);

        // Eventos: entrada marcada, log bloqueado, banco bloqueado.
        var eventos = EventosDepoisDe(antes);
        var entrada = Assert.Single(eventos, e => e.Etapa == EtapaDoEvento.Entrada);
        Assert.Equal("POST /chamadas", entrada.Onde);
        Assert.Equal(AcaoDeSeguranca.Marcado, entrada.Acao);
        Assert.Null(entrada.Sink);
        Assert.Equal(
            [TipoDePii.Cpf, TipoDePii.Rg, TipoDePii.Senha, TipoDePii.DadoDeSaude],
            entrada.Tipos.OrderBy(t => t));

        var eventoDeLog = Assert.Single(eventos, e => e.Sink == Sink.Log);
        Assert.Equal(AcaoDeSeguranca.Bloqueado, eventoDeLog.Acao);
        Assert.Contains("ContratoDoAppService", eventoDeLog.Onde);

        var eventoDeBanco = Assert.Single(eventos, e => e.Sink == Sink.Banco);
        Assert.Equal("Resposta.Observacao", eventoDeBanco.Onde);
        Assert.Contains(TipoDePii.Senha, eventoDeBanco.Tipos);

        Assert.DoesNotContain(eventos, e => e.Sink == Sink.RespostaHttp);

        // Nenhum evento carrega o dado.
        var tudoDosEventos = string.Join('|', eventos.Select(e => $"{e.DescricaoTecnica}|{e.Onde}"));
        foreach (var valor in new[] { Cpf, Rg, Senha, Saude })
        {
            Assert.DoesNotContain(valor, tudoDosEventos);
        }
    }

    [Fact]
    public async Task Cenario1b_mesmo_fluxo_pela_rota_da_coordenacao_mascara_o_log_e_o_banco()
    {
        var idLocal = $"taint-{Guid.NewGuid():N}";

        var resposta = await _cliente.EnviarAsync("/api/chamadas/2/respostas", new
        {
            idLocal,
            ruaId = 6,
            situacao = "PrecisoDeAjuda",
            observacao = $"Senha é {Senha} e a Sra. Maria Souza é {Saude}"
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync();
        var noBanco = LerObservacaoDoBanco(idLocal);
        var log = string.Join('\n', fabrica.Captura.Linhas);

        Assert.DoesNotContain("Maria Souza", noBanco + corpo + log);
        Assert.DoesNotContain(Senha, noBanco + corpo + log);
        Assert.Contains("[nome oculto]", noBanco);
        Assert.Contains(fabrica.Captura.Linhas, l => l.Contains("Pedido de ajuda na rua 6") && l.Contains("[nome oculto]"));
    }

    [Fact]
    public async Task Cenario2_chaves_extras_do_json_sao_marcadas_na_entrada()
    {
        var antes = UltimoIdDeEvento();

        // O exemplo do professor: o corpo traz campos que a API nem conhece.
        var resposta = await _cliente.PostAsync(
            "/api/chamadas/2/respostas",
            new StringContent(
                $$"""{"idLocal":"taint-{{Guid.NewGuid():N}}","ruaId":6,"situacao":"TudoBem","documento":"{{Cpf}}","senha":"{{Senha}}"}""",
                Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var entrada = Assert.Single(EventosDepoisDe(antes), e => e.Etapa == EtapaDoEvento.Entrada);
        Assert.Equal(AcaoDeSeguranca.Marcado, entrada.Acao);
        Assert.Equal("POST /api/chamadas/{id}/respostas", entrada.Onde);
        Assert.Equal([TipoDePii.Cpf, TipoDePii.Senha], entrada.Tipos.OrderBy(t => t));
        Assert.Equal(2, entrada.Quantidade);
        Assert.Equal(Severidade.Alta, entrada.Severidade);
        Assert.DoesNotContain(Senha, string.Join('\n', fabrica.Captura.Linhas));
    }

    [Fact]
    public async Task Pedido_de_ajuda_com_emoji_continua_passando_e_sai_mascarado()
    {
        var idLocal = $"taint-{Guid.NewGuid():N}";

        var resposta = await _cliente.PostAsJsonAsync("/chamadas", new
        {
            id = idLocal,
            ruaId = "6",
            status = "preciso_ajuda",
            pessoas = 1,
            observacao = $"Água subindo 😱🙏 a vizinha é {Saude}",
            criadaEm = Contrato.ComoNoApp(DateTime.UtcNow)
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal("Água subindo 😱🙏 a vizinha é [dado de saúde oculto]", LerObservacaoDoBanco(idLocal));
    }

    [Fact]
    public async Task Entrada_sem_dado_sensivel_nao_gera_evento_e_whatsapp_e_isento()
    {
        var antes = UltimoIdDeEvento();

        var resposta = await _cliente.PostAsJsonAsync("/api/chamadas/2/respostas", new
        {
            idLocal = $"taint-{Guid.NewGuid():N}",
            ruaId = 6,
            situacao = "TudoBem",
            observacao = "Tudo tranquilo na Rua Dona Ana",
            whatsapp = "11912345678"
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Empty(EventosDepoisDe(antes));
    }

    [Fact]
    public async Task Json_invalido_passa_sem_marcar_e_o_model_binding_responde_400()
    {
        var antes = UltimoIdDeEvento();

        var resposta = await _cliente.PostAsync("/chamadas", new StringContent("""{"id": "x", "observacao": CPF 123.456.789-00""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Empty(EventosDepoisDe(antes));
    }

    [Fact]
    public async Task Cenario3_dado_legado_no_banco_sai_mascarado_na_resposta_e_gera_evento()
    {
        var idLocal = $"legado-{Guid.NewGuid():N}";
        var criada = await _cliente.EnviarAsync("/api/chamadas/2/respostas", new { idLocal, ruaId = 6, situacao = "AguaSubindo" });
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);

        // Dado "antigo", gravado por fora da API (SQL cru): o interceptor do banco nunca viu.
        using (var conexao = new SqliteConnection($"Data Source={fabrica.ArquivoDoBanco}"))
        {
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText = "UPDATE Respostas SET Observacao = $obs WHERE IdLocal = $id";
            comando.Parameters.AddWithValue("$obs", $"CPF {Cpf} e a vizinha {Saude}");
            comando.Parameters.AddWithValue("$id", idLocal);
            Assert.Equal(1, comando.ExecuteNonQuery());
        }

        var antes = UltimoIdDeEvento();
        var lista = await _cliente.GetAsync("/api/chamadas/2/respostas");
        var corpo = await lista.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        Assert.DoesNotContain(Cpf, corpo);
        Assert.DoesNotContain(Saude, corpo);

        using var json = JsonDocument.Parse(corpo);
        var observacao = json.RootElement.EnumerateArray()
            .Single(r => r.GetProperty("idLocal").GetString() == idLocal)
            .GetProperty("observacao").GetString();
        Assert.Equal("CPF ***.456.***-00 e a vizinha [dado de saúde oculto]", observacao);

        var evento = Assert.Single(EventosDepoisDe(antes));
        Assert.Equal((EtapaDoEvento.Saida, Sink.RespostaHttp, AcaoDeSeguranca.Bloqueado), (evento.Etapa, evento.Sink, evento.Acao));
        Assert.Equal("GET /api/chamadas/{id}/respostas", evento.Onde);
        Assert.Contains(TipoDePii.Cpf, evento.Tipos);
        Assert.Contains(TipoDePii.DadoDeSaude, evento.Tipos);

        // Content-Length acompanha o corpo reescrito.
        if (lista.Content.Headers.ContentLength is { } tamanho)
        {
            Assert.Equal(Encoding.UTF8.GetByteCount(corpo), tamanho);
        }
    }

    [Fact]
    public async Task Cenario4_contratos_do_app_e_do_painel_nao_mudam_e_a_semente_nao_gera_evento()
    {
        // Fábrica própria: assim dá para afirmar que subir a API e semear o banco não registrou nada.
        using var outra = new FabricaComCapturaDeLog();
        using var cliente = outra.CreateClient();
        Assert.Empty(outra.Eventos.Listar(null, 10));

        foreach (var caminho in new[] { "/situacao", "/api/admin/instantaneo", "/avisos?codigoIbge=3550308", "/regua", "/ruas/1/plano", "/api/chamadas/2/registro" })
        {
            var resposta = await cliente.GetAsync(caminho);
            var bytes = await resposta.Content.ReadAsByteArrayAsync();

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            Assert.DoesNotContain("oculto", Encoding.UTF8.GetString(bytes));
            if (resposta.Content.Headers.ContentLength is { } tamanho)
            {
                Assert.Equal(bytes.Length, tamanho);
            }

            using var json = JsonDocument.Parse(bytes); // continua JSON válido
        }

        Assert.Empty(outra.Eventos.Listar(null, 10));
        Assert.DoesNotContain(outra.Captura.Linhas, l => l.Contains("oculto"));
    }
}

using System.Text.Json;
using Elo.Application.Avisos;
using Elo.Application.Comum;
using Elo.Infrastructure;
using Elo.Infrastructure.Avisos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using static Elo.Tests.Fixtures;

namespace Elo.Tests.Unitarios.Infraestrutura;

/// <summary>Leitura da resposta do INMET, com os mesmos casos dos testes do app (src/services/inmet.test.ts).</summary>
public class LeitorDeAvisosDoInmetTests
{
    private static IReadOnlyList<AvisoDoInmet> Ler(string json)
    {
        using var documento = JsonDocument.Parse(json);
        return LeitorDeAvisosDoInmet.Ler(documento.RootElement);
    }

    [Fact]
    public void Le_a_resposta_real_sem_poligono_nem_icone()
    {
        var avisos = AvisosDoInmet();

        Assert.Equal(4, avisos.Count);
        var primeiro = avisos[0];
        Assert.Equal(("56273", "Tempestade", "Perigo", 2, "hoje"), (primeiro.Id, primeiro.Titulo, primeiro.Severidade, primeiro.Gravidade, primeiro.Quando));
        Assert.Equal("2026-11-05T00:01:00-03:00", primeiro.Inicio);
        Assert.Equal("2026-11-05T23:59:00-03:00", primeiro.Fim);
        Assert.Contains("3550308", primeiro.Municipios);
        Assert.StartsWith("Chuva entre", primeiro.Riscos[0]);
        Assert.True(primeiro.Instrucoes.Count > 1);
        Assert.Equal("futuro", avisos[3].Quando);
    }

    [Fact]
    public void Lista_do_Python_em_texto_vira_lista_e_o_apostrofo_fica()
    {
        var aviso = Assert.Single(Ler("""
            {"hoje":[{"id":"1","descricao":"Chuvas","severidade":"Perigo","inicio":"2026-11-05 10:00","fim":"2026-11-05 23:59",
            "geocodes":"3550308","riscos":"Texto sem lista","instrucoes":"[\"Água d'alto nível\", 'B']"}],"futuro":[]}
            """));

        Assert.Equal(["Texto sem lista"], aviso.Riscos);
        Assert.Equal(["Água d'alto nível", "B"], aviso.Instrucoes);
    }

    [Fact]
    public void Encerrado_pode_vir_como_texto_ou_booleano()
    {
        var avisos = Ler("""
            {"hoje":[
              {"id":"a","geocodes":"3550308","encerrado":"True"},
              {"id":"b","geocodes":"3550308","encerrado":true},
              {"id":"c","geocodes":"3550308","encerrado":"False"}
            ]}
            """);

        Assert.Equal([true, true, false], avisos.Select(aviso => aviso.Encerrado));
    }

    [Theory]
    [InlineData("Grande Perigo", "Grande Perigo", 3)]
    [InlineData("GRANDE PERIGO", "Grande Perigo", 3)]
    [InlineData("Perigo", "Perigo", 2)]
    [InlineData("perígo", "Perigo", 2)]
    [InlineData("Perigo Potencial", "Perigo Potencial", 1)]
    [InlineData("???", "Perigo Potencial", 1)]
    public void Severidade_desconhecida_vira_perigo_potencial(string texto, string severidade, int gravidade)
    {
        Assert.Equal((severidade, gravidade), LeitorDeAvisosDoInmet.LerSeveridade(texto));
    }

    [Theory]
    [InlineData("2026-11-05 00:01", "2026-11-05T00:01:00-03:00")]
    [InlineData("2026-11-05T14:30", "2026-11-05T14:30:00-03:00")]
    [InlineData("data estranha", "data estranha")]
    public void Data_de_Brasilia_sem_fuso_vira_ISO(string texto, string esperado)
    {
        Assert.Equal(esperado, LeitorDeAvisosDoInmet.ParaIso(texto));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"hoje":"x","futuro":[null,3]}""")]
    public void Nao_quebra_com_lixo(string json)
    {
        Assert.Empty(Ler(json));
    }
}

/// <summary>Cache dos avisos: baixa pouco, aguenta o INMET fora do ar.</summary>
public class FonteDeAvisosEmCacheTests
{
    private static readonly IReadOnlyList<AvisoDoInmet> Lista = [Aviso("1", "3550308")];

    private readonly Mock<IFonteDeAvisos> _origem = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 9, 18, 0, 0, TimeSpan.Zero));

    private FonteDeAvisosEmCache CriarCache() => new(
        _origem.Object,
        Options.Create(new InmetOptions { IdadeMaximaEmMinutos = 10 }),
        _relogio,
        NullLogger<FonteDeAvisosEmCache>.Instance);

    [Fact]
    public async Task Baixa_uma_vez_e_serve_do_cache_enquanto_a_lista_e_nova()
    {
        _origem.Setup(o => o.ObterAvisosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Lista);
        using var cache = CriarCache();

        await cache.ObterAvisosAsync();
        await cache.ObterAvisosAsync();
        _relogio.Advance(TimeSpan.FromMinutes(9));
        await cache.ObterAvisosAsync();
        _origem.Verify(o => o.ObterAvisosAsync(It.IsAny<CancellationToken>()), Times.Once);

        _relogio.Advance(TimeSpan.FromMinutes(2)); // passou dos 10 minutos
        await cache.ObterAvisosAsync();
        _origem.Verify(o => o.ObterAvisosAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Com_o_INMET_fora_do_ar_segue_valendo_a_ultima_lista_boa()
    {
        _origem.SetupSequence(o => o.ObterAvisosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Lista)
            .ThrowsAsync(new HttpRequestException("INMET fora do ar"));
        using var cache = CriarCache();

        await cache.ObterAvisosAsync();
        _relogio.Advance(TimeSpan.FromMinutes(11));
        var depoisDaFalha = await cache.ObterAvisosAsync();
        var logoEmSeguida = await cache.ObterAvisosAsync(); // menos de 1 minuto depois da falha: nem tenta

        Assert.Same(Lista, depoisDaFalha);
        Assert.Same(Lista, logoEmSeguida);
        _origem.Verify(o => o.ObterAvisosAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Sem_lista_guardada_a_falha_vira_fonte_indisponivel()
    {
        _origem.Setup(o => o.ObterAvisosAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("INMET fora do ar"));
        using var cache = CriarCache();

        await Assert.ThrowsAsync<FonteIndisponivelException>(() => cache.ObterAvisosAsync());
    }

    [Fact]
    public void Decorator_quem_pede_a_fonte_de_avisos_recebe_o_cache()
    {
        var servicos = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSingleton(TimeProvider.System)
            .AddLogging()
            .AddInfrastructure();

        using var provedor = servicos.BuildServiceProvider();
        var fonte = provedor.GetRequiredService<IFonteDeAvisos>();

        // O cache embrulha o ClienteDoInmet e é a mesma instância que o serviço em segundo plano atualiza.
        Assert.IsType<FonteDeAvisosEmCache>(fonte);
        Assert.Same(fonte, provedor.GetRequiredService<ICacheDeAvisos>());
        Assert.IsAssignableFrom<IFonteDeAvisos>(provedor.GetRequiredService<ClienteDoInmet>());
    }
}

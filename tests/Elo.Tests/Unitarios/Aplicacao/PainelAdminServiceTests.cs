using Elo.Application.Avisos;
using Elo.Application.Comum;
using Elo.Application.PainelAdmin;
using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using static Elo.Tests.Fabrica;

namespace Elo.Tests.Unitarios.Aplicacao;

public class PainelAdminServiceTests
{
    private readonly Mock<IPainelAdminRepository> _repositorio = new();
    private readonly Mock<IAvisoService> _avisos = new();

    public PainelAdminServiceTests()
    {
        _repositorio.Setup(r => r.ListarSensoresComLeiturasAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repositorio.Setup(r => r.ListarChamadasComDetalhesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private PainelAdminService CriarServico() =>
        new(_repositorio.Object, _avisos.Object, new FakeTimeProvider(new DateTimeOffset(Inicio)), NullLogger<PainelAdminService>.Instance);

    private void Bairros(params Bairro[] bairros) =>
        _repositorio.Setup(r => r.ListarBairrosComRuasAsync(It.IsAny<CancellationToken>())).ReturnsAsync(bairros);

    private static Bairro NovoBairro(int id, string? uf, string? ibge, string nome = "Bairro") =>
        new Bairro($"{nome} {id}", "Cidade", "Sub", true, uf, ibge).ComId(id);

    private static AvisoDoAppDto Aviso(string id, int gravidade = 2, string inicio = "2026-11-09T09:00:00-03:00") =>
        new(id, "Chuvas Intensas", gravidade == 3 ? "Grande Perigo" : "Perigo", gravidade, inicio, "2026-11-10T09:00:00-03:00", "hoje", [], []);

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task Dias_fora_de_1_a_30_nao_chegam_ao_repositorio(int dias)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CriarServico().ObterInstantaneoAsync(dias));

        _repositorio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public async Task A_janela_de_dias_e_contada_a_partir_de_agora(int dias)
    {
        Bairros();

        await CriarServico().ObterInstantaneoAsync(dias);

        _repositorio.Verify(r => r.ListarChamadasComDetalhesAsync(Inicio, Inicio.AddDays(-dias), It.IsAny<CancellationToken>()));
        _repositorio.Verify(r => r.ListarSensoresComLeiturasAsync(Inicio.AddHours(-72), 500, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Mesmo_aviso_em_UFs_diferentes_aparece_uma_vez_com_as_duas_UFs()
    {
        Bairros(NovoBairro(1, "SP", "3550308"), NovoBairro(2, "rj", "3304557"), NovoBairro(3, "SP", "3550308"));
        _avisos.Setup(a => a.ListarDoMunicipioAsync("3550308", It.IsAny<CancellationToken>())).ReturnsAsync([Aviso("A"), Aviso("B", gravidade: 3)]);
        _avisos.Setup(a => a.ListarDoMunicipioAsync("3304557", It.IsAny<CancellationToken>())).ReturnsAsync([Aviso("A")]);

        var instantaneo = await CriarServico().ObterInstantaneoAsync(30);

        Assert.Equal(["B", "A"], instantaneo.Avisos.Select(aviso => aviso.Id)); // o mais grave primeiro
        Assert.Equal(["SP"], instantaneo.Avisos[0].Ufs);
        Assert.Equal(["RJ", "SP"], instantaneo.Avisos[1].Ufs);
        _avisos.Verify(a => a.ListarDoMunicipioAsync("3550308", It.IsAny<CancellationToken>()), Times.Once); // um municipio, uma consulta
    }

    [Fact]
    public async Task Datas_do_aviso_vao_para_UTC()
    {
        Bairros(NovoBairro(1, "SP", "3550308"));
        _avisos.Setup(a => a.ListarDoMunicipioAsync("3550308", It.IsAny<CancellationToken>())).ReturnsAsync([Aviso("A")]);

        var aviso = Assert.Single((await CriarServico().ObterInstantaneoAsync(30)).Avisos);

        Assert.Equal(new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc), aviso.Inicio);
        Assert.Equal(DateTimeKind.Utc, aviso.Inicio!.Value.Kind);
    }

    [Fact]
    public async Task Bairro_sem_IBGE_nao_consulta_avisos()
    {
        Bairros(NovoBairro(1, "SP", null));

        var instantaneo = await CriarServico().ObterInstantaneoAsync(30);

        Assert.Empty(instantaneo.Avisos);
        _avisos.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task INMET_fora_do_ar_deixa_os_avisos_vazios_sem_derrubar_a_rota()
    {
        Bairros(NovoBairro(1, "SP", "3550308"), NovoBairro(2, "RJ", "3304557"));
        _avisos.Setup(a => a.ListarDoMunicipioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FonteIndisponivelException("fora do ar"));

        var instantaneo = await CriarServico().ObterInstantaneoAsync(30);

        Assert.Empty(instantaneo.Avisos);
        Assert.Equal(2, instantaneo.Bairros.Count);
        _avisos.Verify(a => a.ListarDoMunicipioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once); // não insiste
    }

    [Fact]
    public async Task Leituras_vao_para_metros_da_mais_antiga_para_a_mais_nova()
    {
        Bairros();
        var sensor = new Sensor(1, "regua-teste", "Ponte", 120).ComId(7);
        _repositorio.Setup(r => r.ListarSensoresComLeiturasAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SensorComLeituras(sensor, 3, [new(142, Inicio), new(48.5, Inicio.AddMinutes(-10))])]);

        var item = Assert.Single((await CriarServico().ObterInstantaneoAsync(30)).Sensores);

        Assert.Equal(1.2, item.LimiteMetros);
        Assert.Equal(3, item.BairroId);
        Assert.Equal([0.485, 1.42], item.Leituras.Select(leitura => leitura.NivelMetros));
    }

    [Fact]
    public async Task Treino_vencido_conta_como_encerrado_e_o_aberto_nao_tem_data_de_fim()
    {
        Bairros();
        var rua = NovaRua(1);
        var vencido = NovoTreino(id: 1, inicio: Inicio.AddMinutes(-50), duracaoMinutos: 30);
        var aberto = NovaChamadaReal(id: 2, inicio: Inicio.AddMinutes(-5));
        Responder(aberto, rua, SituacaoRua.PrecisoDeAjuda, aposMinutos: 1, pessoas: 6);
        _repositorio.Setup(r => r.ListarChamadasComDetalhesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([aberto, vencido]);

        var chamadas = (await CriarServico().ObterInstantaneoAsync(30)).Chamadas;

        Assert.Null(chamadas[0].EncerradaEm);
        Assert.Equal("Acumulado de Chuva", chamadas[0].AvisoTitulo);
        Assert.Equal(6, Assert.Single(chamadas[0].Respostas).Pessoas);
        Assert.Equal(Inicio.AddMinutes(-20), chamadas[1].EncerradaEm);
        Assert.Null(chamadas[1].AvisoTitulo);
    }
}

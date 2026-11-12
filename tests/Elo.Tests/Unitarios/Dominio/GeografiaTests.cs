using Elo.Domain.Bairros;
using Elo.Domain.Comum;
using static Elo.Tests.Fabrica;

namespace Elo.Tests.Unitarios.Dominio;

/// <summary>Validações novas do painel de administração: UF, IBGE, coordenadas e o simulado sem o app.</summary>
public class BairroGeograficoTests
{
    private static Bairro Novo(
        string? uf = null,
        string? codigoIbge = null,
        double? latitude = null,
        double? longitude = null,
        double? percentualSemApp = null,
        DateTime? semAppMedidoEm = null) =>
        new("Jardim Teste", "São Paulo", "Sé", dadosSimulados: true, uf, codigoIbge, latitude, longitude, percentualSemApp, semAppMedidoEm);

    [Fact]
    public void Dados_geograficos_sao_opcionais()
    {
        var bairro = Novo();

        Assert.Null(bairro.Uf);
        Assert.Null(bairro.CodigoIbge);
        Assert.Null(bairro.Latitude);
        Assert.Null(bairro.Longitude);
        Assert.Null(bairro.PercentualSemApp);
        Assert.Null(bairro.SemAppMedidoEm);
    }

    [Fact]
    public void Dados_validos_sao_guardados_com_a_UF_em_maiusculas()
    {
        var medidoEm = new DateTime(2026, 9, 28, 5, 0, 0, DateTimeKind.Utc);

        var bairro = Novo(" sp ", "3550308", -23.47939, -46.40912, 31, medidoEm);

        Assert.Equal("SP", bairro.Uf);
        Assert.Equal("3550308", bairro.CodigoIbge);
        Assert.Equal(-23.47939, bairro.Latitude);
        Assert.Equal(-46.40912, bairro.Longitude);
        Assert.Equal(31, bairro.PercentualSemApp);
        Assert.Equal(medidoEm, bairro.SemAppMedidoEm);
    }

    [Theory]
    [InlineData("S")]
    [InlineData("SPP")]
    [InlineData("S1")]
    [InlineData("--")]
    public void UF_precisa_ter_2_letras(string uf)
    {
        Assert.Throws<RegraDeNegocioException>(() => Novo(uf: uf));
    }

    [Theory]
    [InlineData("355030")]
    [InlineData("35503080")]
    [InlineData("355030a")]
    [InlineData("3550 08")]
    public void Codigo_IBGE_precisa_ter_7_digitos(string codigo)
    {
        Assert.Throws<RegraDeNegocioException>(() => Novo(codigoIbge: codigo));
    }

    [Fact]
    public void Codigo_IBGE_mantem_zeros_a_esquerda()
    {
        Assert.Equal("0012345", Novo(codigoIbge: "0012345").CodigoIbge);
    }

    [Theory]
    [InlineData(-90.1, 0)]
    [InlineData(90.1, 0)]
    [InlineData(0, -180.1)]
    [InlineData(0, 180.1)]
    [InlineData(double.NaN, 0)]
    public void Latitude_e_longitude_fora_do_intervalo_sao_recusadas(double latitude, double longitude)
    {
        Assert.Throws<RegraDeNegocioException>(() => Novo(latitude: latitude, longitude: longitude));
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void Extremos_do_mapa_sao_aceitos(double latitude, double longitude)
    {
        var bairro = Novo(latitude: latitude, longitude: longitude);

        Assert.Equal(latitude, bairro.Latitude);
        Assert.Equal(longitude, bairro.Longitude);
    }

    [Fact]
    public void Latitude_sem_longitude_e_recusada()
    {
        Assert.Throws<RegraDeNegocioException>(() => Novo(latitude: -23.4));
        Assert.Throws<RegraDeNegocioException>(() => Novo(longitude: -46.4));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    public void Percentual_sem_o_app_fora_de_0_a_100_e_recusado(double percentual)
    {
        Assert.Throws<RegraDeNegocioException>(() => Novo(percentualSemApp: percentual));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31.5)]
    [InlineData(100)]
    public void Percentual_sem_o_app_de_0_a_100_e_aceito(double percentual)
    {
        Assert.Equal(percentual, Novo(percentualSemApp: percentual).PercentualSemApp);
    }

    [Fact]
    public void Data_do_simulado_sem_percentual_e_recusada()
    {
        Assert.Throws<RegraDeNegocioException>(() => Novo(semAppMedidoEm: Inicio));
    }

    [Fact]
    public void Data_local_do_simulado_vira_UTC()
    {
        var local = new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Local);

        var bairro = Novo(percentualSemApp: 31, semAppMedidoEm: local);

        Assert.Equal(DateTimeKind.Utc, bairro.SemAppMedidoEm!.Value.Kind);
        Assert.Equal(local.ToUniversalTime(), bairro.SemAppMedidoEm);
    }

    [Fact]
    public void Atualizacao_invalida_nao_muda_nada()
    {
        var bairro = Novo("SP", "3550308", -23.4, -46.4, 31, Inicio);

        Assert.Throws<RegraDeNegocioException>(() =>
            bairro.Atualizar("Outro nome", "Recife", "Sé", false, "PE", "2611606", 95, -34.8, 10, Inicio));

        Assert.Equal("Jardim Teste", bairro.Nome);
        Assert.Equal("SP", bairro.Uf);
        Assert.Equal(-23.4, bairro.Latitude);
        Assert.Equal(31, bairro.PercentualSemApp);
    }

    [Fact]
    public void Atualizar_sem_os_dados_novos_limpa_os_opcionais()
    {
        var bairro = Novo("SP", "3550308", -23.4, -46.4, 31, Inicio);

        bairro.Atualizar("Jardim Teste", "São Paulo", "Sé", true);

        Assert.Null(bairro.Uf);
        Assert.Null(bairro.CodigoIbge);
        Assert.Null(bairro.Latitude);
        Assert.Null(bairro.PercentualSemApp);
        Assert.Null(bairro.SemAppMedidoEm);
    }
}

public class RuaESensorGeograficosTests
{
    [Fact]
    public void Rua_nasce_sem_geografia()
    {
        var rua = NovaRua(1);

        Assert.Null(rua.Latitude);
        Assert.Null(rua.Longitude);
        Assert.Null(rua.Tracado);
    }

    [Fact]
    public void Rua_guarda_o_ponto_e_o_tracado_na_ordem()
    {
        var rua = NovaRua(1);
        Coordenada[] tracado = [new(-46.41, -23.48), new(-46.409, -23.481), new(-46.408, -23.482)];

        rua.DefinirGeografia(-23.481, -46.409, tracado);

        Assert.Equal(-23.481, rua.Latitude);
        Assert.Equal(-46.409, rua.Longitude);
        Assert.Equal(tracado, rua.Tracado);
    }

    [Fact]
    public void Tracado_vazio_ou_nulo_vira_sem_tracado()
    {
        var rua = NovaRua(1);
        rua.DefinirGeografia(null, null, [new(-46.41, -23.48), new(-46.409, -23.481)]);

        rua.DefinirGeografia(null, null, []);

        Assert.Null(rua.Tracado);
    }

    [Fact]
    public void Tracado_com_um_ponto_so_e_recusado()
    {
        var rua = NovaRua(1);

        Assert.Throws<RegraDeNegocioException>(() => rua.DefinirGeografia(null, null, [new(-46.41, -23.48)]));
        Assert.Null(rua.Tracado);
    }

    [Fact]
    public void Tracado_grande_demais_e_recusado()
    {
        var rua = NovaRua(1);
        var pontos = Enumerable.Range(0, 2001).Select(i => new Coordenada(-46.4, -23.4)).ToList();

        Assert.Throws<RegraDeNegocioException>(() => rua.DefinirGeografia(null, null, pontos));
    }

    [Fact]
    public void Rua_com_latitude_sem_longitude_e_recusada()
    {
        Assert.Throws<RegraDeNegocioException>(() => NovaRua(1).DefinirGeografia(-23.4, null, null));
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    public void Ponto_da_rua_fora_do_mapa_e_recusado(double latitude, double longitude)
    {
        Assert.Throws<RegraDeNegocioException>(() => NovaRua(1).DefinirGeografia(latitude, longitude, null));
    }

    [Theory]
    [InlineData(-46.4, -90.5)]
    [InlineData(-181, -23.4)]
    public void Coordenada_do_tracado_fora_do_mapa_e_recusada(double longitude, double latitude)
    {
        Assert.Throws<RegraDeNegocioException>(() => new Coordenada(longitude, latitude));
    }

    [Fact]
    public void Coordenadas_iguais_sao_o_mesmo_valor()
    {
        Assert.Equal(new Coordenada(-46.4, -23.4), new Coordenada(-46.4, -23.4));
    }

    [Fact]
    public void Sensor_guarda_e_limpa_a_localizacao()
    {
        var sensor = new Elo.Domain.Sensores.Sensor(1, "regua-teste", "Ponte", 120);

        sensor.DefinirLocalizacao(-23.47, -46.41);
        Assert.Equal(-23.47, sensor.Latitude);
        Assert.Equal(-46.41, sensor.Longitude);

        sensor.DefinirLocalizacao(null, null);
        Assert.Null(sensor.Latitude);
        Assert.Null(sensor.Longitude);
    }

    [Fact]
    public void Sensor_com_localizacao_incompleta_ou_fora_do_mapa_e_recusado()
    {
        var sensor = new Elo.Domain.Sensores.Sensor(1, "regua-teste", "Ponte", 120);

        Assert.Throws<RegraDeNegocioException>(() => sensor.DefinirLocalizacao(-23.47, null));
        Assert.Throws<RegraDeNegocioException>(() => sensor.DefinirLocalizacao(-23.47, 200));
    }
}

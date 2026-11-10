using Elo.Domain.Comum;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;
using static Elo.Tests.Fabrica;

namespace Elo.Tests.Unitarios.Dominio;

public class LiderancaTests
{
    [Fact]
    public void WhatsApp_e_guardado_so_com_digitos()
    {
        var lideranca = new Lideranca("Dona Cida", "+55 (11) 91234-5678");

        Assert.Equal("5511912345678", lideranca.WhatsApp);
    }

    [Fact]
    public void WhatsApp_sem_DDI_e_DDD_e_recusado()
    {
        Assert.Throws<RegraDeNegocioException>(() => new Lideranca("Dona Cida", "91234-5678"));
    }

    [Fact]
    public void Link_do_WhatsApp_leva_a_mensagem_codificada()
    {
        var link = new Lideranca("Dona Cida", "5511912345678").GerarLinkWhatsApp("Como está a rua?");

        Assert.Equal("https://wa.me/5511912345678?text=Como%20est%C3%A1%20a%20rua%3F", link);
    }

    [Fact]
    public void Value_object_compara_por_valor()
    {
        var primeira = new Lideranca("Dona Cida", "5511912345678");
        var segunda = new Lideranca("Dona Cida", "+55 11 91234-5678");

        Assert.Equal(primeira, segunda);
        Assert.NotSame(primeira, segunda);
    }
}

public class PlanoDaRuaTests
{
    [Fact]
    public void Itens_repetidos_e_vazios_sao_removidos()
    {
        var plano = new PlanoDaRua("Rota", "Praça", "Escola", ["Documentos", "documentos", " ", "Remédios"], Inicio);

        Assert.Equal(["Documentos", "Remédios"], plano.ItensParaLevar);
    }

    [Fact]
    public void Plano_com_itens_demais_e_recusado()
    {
        var itens = Enumerable.Range(1, PlanoDaRua.MaximoDeItens + 1).Select(i => $"Item {i}");

        Assert.Throws<RegraDeNegocioException>(() => new PlanoDaRua("Rota", "Praça", "Escola", itens, Inicio));
    }

    [Fact]
    public void Planos_com_os_mesmos_itens_sao_iguais_mesmo_vindo_de_listas_diferentes()
    {
        var primeiro = new PlanoDaRua("Rota", "Praça", "Escola", ["Documentos", "Remédios"], Inicio);
        var segundo = new PlanoDaRua("Rota", "Praça", "Escola", new List<string> { "Documentos", "Remédios" }, Inicio);
        var outraOrdem = new PlanoDaRua("Rota", "Praça", "Escola", ["Remédios", "Documentos"], Inicio);

        Assert.Equal(primeiro, segundo);
        Assert.Equal(primeiro.GetHashCode(), segundo.GetHashCode());
        Assert.NotEqual(primeiro, outraOrdem);
    }

    [Fact]
    public void Lista_de_itens_nao_pode_ser_alterada_por_fora()
    {
        var plano = new PlanoDaRua("Rota", "Praça", "Escola", ["Documentos"], Inicio);

        var lista = Assert.IsAssignableFrom<ICollection<string>>(plano.ItensParaLevar);
        Assert.True(lista.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => lista.Add("Celular"));
        Assert.Equal(["Documentos"], plano.ItensParaLevar);
    }
}

public class SensorTests
{
    private static Sensor NovoSensor(double limite = 40) => new Sensor(1, "esp32-teste", "Ponte do córrego", limite).ComId(1);

    [Theory]
    [InlineData(39.9, false)]
    [InlineData(40, true)]
    [InlineData(55, true)]
    public void Leitura_no_limite_ou_acima_vira_alerta(double nivel, bool esperado)
    {
        var leitura = NovoSensor(limite: 40).RegistrarLeitura(nivel, Inicio, Inicio);

        Assert.Equal(esperado, leitura.AcimaDoLimite);
        Assert.Equal(40, leitura.LimiteNaHoraCm);
    }

    [Fact]
    public void Codigo_do_sensor_fica_em_minusculas_como_o_reguaId_do_ESP32()
    {
        Assert.Equal("regua-teste", new Sensor(1, " REGUA-Teste ", "Ponte", 120).Codigo);
    }

    [Fact]
    public void Sensor_desativado_nao_recebe_leitura()
    {
        var sensor = NovoSensor();
        sensor.Atualizar(sensor.Local, sensor.LimiteAlertaCm, ativo: false);

        Assert.Throws<RegraDeNegocioException>(() => sensor.RegistrarLeitura(10, Inicio, Inicio));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(501)]
    public void Nivel_fora_da_faixa_do_sensor_e_recusado(double nivel)
    {
        Assert.Throws<RegraDeNegocioException>(() => NovoSensor().RegistrarLeitura(nivel, Inicio, Inicio));
    }
}

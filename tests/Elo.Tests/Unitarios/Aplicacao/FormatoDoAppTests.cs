using Elo.Application.ContratoDoApp;
using Elo.Domain.Chamadas;

namespace Elo.Tests.Unitarios.Aplicacao;

/// <summary>A tradução entre o vocabulário do app e o do domínio.</summary>
public class FormatoDoAppTests
{
    [Theory]
    [InlineData(SituacaoRua.TudoBem, "tudo_bem")]
    [InlineData(SituacaoRua.AguaSubindo, "agua_subindo")]
    [InlineData(SituacaoRua.PrecisoDeAjuda, "preciso_ajuda")]
    public void Status_vai_e_volta_no_vocabulario_do_app(SituacaoRua situacao, string status)
    {
        Assert.Equal(status, FormatoDoApp.Status(situacao));
        Assert.Equal(situacao, FormatoDoApp.Situacao(status));
    }

    [Theory]
    [InlineData("inundado")]
    [InlineData("TUDO_BEM")]
    [InlineData("")]
    [InlineData(null)]
    public void Status_fora_do_contrato_nao_vira_situacao(string? status)
    {
        Assert.Null(FormatoDoApp.Situacao(status));
    }

    [Fact]
    public void Data_sai_igual_ao_toISOString_do_JavaScript()
    {
        var momento = new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc);

        Assert.Equal("2026-11-05T15:00:00.000Z", FormatoDoApp.Data(momento));
        Assert.Equal("2026-11-05T15:00:00.123Z", FormatoDoApp.Data(momento.AddMilliseconds(123)));
    }

    [Theory]
    [InlineData("7", 7)]
    [InlineData("rua-3", null)]
    [InlineData("-1", null)]
    [InlineData("0", null)]
    [InlineData(null, null)]
    public void Id_da_rua_chega_como_texto(string? texto, int? esperado)
    {
        Assert.Equal(esperado, FormatoDoApp.LerId(texto));
    }

    [Fact]
    public void Centimetros_viram_metros_com_duas_casas()
    {
        Assert.Equal(1.42, FormatoDoApp.Metros(142));
        Assert.Equal(1.2, FormatoDoApp.Metros(120));
    }
}

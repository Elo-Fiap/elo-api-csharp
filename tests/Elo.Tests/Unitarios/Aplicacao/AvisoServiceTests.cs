using Elo.Application.Avisos;
using Elo.Domain.Comum;
using static Elo.Tests.Fixtures;

namespace Elo.Tests.Unitarios.Aplicacao;

/// <summary>O filtro de avisos por município, com as regras do app.</summary>
public class AvisoServiceTests
{
    private const string SaoPaulo = "3550308";

    private static AvisoService Servico(IReadOnlyList<AvisoDoInmet> avisos) => new(new FonteDeAvisosFalsa(() => avisos));

    [Fact]
    public async Task Mantem_so_os_avisos_do_municipio_do_mais_grave_ao_mais_leve()
    {
        var avisos = await Servico(AvisosDoInmet()).ListarDoMunicipioAsync(SaoPaulo);

        Assert.Equal(["56273", "56272", "56278"], avisos.Select(aviso => aviso.Id));
        Assert.Equal(2, avisos[0].Gravidade);
        Assert.Equal("futuro", avisos[2].Quando);
    }

    [Fact]
    public async Task Nao_confunde_codigos_parecidos()
    {
        var avisos = await Servico([Aviso("1", "355030,35503089,13550308")]).ListarDoMunicipioAsync(SaoPaulo);

        Assert.Empty(avisos);
    }

    [Fact]
    public async Task Ignora_encerrados_e_nao_repete_o_aviso_de_hoje_no_futuro()
    {
        var avisos = await Servico(
        [
            Aviso("igual", SaoPaulo, quando: "hoje"),
            Aviso("igual", SaoPaulo, quando: "futuro"),
            Aviso("encerrado", SaoPaulo, encerrado: true)
        ]).ListarDoMunicipioAsync(SaoPaulo);

        var aviso = Assert.Single(avisos);
        Assert.Equal(("igual", "hoje"), (aviso.Id, aviso.Quando));
    }

    [Fact]
    public async Task Grande_perigo_vem_antes_e_no_empate_o_que_comeca_antes()
    {
        var avisos = await Servico(
        [
            Aviso("perigo-tarde", SaoPaulo, gravidade: 2, inicio: "2026-11-05T15:00:00-03:00"),
            Aviso("potencial", SaoPaulo, gravidade: 1),
            Aviso("perigo-cedo", SaoPaulo, gravidade: 2, inicio: "2026-11-05T08:00:00-03:00"),
            Aviso("grande", SaoPaulo, gravidade: 3)
        ]).ListarDoMunicipioAsync(SaoPaulo);

        Assert.Equal(["grande", "perigo-cedo", "perigo-tarde", "potencial"], avisos.Select(aviso => aviso.Id));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abcdefg")]
    [InlineData("")]
    public async Task Codigo_IBGE_invalido_e_recusado(string codigo)
    {
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico([]).ListarDoMunicipioAsync(codigo));
    }
}

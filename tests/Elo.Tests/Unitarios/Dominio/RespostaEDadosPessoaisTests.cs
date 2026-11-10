using Elo.Domain.Chamadas;
using Elo.Domain.Comum;
using static Elo.Tests.Fabrica;

namespace Elo.Tests.Unitarios.Dominio;

public class RespostaTests
{
    [Fact]
    public void Pedir_ajuda_sem_dizer_quantas_pessoas_e_recusado()
    {
        var erro = Assert.Throws<RegraDeNegocioException>(() =>
            Responder(NovaChamadaReal(), NovaRua(1), SituacaoRua.PrecisoDeAjuda, 5, pessoas: null));

        Assert.Contains("quantas pessoas", erro.Message);
    }

    [Fact]
    public void Quantidade_de_pessoas_so_vale_para_pedido_de_ajuda()
    {
        Assert.Throws<RegraDeNegocioException>(() =>
            Responder(NovaChamadaReal(), NovaRua(1), SituacaoRua.TudoBem, 5, pessoas: 3));
    }

    [Fact]
    public void Observacao_e_salva_com_cpf_e_telefone_mascarados()
    {
        var resposta = NovaChamadaReal().RegistrarResposta(
            Guid.NewGuid(), NovaRua(1), SituacaoRua.PrecisoDeAjuda, 2,
            "Liguem para 11 91234-5678, CPF 123.456.789-09",
            CanalResposta.App, Inicio.AddMinutes(5), Inicio.AddMinutes(5));

        Assert.Equal("Liguem para [telefone removido], CPF [CPF removido]", resposta.Observacao);
    }

    [Fact]
    public void Sem_id_local_a_resposta_ganha_um_novo()
    {
        var resposta = NovaChamadaReal().RegistrarResposta(
            Guid.Empty, NovaRua(1), SituacaoRua.TudoBem, null, null,
            CanalResposta.Sms, Inicio.AddMinutes(5), Inicio.AddMinutes(5));

        Assert.NotEqual(Guid.Empty, resposta.IdLocal);
        Assert.Equal(CanalResposta.Sms, resposta.Canal);
    }
}

public class FiltroDeDadosPessoaisTests
{
    [Theory]
    [InlineData("CPF 123.456.789-09", "CPF [CPF removido]")]
    [InlineData("cpf 12345678909 da moradora", "cpf [CPF removido] da moradora")]
    [InlineData("Liga (11) 91234-5678", "Liga [telefone removido]")]
    [InlineData("+55 11 91234-5678 urgente", "[telefone removido] urgente")]
    [InlineData("fixo 2345-6789", "fixo [telefone removido]")]
    [InlineData("escreve para ana.souza@exemplo.com.br", "escreve para [e-mail removido]")]
    public void Mascara_dados_pessoais(string entrada, string esperado)
    {
        Assert.Equal(esperado, FiltroDeDadosPessoais.Aplicar(entrada));
        Assert.True(FiltroDeDadosPessoais.ContemDadoPessoal(entrada));
    }

    [Theory]
    [InlineData("Água na altura do joelho, 5 pessoas no térreo.")]
    [InlineData("Rua 25 de Março, número 1500")]
    [InlineData("O nível subiu 30 cm em 10 minutos")]
    public void Nao_altera_texto_sem_dado_pessoal(string texto)
    {
        Assert.Equal(texto, FiltroDeDadosPessoais.Aplicar(texto));
        Assert.False(FiltroDeDadosPessoais.ContemDadoPessoal(texto));
    }
}

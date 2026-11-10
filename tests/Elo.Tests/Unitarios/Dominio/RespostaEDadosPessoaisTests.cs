using Elo.Domain.Chamadas;
using Elo.Domain.Comum;
using static Elo.Tests.Fabrica;

namespace Elo.Tests.Unitarios.Dominio;

public class RespostaTests
{
    [Fact]
    public void Pedir_ajuda_sem_dizer_quantas_pessoas_e_aceito()
    {
        // O contrato do app permite "pessoas" nulo: a liderança pode não saber quantas pessoas são.
        var resposta = Responder(NovaChamadaReal(), NovaRua(1), SituacaoRua.PrecisoDeAjuda, 5, pessoas: null);

        Assert.True(resposta.PediuAjuda);
        Assert.Null(resposta.Pessoas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Quantidade_de_pessoas_fora_da_faixa_e_recusada(int pessoas)
    {
        var erro = Assert.Throws<RegraDeNegocioException>(() =>
            Responder(NovaChamadaReal(), NovaRua(1), SituacaoRua.PrecisoDeAjuda, 5, pessoas));

        Assert.Contains("de 1 a 500", erro.Message);
    }

    [Fact]
    public void Observacao_com_mais_de_200_caracteres_e_recusada()
    {
        Assert.Throws<RegraDeNegocioException>(() => NovaChamadaReal().RegistrarResposta(
            "id-longo", NovaRua(1), SituacaoRua.TudoBem, null, new string('a', 201),
            CanalResposta.App, Inicio.AddMinutes(5), Inicio.AddMinutes(5)));
    }

    [Fact]
    public void Observacao_e_salva_com_cpf_e_telefone_mascarados()
    {
        var resposta = NovaChamadaReal().RegistrarResposta(
            "id-mascara", NovaRua(1), SituacaoRua.PrecisoDeAjuda, 2,
            "Liguem para 11 91234-5678, CPF 123.456.789-09",
            CanalResposta.App, Inicio.AddMinutes(5), Inicio.AddMinutes(5));

        Assert.Equal("Liguem para [telefone removido], CPF [CPF removido]", resposta.Observacao);
    }

    [Fact]
    public void Id_criado_no_app_e_guardado_como_texto()
    {
        var resposta = NovaChamadaReal().RegistrarResposta(
            "m3k9x2a-7f4q1b2c", NovaRua(1), SituacaoRua.TudoBem, null, null,
            CanalResposta.App, Inicio.AddMinutes(5), Inicio.AddMinutes(5));

        Assert.Equal("m3k9x2a-7f4q1b2c", resposta.IdLocal);
    }

    [Fact]
    public void Sem_id_local_a_resposta_ganha_um_novo()
    {
        var resposta = NovaChamadaReal().RegistrarResposta(
            null, NovaRua(1), SituacaoRua.TudoBem, null, null,
            CanalResposta.Sms, Inicio.AddMinutes(5), Inicio.AddMinutes(5));

        Assert.False(string.IsNullOrWhiteSpace(resposta.IdLocal));
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

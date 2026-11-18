using Elo.Seguranca.Pii;

namespace Elo.Tests.Unitarios.Seguranca;

public class MotorDePiiTests
{
    private readonly MotorDePii _motor = new();

    private ResultadoDaAnalise Livre(string texto) => _motor.Analisar(texto, ModoDeAnalise.TextoLivre);
    private ResultadoDaAnalise Campo(string texto) => _motor.Analisar(texto, ModoDeAnalise.CampoEstruturado);

    private static TipoDePii[] Tipos(ResultadoDaAnalise r) => [.. r.Achados.Select(a => a.Tipo)];

    // ---------- CPF ----------

    [Fact]
    public void Cpf_formatado_do_exemplo_do_professor_e_achado_mesmo_com_digito_invalido_e_mantem_o_miolo_e_o_dv()
    {
        var r = Campo("CPF 123.456.789-00 da dona");

        Assert.Equal([TipoDePii.Cpf], Tipos(r));
        Assert.Equal("CPF ***.456.***-00 da dona", r.TextoMascarado);
    }

    [Fact]
    public void Cpf_sem_formatacao_so_conta_com_digito_verificador_valido()
    {
        Assert.Equal([TipoDePii.Cpf], Tipos(Campo("meu 12345678909 ok")));
        Assert.False(Campo("meu 12345678900 ok").TemPii);
        Assert.False(Campo("11111111111").TemPii);
    }

    [Fact]
    public void Telefone_de_11_digitos_nao_vira_cpf()
    {
        var r = Campo("ligue 11912345678");

        Assert.Equal([TipoDePii.Telefone], Tipos(r));
        Assert.Equal("ligue (**) *****-5678", r.TextoMascarado);
    }

    // ---------- RG ----------

    [Theory]
    [InlineData("RG 12.345.678-9")]
    [InlineData("rg: 123456789")]
    [InlineData("RG nº 1234567")]
    public void Rg_e_achado_com_a_palavra_ou_formatado(string texto)
    {
        var r = Campo(texto);

        Assert.Equal([TipoDePii.Rg], Tipos(r));
        Assert.Contains("[RG oculto]", r.TextoMascarado);
    }

    [Fact]
    public void Rg_formatado_sozinho_e_achado_e_texto_comum_com_rg_nao()
    {
        Assert.Equal([TipoDePii.Rg], Tipos(Campo("documento 12.345.678-X")));
        Assert.False(Campo("RG oculto").TemPii);
        Assert.False(Campo("Rua Org 12345").TemPii);
    }

    // ---------- Cartão SUS ----------

    [Theory]
    [InlineData("898 0000 0000 0002")]
    [InlineData("898000000000002")]
    [InlineData("123456789010000")]
    public void Cartao_sus_valido_e_achado(string cns)
    {
        var r = Campo($"cns {cns}.");

        Assert.Equal([TipoDePii.CartaoSus], Tipos(r));
        Assert.Equal("cns [cartão SUS oculto].", r.TextoMascarado);
    }

    [Fact]
    public void Cartao_sus_com_digito_invalido_so_conta_com_a_palavra()
    {
        Assert.False(Campo("numero 898000000000001").TemPii);
        Assert.Equal([TipoDePii.CartaoSus], Tipos(Campo("cartão SUS 898000000000001")));
    }

    // ---------- E-mail e telefone ----------

    [Fact]
    public void Email_mantem_a_primeira_letra_e_o_dominio()
    {
        var r = Campo("fale com joao.silva@gmail.com agora");

        Assert.Equal([TipoDePii.Email], Tipos(r));
        Assert.Equal("fale com j***@gmail.com agora", r.TextoMascarado);
    }

    [Theory]
    [InlineData("(11) 91234-5678")]
    [InlineData("11 912345678")]
    [InlineData("+55 11 91234-5678")]
    [InlineData("91234-5678")]
    public void Telefone_mantem_so_os_4_ultimos_digitos(string telefone)
    {
        var r = Campo($"tel {telefone}");

        Assert.Equal([TipoDePii.Telefone], Tipos(r));
        Assert.Equal("tel (**) *****-5678", r.TextoMascarado);
    }

    // ---------- Senha ----------

    [Fact]
    public void Senha_mascara_so_o_valor()
    {
        var r = Campo("senha: abc12345 e fim");

        Assert.Equal([TipoDePii.Senha], Tipos(r));
        Assert.Equal("senha: [senha oculta] e fim", r.TextoMascarado);
    }

    [Theory]
    [InlineData("a senha é Xy9z", "a senha é [senha oculta]")]
    [InlineData("password=hunter2", "password=[senha oculta]")]
    [InlineData("PIN 4321", "PIN [senha oculta]")]
    [InlineData("código de acesso: 99887", "código de acesso: [senha oculta]")]
    public void Senha_em_varias_formas(string texto, string esperado) =>
        Assert.Equal(esperado, Campo(texto).TextoMascarado);

    [Fact]
    public void Palavra_senha_sem_valor_de_verdade_nao_e_senha()
    {
        Assert.False(Campo("sem senha nenhuma").TemPii);
        Assert.False(Campo("pinheiros").TemPii);
    }

    // ---------- Saúde ----------

    [Fact]
    public void Cid_conta_nos_dois_modos()
    {
        Assert.Equal([TipoDePii.DadoDeSaude], Tipos(Campo("CID F32")));
        Assert.Equal("[dado de saúde oculto]", Livre("cid-b20").TextoMascarado);
        Assert.Equal([TipoDePii.DadoDeSaude], Tipos(Campo("CID: A15.0")));
        Assert.False(Livre("cidade A12").TemPii);
    }

    [Fact]
    public void Termos_de_saude_so_contam_em_texto_livre()
    {
        Assert.False(Campo("Plano para acamados").TemPii);

        var r = Livre("Há uma vizinha diabética e dois acamados");
        Assert.Equal([TipoDePii.DadoDeSaude, TipoDePii.DadoDeSaude], Tipos(r));
        Assert.Equal("Há uma vizinha [dado de saúde oculto] e dois [dado de saúde oculto]", r.TextoMascarado);
    }

    [Theory]
    [InlineData("Hipertensão e insulina")]
    [InlineData("HIV positivo")]
    [InlineData("precisa de oxigênio domiciliar")]
    [InlineData("usa marca-passo")]
    [InlineData("MORADOR CADEIRANTE")]
    [InlineData("criança autista")]
    [InlineData("tem Alzheimer")]
    public void Termos_de_saude_ignoram_acento_e_caixa(string texto) =>
        Assert.True(Livre(texto).TemPii);

    [Fact]
    public void Emoji_no_texto_nao_quebra_o_motor_nem_desloca_a_mascara()
    {
        var r = Livre("Água subindo 😱🌧️ minha mãe é diabética 🙏");

        Assert.Equal("Água subindo 😱🌧️ minha mãe é [dado de saúde oculto] 🙏", r.TextoMascarado);
    }

    [Theory]
    [InlineData("A saúde da região vai bem, o médico passou no hospital")]
    [InlineData("tomou o remédio, o doente melhorou")]
    [InlineData("Rua Dona Ana, 12")]
    public void Palavras_genericas_de_saude_nao_contam(string texto) =>
        Assert.False(Livre(texto).TemPii);

    // ---------- Nome ----------

    [Theory]
    [InlineData("meu nome é Maria Souza e preciso", "meu nome é [nome oculto] e preciso")]
    [InlineData("Me chamo José da Silva.", "Me chamo [nome oculto].")]
    [InlineData("nome: Ana Lima", "nome: [nome oculto]")]
    [InlineData("falar com a Sra. Joana", "falar com a Sra. [nome oculto]")]
    public void Nome_so_em_texto_livre_e_so_o_nome_e_mascarado(string texto, string esperado)
    {
        Assert.Equal(esperado, Livre(texto).TextoMascarado);
        Assert.DoesNotContain(Campo(texto).Achados, a => a.Tipo == TipoDePii.Nome);
    }

    [Fact]
    public void Rua_Dona_Ana_nao_vira_nome()
    {
        Assert.False(Livre("Rua Dona Ana, esquina com a Rua Seu Antônio").TemPii);
    }

    // ---------- Geral ----------

    [Fact]
    public void Varios_achados_no_mesmo_texto_sao_mascarados_do_fim_para_o_comeco_sem_embaralhar()
    {
        var r = Livre("CPF 123.456.789-00, RG 12.345.678-9 e senha: abc12345, uma diabética");

        Assert.Equal(
            "CPF ***.456.***-00, RG [RG oculto] e senha: [senha oculta], uma [dado de saúde oculto]",
            r.TextoMascarado);
        Assert.Equal(
            [TipoDePii.Cpf, TipoDePii.Rg, TipoDePii.Senha, TipoDePii.DadoDeSaude],
            Tipos(r));
    }

    [Fact]
    public void Achados_nao_se_sobrepoem_cpf_formatado_nao_vira_rg_nem_telefone()
    {
        var r = Campo("123.456.789-00");

        var achado = Assert.Single(r.Achados);
        Assert.Equal(TipoDePii.Cpf, achado.Tipo);
    }

    [Fact]
    public void Texto_ja_mascarado_nao_gera_achado_de_novo()
    {
        var original = Livre("CPF 123.456.789-00, RG 12.345.678-9, senha: abc12345, a vizinha diabética, meu nome é Maria Souza, j@x.com, 11912345678, CNS 898000000000002");
        Assert.True(original.TemPii);

        Assert.False(Livre(original.TextoMascarado).TemPii);
    }

    [Fact]
    public void Texto_vazio_ou_nulo_nao_tem_pii()
    {
        Assert.False(Livre("").TemPii);
        Assert.False(_motor.Analisar(null, ModoDeAnalise.TextoLivre).TemPii);
    }

    [Fact]
    public void Ignorar_contato_deixa_telefone_e_email_passarem()
    {
        var r = _motor.Analisar("SAMU 192 ou (11) 91234-5678 ou a@b.com", ModoDeAnalise.CampoEstruturado, ignorarContato: true);

        Assert.False(r.TemPii);
    }

    // ---------- Chaves ----------

    [Theory]
    [InlineData("cpf", TipoDePii.Cpf)]
    [InlineData("Documento", TipoDePii.Cpf)]
    [InlineData("rg", TipoDePii.Rg)]
    [InlineData("senha", TipoDePii.Senha)]
    [InlineData("Password", TipoDePii.Senha)]
    [InlineData("pin", TipoDePii.Senha)]
    [InlineData("token", TipoDePii.Senha)]
    [InlineData("laudo", TipoDePii.DadoDeSaude)]
    [InlineData("Diagnóstico", TipoDePii.DadoDeSaude)]
    [InlineData("condicao_de_saude", TipoDePii.DadoDeSaude)]
    [InlineData("cid", TipoDePii.DadoDeSaude)]
    [InlineData("doenca", TipoDePii.DadoDeSaude)]
    [InlineData("cartao-sus", TipoDePii.CartaoSus)]
    [InlineData("CNS", TipoDePii.CartaoSus)]
    [InlineData("email", TipoDePii.Email)]
    [InlineData("celular", TipoDePii.Telefone)]
    [InlineData("telefone", TipoDePii.Telefone)]
    public void Chave_diz_o_tipo_do_valor(string chave, TipoDePii esperado) =>
        Assert.Equal(esperado, _motor.ClassificarChave(chave));

    [Theory]
    [InlineData("nome")]
    [InlineData("observacao")]
    [InlineData("bairroId")]
    public void Chave_comum_nao_e_classificada(string chave) =>
        Assert.Null(_motor.ClassificarChave(chave));

    [Theory]
    [InlineData("whatsapp")]
    [InlineData("linkWhatsApp")]
    [InlineData("apelido")]
    [InlineData("id")]
    [InlineData("idLocal")]
    [InlineData("ruaId")]
    [InlineData("criadaEm")]
    public void Contato_da_lideranca_e_ids_tecnicos_sao_isentos(string chave) =>
        Assert.True(_motor.EhChaveIsenta(chave));

    [Theory]
    [InlineData("observacao", true)]
    [InlineData("Descricao", true)]
    [InlineData("relato", true)]
    [InlineData("nome", false)]
    [InlineData("local", false)]
    public void Chaves_de_texto_livre(string chave, bool esperado) =>
        Assert.Equal(esperado, _motor.EhChaveDeTextoLivre(chave));
}

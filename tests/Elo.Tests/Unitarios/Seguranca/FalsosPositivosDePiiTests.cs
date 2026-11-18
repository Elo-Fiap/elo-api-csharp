using Elo.Seguranca.Pii;

namespace Elo.Tests.Unitarios.Seguranca;

/// <summary>
/// Ids, GUIDs e caminhos de arquivo têm sequências de dígitos que, soltas, parecem telefone, CPF ou cartão do SUS.
/// Um falso positivo no log ("Banco pronto: .../elo-testes-3f25...db") deixaria o farol vermelho à toa.
/// </summary>
public class FalsosPositivosDePiiTests
{
    [Theory]
    [InlineData("Banco pronto: /tmp/elo-testes-a12345678b9c0d1e.db.")]
    [InlineData("Banco pronto: /tmp/elo-testes-ab12345678901cd.db.")]
    [InlineData("Banco pronto: /tmp/elo-testes-x898000000000002y.db.")]
    [InlineData("requisição req-9f12345678a trace_12345678_abc")]
    public void Sequencia_de_digitos_dentro_de_uma_palavra_nao_e_dado_pessoal(string texto)
    {
        Assert.False(MotorDePii.Padrao.Analisar(texto, ModoDeAnalise.TextoLivre).TemPii);
        Assert.False(MotorDePii.Padrao.Analisar(texto, ModoDeAnalise.CampoEstruturado).TemPii);
    }

    [Theory]
    [InlineData("ligue para 11912345678.", TipoDePii.Telefone)]
    [InlineData("tel: (11) 91234-5678", TipoDePii.Telefone)]
    [InlineData("CPF: 12345678909,", TipoDePii.Cpf)]
    [InlineData("cartão 898000000000002 ok", TipoDePii.CartaoSus)]
    public void Numero_solto_cercado_de_espaco_ou_pontuacao_continua_sendo_achado(string texto, TipoDePii esperado)
    {
        var resultado = MotorDePii.Padrao.Analisar(texto, ModoDeAnalise.CampoEstruturado);

        Assert.Contains(resultado.Achados, a => a.Tipo == esperado);
    }
}

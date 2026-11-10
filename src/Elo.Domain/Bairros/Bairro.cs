using Elo.Domain.Comum;
using Elo.Domain.Ruas;

namespace Elo.Domain.Bairros;

/// <summary>
/// Bairro acompanhado pelo Elo. O coordenador do bairro abre as chamadas
/// e vê, no painel, como cada rua está.
/// </summary>
public class Bairro : Entidade
{
    public const int TamanhoMaximoNome = 120;
    public const int TamanhoMaximoCidade = 80;
    public const int TamanhoMaximoSubprefeitura = 80;

    // Encapsulamento: a lista real é privada; quem está fora só lê.
    private readonly List<Rua> _ruas = [];

    public string Nome { get; private set; } = string.Empty;
    public string Cidade { get; private set; } = string.Empty;

    /// <summary>A subprefeitura é a unidade usada pelo modelo de ML (nível de atenção).</summary>
    public string Subprefeitura { get; private set; } = string.Empty;

    /// <summary>Na GS as ruas e as respostas são simuladas, e o app avisa isso na tela.</summary>
    public bool DadosSimulados { get; private set; }

    public IReadOnlyCollection<Rua> Ruas => _ruas.AsReadOnly();

    private Bairro()
    {
        // Usado pelo EF Core para materializar o objeto vindo do banco.
    }

    public Bairro(string nome, string cidade, string subprefeitura, bool dadosSimulados)
    {
        Atualizar(nome, cidade, subprefeitura, dadosSimulados);
    }

    public void Atualizar(string nome, string cidade, string subprefeitura, bool dadosSimulados)
    {
        Nome = Guarda.TextoObrigatorio(nome, "Nome do bairro", TamanhoMaximoNome);
        Cidade = Guarda.TextoObrigatorio(cidade, "Cidade", TamanhoMaximoCidade);
        Subprefeitura = Guarda.TextoObrigatorio(subprefeitura, "Subprefeitura", TamanhoMaximoSubprefeitura);
        DadosSimulados = dadosSimulados;
    }

    public override string ToString() => $"{Nome} - {Cidade}";
}

using Elo.Domain.Comum;
using Elo.Domain.Sensores;

namespace Elo.Domain.Ruas;

/// <summary>
/// Uma rua acompanhada pelo Elo. Durante a chuva, a liderança da rua responde
/// às chamadas em um toque.
/// </summary>
public class Rua : Entidade
{
    public const int TamanhoMaximoNome = 120;
    public const int TamanhoMaximoMicroarea = 60;

    private readonly List<Sensor> _sensores = [];

    public int BairroId { get; private set; }
    public string Nome { get; private set; } = string.Empty;

    /// <summary>Grupo de ruas próximas (o simulado da GS usa 3 microáreas).</summary>
    public string Microarea { get; private set; } = string.Empty;

    public NivelRisco NivelRisco { get; private set; }

    /// <summary>Rua desativada não entra no painel nem no indicador.</summary>
    public bool Ativa { get; private set; }

    public Lideranca? Lideranca { get; private set; }
    public PlanoDaRua? Plano { get; private set; }
    public IReadOnlyCollection<Sensor> Sensores => _sensores.AsReadOnly();

    private Rua()
    {
        // Usado pelo EF Core.
    }

    public Rua(int bairroId, string nome, string microarea, NivelRisco nivelRisco, Lideranca? lideranca = null)
    {
        BairroId = Guarda.IdValido(bairroId, "A rua precisa pertencer a um bairro.");
        Atualizar(nome, microarea, nivelRisco, lideranca, ativa: true);
    }

    public void Atualizar(string nome, string microarea, NivelRisco nivelRisco, Lideranca? lideranca, bool ativa)
    {
        Nome = Guarda.TextoObrigatorio(nome, "Nome da rua", TamanhoMaximoNome);
        Microarea = Guarda.TextoObrigatorio(microarea, "Microárea", TamanhoMaximoMicroarea);
        NivelRisco = Guarda.EnumValido(nivelRisco, "Nível de risco");
        Lideranca = lideranca;
        Ativa = ativa;
    }

    public void DefinirPlano(PlanoDaRua plano)
    {
        ArgumentNullException.ThrowIfNull(plano);
        Plano = plano;
    }

    public override string ToString() => $"{Nome} ({Microarea})";
}

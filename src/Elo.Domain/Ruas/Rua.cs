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
    public const int MinimoDePontosDoTracado = 2;
    public const int MaximoDePontosDoTracado = 2000;

    private readonly List<Sensor> _sensores = [];

    public int BairroId { get; private set; }
    public string Nome { get; private set; } = string.Empty;

    /// <summary>Grupo de ruas próximas (o simulado da GS usa 3 microáreas).</summary>
    public string Microarea { get; private set; } = string.Empty;

    public NivelRisco NivelRisco { get; private set; }

    /// <summary>Rua desativada não entra no painel nem no indicador.</summary>
    public bool Ativa { get; private set; }

    /// <summary>Ponto de referência da rua no mapa (ex.: onde o marcador aparece). Opcional, sempre junto da longitude.</summary>
    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    /// <summary>
    /// Linha da rua no mapa, na ordem em que foi desenhada. Nulo quando a rua não tem traçado.
    /// Só leitura: para trocar, use <see cref="DefinirGeografia"/>.
    /// </summary>
    public IReadOnlyList<Coordenada>? Tracado { get; private set; }

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

    /// <summary>
    /// Define onde a rua fica no mapa. Tudo é opcional; o traçado, quando existe, precisa de pelo menos 2 pontos.
    /// Fica separado de <see cref="Atualizar"/> para quem edita o cadastro da rua não apagar o traçado sem querer.
    /// </summary>
    public void DefinirGeografia(double? latitude, double? longitude, IEnumerable<Coordenada>? tracado)
    {
        var (latitudeValida, longitudeValida) = Guarda.ParDeCoordenadas(latitude, longitude, "Rua");

        var pontos = tracado?.ToList();
        if (pontos is { Count: > 0 and < MinimoDePontosDoTracado } || pontos?.Count > MaximoDePontosDoTracado)
        {
            throw new RegraDeNegocioException(
                $"O traçado da rua deve ter de {MinimoDePontosDoTracado} a {MaximoDePontosDoTracado} pontos.");
        }

        Latitude = latitudeValida;
        Longitude = longitudeValida;
        Tracado = pontos is { Count: > 0 } ? pontos.AsReadOnly() : null;
    }

    public void DefinirPlano(PlanoDaRua plano)
    {
        ArgumentNullException.ThrowIfNull(plano);
        Plano = plano;
    }

    public override string ToString() => $"{Nome} ({Microarea})";
}

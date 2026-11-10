using Elo.Domain.Comum;

namespace Elo.Domain.Sensores;

/// <summary>
/// Sensor de nível de água (ESP32 + HC-SR04) instalado no ponto crítico de uma rua.
/// O buzzer toca no próprio ESP32, mesmo sem internet; quando há rede, a leitura vem para a API.
/// </summary>
public class Sensor : Entidade
{
    public const double NivelMaximoCm = 500;
    public const int TamanhoMaximoCodigo = 40;
    public const int TamanhoMaximoLocal = 150;

    private readonly List<LeituraSensor> _leituras = [];

    public int RuaId { get; private set; }

    /// <summary>
    /// Código gravado no ESP32, sempre em minúsculas. É o "reguaId" do contrato do app. Ex.: regua-exemplo.
    /// </summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Local { get; private set; } = string.Empty;

    /// <summary>Acima deste nível (em cm) a leitura é marcada como alerta.</summary>
    public double LimiteAlertaCm { get; private set; }

    public bool Ativo { get; private set; }

    public IReadOnlyCollection<LeituraSensor> Leituras => _leituras.AsReadOnly();

    private Sensor()
    {
        // Usado pelo EF Core.
    }

    public Sensor(int ruaId, string codigo, string local, double limiteAlertaCm)
    {
        RuaId = Guarda.IdValido(ruaId, "O sensor precisa estar em uma rua.");
        Codigo = NormalizarCodigo(codigo);
        Atualizar(local, limiteAlertaCm, ativo: true);
    }

    /// <summary>O código é comparado sem diferenciar maiúsculas: "REGUA-EXEMPLO" e "regua-exemplo" são o mesmo sensor.</summary>
    public static string NormalizarCodigo(string? codigo) =>
        Guarda.TextoObrigatorio(codigo, "Código do sensor", TamanhoMaximoCodigo).ToLowerInvariant();

    public void Atualizar(string local, double limiteAlertaCm, bool ativo)
    {
        Local = Guarda.TextoObrigatorio(local, "Local do sensor", TamanhoMaximoLocal);
        LimiteAlertaCm = Guarda.Intervalo(limiteAlertaCm, 1, NivelMaximoCm, "Limite de alerta (cm)");
        Ativo = ativo;
    }

    public LeituraSensor RegistrarLeitura(double nivelCm, DateTime medidaEm, DateTime agora)
    {
        if (!Ativo)
        {
            throw new RegraDeNegocioException("Este sensor está desativado e não recebe leituras.");
        }

        if (medidaEm > agora + TimeSpan.FromMinutes(5))
        {
            throw new RegraDeNegocioException("A hora da leitura está no futuro. Confira o relógio do ESP32.");
        }

        var nivel = Guarda.Intervalo(nivelCm, 0, NivelMaximoCm, "Nível da água (cm)");
        var leitura = new LeituraSensor(Id, nivel, LimiteAlertaCm, medidaEm, agora);
        _leituras.Add(leitura);
        return leitura;
    }
}

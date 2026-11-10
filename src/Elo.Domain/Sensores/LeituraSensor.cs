using Elo.Domain.Comum;

namespace Elo.Domain.Sensores;

/// <summary>
/// Uma medição do nível da água. Guarda o limite que valia na hora,
/// porque o limite do sensor pode mudar depois.
/// </summary>
public class LeituraSensor : Entidade
{
    public int SensorId { get; private set; }
    public double NivelCm { get; private set; }
    public double LimiteNaHoraCm { get; private set; }
    public bool AcimaDoLimite { get; private set; }
    public DateTime MedidaEm { get; private set; }
    public DateTime RecebidaEm { get; private set; }

    private LeituraSensor()
    {
        // Usado pelo EF Core.
    }

    internal LeituraSensor(int sensorId, double nivelCm, double limiteCm, DateTime medidaEm, DateTime recebidaEm)
    {
        SensorId = sensorId;
        NivelCm = nivelCm;
        LimiteNaHoraCm = limiteCm;
        AcimaDoLimite = nivelCm >= limiteCm;
        MedidaEm = medidaEm;
        RecebidaEm = recebidaEm;
    }
}

/// <summary>Última leitura de um sensor, já com o código dele (usada no painel).</summary>
public sealed record LeituraRecente(
    string CodigoSensor,
    double NivelCm,
    double LimiteCm,
    bool AcimaDoLimite,
    DateTime MedidaEm);

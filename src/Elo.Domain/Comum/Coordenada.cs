namespace Elo.Domain.Comum;

/// <summary>
/// Um ponto do mapa. Value object imutável (comparado por valor).
/// A ordem segue o GeoJSON e o MapLibre: primeiro a longitude, depois a latitude.
/// </summary>
public readonly record struct Coordenada
{
    public double Longitude { get; }
    public double Latitude { get; }

    public Coordenada(double longitude, double latitude)
    {
        Longitude = Guarda.Intervalo(longitude, -180, 180, "Longitude");
        Latitude = Guarda.Intervalo(latitude, -90, 90, "Latitude");
    }
}

using System.Text.Json;
using Elo.Domain.Comum;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Elo.Infrastructure.Persistencia.Conversores;

/// <summary>
/// Guarda o traçado da rua em uma coluna de texto, como lista JSON de pares [longitude, latitude]
/// (o mesmo formato do GeoJSON). Ex.: [[-46.41,-23.48],[-46.409,-23.481]].
/// </summary>
internal sealed class TracadoConverter() : ValueConverter<IReadOnlyList<Coordenada>?, string>(
    tracado => Serializar(tracado!),
    json => Ler(json))
{
    private static string Serializar(IReadOnlyList<Coordenada> tracado) =>
        JsonSerializer.Serialize(tracado.Select(ponto => new[] { ponto.Longitude, ponto.Latitude }));

    private static IReadOnlyList<Coordenada> Ler(string json) =>
        (JsonSerializer.Deserialize<double[][]>(json) ?? [])
            .Select(par => new Coordenada(par[0], par[1]))
            .ToList()
            .AsReadOnly();
}

/// <summary>Compara os traçados ponto a ponto (o EF Core usa isso para saber se a coluna mudou).</summary>
internal sealed class TracadoComparer() : ValueComparer<IReadOnlyList<Coordenada>?>(
    (a, b) => ReferenceEquals(a, b) || (a != null && b != null && a.SequenceEqual(b)),
    tracado => tracado == null ? 0 : tracado.Aggregate(0, (hash, ponto) => HashCode.Combine(hash, ponto)),
    tracado => tracado == null ? null : tracado.ToList().AsReadOnly());

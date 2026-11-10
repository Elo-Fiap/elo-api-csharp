namespace Elo.Domain.Ruas;

/// <summary>
/// Quanto a rua costuma alagar. Ajuda a decidir qual rua em silêncio
/// o coordenador deve procurar primeiro.
/// </summary>
public enum NivelRisco
{
    Baixo = 1,
    Medio = 2,
    Alto = 3
}

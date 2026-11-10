using System.Globalization;

namespace Elo.Application.Avisos;

/// <summary>
/// Aviso do INMET já limpo: sem polígono, sem ícone e com as datas no formato ISO.
/// A lista do Brasil inteiro fica em cache; o filtro por município é feito por <see cref="ValePara"/>.
/// </summary>
public sealed record AvisoDoInmet(
    string Id,
    string Titulo,
    string Severidade,
    int Gravidade,
    string Inicio,
    string Fim,
    string Quando,
    IReadOnlyList<string> Riscos,
    IReadOnlyList<string> Instrucoes,
    IReadOnlySet<string> Municipios,
    bool Encerrado)
{
    /// <summary>O aviso vale para o município se não foi encerrado e cita o código IBGE inteiro (nunca por prefixo).</summary>
    public bool ValePara(string codigoIbge) => !Encerrado && Municipios.Contains(codigoIbge);

    /// <summary>Para ordenar pelo início; data que não dá para ler vai para o começo.</summary>
    public DateTimeOffset InicioParaOrdenar =>
        DateTimeOffset.TryParse(Inicio, CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio)
            ? inicio
            : DateTimeOffset.MinValue;

    public static bool EhCodigoIbge(string? codigo) =>
        codigo is { Length: 7 } && codigo.All(char.IsAsciiDigit);
}

/// <summary>De onde vêm os avisos oficiais. A implementação baixa do INMET e guarda em cache.</summary>
public interface IFonteDeAvisos
{
    Task<IReadOnlyList<AvisoDoInmet>> ObterAvisosAsync(CancellationToken cancellationToken = default);
}

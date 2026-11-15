namespace Elo.Domain.Comum;

/// <summary>
/// Validações reaproveitadas pelas entidades ("guard clauses").
/// Centralizar aqui evita repetir o mesmo <c>if</c> em todo construtor.
/// </summary>
public static class Guarda
{
    public static string TextoObrigatorio(string? valor, string campo, int tamanhoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new RegraDeNegocioException($"{campo} é obrigatório.");
        }

        var texto = valor.Trim();
        if (texto.Length > tamanhoMaximo)
        {
            throw new RegraDeNegocioException($"{campo} deve ter no máximo {tamanhoMaximo} caracteres.");
        }

        return texto;
    }

    public static string? TextoOpcional(string? valor, string campo, int tamanhoMaximo) =>
        string.IsNullOrWhiteSpace(valor) ? null : TextoObrigatorio(valor, campo, tamanhoMaximo);

    public static TEnum EnumValido<TEnum>(TEnum valor, string campo)
        where TEnum : struct, Enum =>
        Enum.IsDefined(valor) ? valor : throw new RegraDeNegocioException($"{campo} inválido: {valor}.");

    public static double Intervalo(double valor, double minimo, double maximo, string campo)
    {
        if (double.IsNaN(valor) || valor < minimo || valor > maximo)
        {
            throw new RegraDeNegocioException($"{campo} deve estar entre {minimo} e {maximo}.");
        }

        return valor;
    }

    /// <summary>Latitude e longitude opcionais, mas sempre juntas: ou vêm as duas, ou nenhuma.</summary>
    public static (double? Latitude, double? Longitude) ParDeCoordenadas(double? latitude, double? longitude, string campo)
    {
        if (latitude is null != longitude is null)
        {
            throw new RegraDeNegocioException($"{campo}: informe latitude e longitude juntas.");
        }

        return (
            latitude is double lat ? Intervalo(lat, -90, 90, $"Latitude ({campo})") : null,
            longitude is double lon ? Intervalo(lon, -180, 180, $"Longitude ({campo})") : null);
    }

    public static int IdValido(int id, string mensagem) =>
        id > 0 ? id : throw new RegraDeNegocioException(mensagem);
}

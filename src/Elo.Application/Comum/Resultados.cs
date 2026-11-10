namespace Elo.Application.Comum;

/// <summary>Uma página de resultados (paginação com Skip/Take).</summary>
public sealed record PaginaResultado<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, int TotalDeItens)
{
    public int TotalDePaginas =>
        TamanhoPagina <= 0 ? 0 : (int)Math.Ceiling(TotalDeItens / (double)TamanhoPagina);

    /// <summary>Converte os itens (ex.: entidade para DTO) mantendo os dados da paginação.</summary>
    public PaginaResultado<TDestino> Mapear<TDestino>(Func<T, TDestino> conversor) =>
        new([.. Itens.Select(conversor)], Pagina, TamanhoPagina, TotalDeItens);
}

/// <summary>
/// Resultado de um registro idempotente: <see cref="Criado"/> é falso quando o mesmo
/// registro já existia (ex.: resposta reenviada pelo celular depois de ficar offline).
/// </summary>
public sealed record ResultadoDeRegistro<T>(T Valor, bool Criado);

/// <summary>Mensagens de validação em português usadas nos DTOs de entrada.</summary>
public static class Mensagens
{
    public const string Obrigatorio = "O campo {0} é obrigatório.";
    public const string TamanhoMaximo = "O campo {0} deve ter no máximo {1} caracteres.";
    public const string Intervalo = "O campo {0} deve estar entre {1} e {2}.";
    public const string MaximoDeItens = "O campo {0} aceita no máximo {1} itens.";
}

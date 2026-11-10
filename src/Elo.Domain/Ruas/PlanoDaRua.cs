using Elo.Domain.Comum;

namespace Elo.Domain.Ruas;

/// <summary>
/// Plano da rua: rota, ponto de encontro, abrigo e o que levar.
/// O app guarda uma cópia para abrir sem internet; <see cref="AtualizadoEm"/>
/// diz ao app se a cópia dele está velha.
/// </summary>
public sealed record PlanoDaRua
{
    public const int MaximoDeItens = 15;
    public const int TamanhoMaximoRota = 500;
    public const int TamanhoMaximoLocal = 200;
    public const int TamanhoMaximoItem = 80;

    public string Rota { get; private init; } = string.Empty;
    public string PontoDeEncontro { get; private init; } = string.Empty;
    public string Abrigo { get; private init; } = string.Empty;
    public List<string> ItensParaLevar { get; private init; } = [];
    public DateTime AtualizadoEm { get; private init; }

    private PlanoDaRua()
    {
        // Usado pelo EF Core.
    }

    public PlanoDaRua(string rota, string pontoDeEncontro, string abrigo, IEnumerable<string>? itensParaLevar, DateTime atualizadoEm)
    {
        Rota = Guarda.TextoObrigatorio(rota, "Rota", TamanhoMaximoRota);
        PontoDeEncontro = Guarda.TextoObrigatorio(pontoDeEncontro, "Ponto de encontro", TamanhoMaximoLocal);
        Abrigo = Guarda.TextoObrigatorio(abrigo, "Abrigo", TamanhoMaximoLocal);

        var itens = (itensParaLevar ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => Guarda.TextoObrigatorio(item, "Item para levar", TamanhoMaximoItem))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (itens.Count > MaximoDeItens)
        {
            throw new RegraDeNegocioException($"O plano aceita no máximo {MaximoDeItens} itens para levar.");
        }

        ItensParaLevar = itens;
        AtualizadoEm = atualizadoEm;
    }
}

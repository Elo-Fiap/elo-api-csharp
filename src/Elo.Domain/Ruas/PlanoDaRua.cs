using Elo.Domain.Comum;

namespace Elo.Domain.Ruas;

/// <summary>
/// Plano da rua: rota, ponto de encontro, abrigo e o que levar.
/// O app guarda uma cópia para abrir sem internet; <see cref="AtualizadoEm"/>
/// diz ao app se a cópia dele está velha.
/// <para>
/// Value object imutável: a lista de itens é só de leitura e entra na comparação por valor.
/// Sozinho, o <c>record</c> compararia a lista por referência, por isso <see cref="Equals(PlanoDaRua?)"/>
/// e <see cref="GetHashCode"/> são escritos aqui.
/// </para>
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

    /// <summary>Só leitura: quem está fora do plano não consegue incluir nem tirar itens.</summary>
    public IReadOnlyList<string> ItensParaLevar { get; private init; } = [];

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

        ItensParaLevar = itens.AsReadOnly();
        AtualizadoEm = atualizadoEm;
    }

    /// <summary>Dois planos são iguais quando têm os mesmos dados e os mesmos itens, na mesma ordem.</summary>
    public bool Equals(PlanoDaRua? outro) =>
        outro is not null
        && Rota == outro.Rota
        && PontoDeEncontro == outro.PontoDeEncontro
        && Abrigo == outro.Abrigo
        && AtualizadoEm == outro.AtualizadoEm
        && ItensParaLevar.SequenceEqual(outro.ItensParaLevar);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Rota);
        hash.Add(PontoDeEncontro);
        hash.Add(Abrigo);
        hash.Add(AtualizadoEm);

        foreach (var item in ItensParaLevar)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}

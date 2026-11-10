using Elo.Domain.Comum;

namespace Elo.Domain.Chamadas;

/// <summary>
/// A resposta de uma rua a uma chamada: "Tudo bem", "Água subindo" ou "Preciso de ajuda".
/// No app ela se chama "chamada" (a chamada que a liderança faz); aqui, "resposta".
/// O construtor é <c>internal</c>: só a <see cref="Chamada"/> cria respostas,
/// depois de validar a janela de tempo e o bairro.
/// </summary>
public class Resposta : Entidade
{
    public const int MaximoDePessoas = 500;
    public const int TamanhoMaximoObservacao = 200;
    public const int TamanhoMaximoIdLocal = 64;

    /// <summary>
    /// Id gerado no celular (texto, ex.: "m3k9x2a-7f4q1b2c"). Quando a internet volta, o app reenvia
    /// as respostas guardadas; com este id a API reconhece a repetida e não duplica.
    /// </summary>
    public string IdLocal { get; private set; } = string.Empty;

    public int ChamadaId { get; private set; }
    public int RuaId { get; private set; }
    public SituacaoRua Situacao { get; private set; }

    /// <summary>Quantas pessoas, se a liderança informou (opcional).</summary>
    public int? Pessoas { get; private set; }

    /// <summary>Texto livre opcional, já com CPF, telefone e e-mail mascarados.</summary>
    public string? Observacao { get; private set; }

    public CanalResposta Canal { get; private set; }

    /// <summary>Quando a liderança tocou no botão (hora do celular, mesmo offline). É o "criadaEm" do app.</summary>
    public DateTime RespondidaEm { get; private set; }

    /// <summary>Quando a resposta chegou na API. É essa hora que conta para o indicador.</summary>
    public DateTime RecebidaEm { get; private set; }

    public bool PediuAjuda => Situacao == SituacaoRua.PrecisoDeAjuda;

    private Resposta()
    {
        // Usado pelo EF Core.
    }

    internal Resposta(
        string? idLocal,
        int chamadaId,
        int ruaId,
        SituacaoRua situacao,
        int? pessoas,
        string? observacao,
        CanalResposta canal,
        DateTime respondidaEm,
        DateTime recebidaEm)
    {
        Situacao = Guarda.EnumValido(situacao, "Situação");
        Canal = Guarda.EnumValido(canal, "Canal");

        if (pessoas is < 1 or > MaximoDePessoas)
        {
            throw new RegraDeNegocioException($"A quantidade de pessoas deve ser de 1 a {MaximoDePessoas}.");
        }

        IdLocal = string.IsNullOrWhiteSpace(idLocal)
            ? Guid.NewGuid().ToString("N")
            : Guarda.TextoObrigatorio(idLocal, "Id local", TamanhoMaximoIdLocal);
        ChamadaId = chamadaId;
        RuaId = ruaId;
        Pessoas = pessoas;
        Observacao = Guarda.TextoOpcional(
            FiltroDeDadosPessoais.Aplicar(observacao), "Observação", TamanhoMaximoObservacao);
        RespondidaEm = respondidaEm;
        RecebidaEm = recebidaEm;
    }
}

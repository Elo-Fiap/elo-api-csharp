using Elo.Domain.Comum;

namespace Elo.Domain.Chamadas;

/// <summary>
/// A resposta de uma rua a uma chamada: "Tudo bem", "Água subindo" ou "Preciso de ajuda".
/// O construtor é <c>internal</c>: só a <see cref="Chamada"/> cria respostas,
/// depois de validar a janela de tempo e o bairro.
/// </summary>
public class Resposta : Entidade
{
    public const int MaximoDePessoas = 500;
    public const int TamanhoMaximoObservacao = 280;

    /// <summary>
    /// Id gerado no celular. Quando a internet volta, o app reenvia as respostas guardadas;
    /// com este id a API reconhece a repetida e não duplica.
    /// </summary>
    public Guid IdLocal { get; private set; }

    public int ChamadaId { get; private set; }
    public int RuaId { get; private set; }
    public SituacaoRua Situacao { get; private set; }

    /// <summary>Quantas pessoas precisam de ajuda (só em "Preciso de ajuda").</summary>
    public int? Pessoas { get; private set; }

    /// <summary>Texto livre opcional, já com CPF, telefone e e-mail mascarados.</summary>
    public string? Observacao { get; private set; }

    public CanalResposta Canal { get; private set; }

    /// <summary>Quando a liderança tocou no botão (hora do celular, mesmo offline).</summary>
    public DateTime RespondidaEm { get; private set; }

    /// <summary>Quando a resposta chegou na API. É essa hora que conta para o indicador.</summary>
    public DateTime RecebidaEm { get; private set; }

    public bool PediuAjuda => Situacao == SituacaoRua.PrecisoDeAjuda;

    private Resposta()
    {
        // Usado pelo EF Core.
    }

    internal Resposta(
        Guid idLocal,
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
        ValidarPessoas(Situacao, pessoas);

        IdLocal = idLocal == Guid.Empty ? Guid.NewGuid() : idLocal;
        ChamadaId = chamadaId;
        RuaId = ruaId;
        Pessoas = pessoas;
        Observacao = Guarda.TextoOpcional(
            FiltroDeDadosPessoais.Aplicar(observacao), "Observação", TamanhoMaximoObservacao);
        RespondidaEm = respondidaEm;
        RecebidaEm = recebidaEm;
    }

    private static void ValidarPessoas(SituacaoRua situacao, int? pessoas)
    {
        if (situacao == SituacaoRua.PrecisoDeAjuda)
        {
            if (pessoas is null or < 1 or > MaximoDePessoas)
            {
                throw new RegraDeNegocioException(
                    $"Ao pedir ajuda, informe quantas pessoas precisam (de 1 a {MaximoDePessoas}).");
            }

            return;
        }

        if (pessoas is not null)
        {
            throw new RegraDeNegocioException("A quantidade de pessoas só vale para \"Preciso de ajuda\".");
        }
    }
}

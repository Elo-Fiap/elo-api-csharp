using Elo.Domain.Chamadas;
using Elo.Domain.Comum;
using Elo.Domain.Ruas;

namespace Elo.Tests;

/// <summary>Monta objetos do domínio prontos para os testes.</summary>
internal static class Fabrica
{
    public static readonly DateTime Inicio = new(2026, 11, 9, 18, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Nos testes não há banco para gerar o Id. Usamos reflexão para preenchê-lo,
    /// sem abrir o setter protegido da entidade.
    /// </summary>
    public static T ComId<T>(this T entidade, int id)
        where T : Entidade
    {
        typeof(Entidade).GetProperty(nameof(Entidade.Id))!.SetValue(entidade, id);
        return entidade;
    }

    public static Rua NovaRua(int id, string nome = "Rua A", NivelRisco risco = NivelRisco.Medio, int bairroId = 1) =>
        new Rua(bairroId, nome, "Microárea 1", risco, new Lideranca("Liderança", "5500900000000")).ComId(id);

    public static AvisoOficial NovoAviso() =>
        new("INMET", "TESTE-0001", "Acumulado de Chuva", SeveridadeAviso.Perigo);

    public static ChamadaReal NovaChamadaReal(int id = 1, int bairroId = 1, DateTime? inicio = null) =>
        new ChamadaReal(bairroId, NovoAviso(), inicio ?? Inicio).ComId(id);

    public static ChamadaTreino NovoTreino(int id = 1, int bairroId = 1, DateTime? inicio = null, int duracaoMinutos = 30) =>
        new ChamadaTreino(bairroId, inicio ?? Inicio, duracaoMinutos).ComId(id);

    public static Resposta Responder(
        Chamada chamada,
        Rua rua,
        SituacaoRua situacao,
        int aposMinutos,
        int? pessoas = null,
        int? chegouAposMinutos = null) =>
        chamada.RegistrarResposta(
            Guid.NewGuid().ToString("N"),
            rua,
            situacao,
            pessoas,
            observacao: null,
            CanalResposta.App,
            respondidaEm: chamada.IniciadaEm.AddMinutes(aposMinutos),
            agora: chamada.IniciadaEm.AddMinutes(chegouAposMinutos ?? aposMinutos));
}

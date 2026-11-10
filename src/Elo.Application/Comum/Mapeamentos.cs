using Elo.Application.Abstracoes;
using Elo.Application.Bairros;
using Elo.Application.Chamadas;
using Elo.Application.Painel;
using Elo.Application.Ruas;
using Elo.Application.Sensores;
using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Painel;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;

namespace Elo.Application.Comum;

/// <summary>
/// Conversões de entidade para DTO (e de DTO para value object).
/// Ficam em um só lugar para os serviços não repetirem código e a API nunca expor a entidade.
/// </summary>
internal static class Mapeamentos
{
    // ---------- Bairros e ruas ----------

    public static BairroDetalheDto ParaDetalheDto(this Bairro bairro) => new(
        bairro.Id,
        bairro.Nome,
        bairro.Cidade,
        bairro.Subprefeitura,
        bairro.DadosSimulados,
        [.. bairro.Ruas
            .OrderBy(rua => rua.Microarea)
            .ThenBy(rua => rua.Nome)
            .Select(rua => rua.ParaResumoDto())]);

    public static RuaResumoDto ParaResumoDto(this Rua rua) => new(
        rua.Id,
        rua.BairroId,
        rua.Nome,
        rua.Microarea,
        rua.NivelRisco,
        rua.Ativa,
        rua.Lideranca?.ParaDto(),
        rua.Plano is not null);

    public static RuaDetalheDto ParaDetalheDto(this Rua rua, IEnumerable<SensorComLeitura> sensores) => new(
        rua.Id,
        rua.BairroId,
        rua.Nome,
        rua.Microarea,
        rua.NivelRisco,
        rua.Ativa,
        rua.Lideranca?.ParaDto(),
        rua.Plano?.ParaDto(),
        [.. sensores.Select(sensor => sensor.ParaDto())]);

    public static LiderancaDto ParaDto(this Lideranca lideranca) =>
        new() { Apelido = lideranca.Apelido, WhatsApp = lideranca.WhatsApp };

    public static Lideranca ParaDominio(this LiderancaDto dto) => new(dto.Apelido, dto.WhatsApp);

    public static PlanoDaRuaDto ParaDto(this PlanoDaRua plano) => new(
        plano.Rota,
        plano.PontoDeEncontro,
        plano.Abrigo,
        [.. plano.ItensParaLevar],
        plano.AtualizadoEm);

    // ---------- Chamadas ----------

    public static StatusChamada StatusEm(this Chamada chamada, DateTime agora) =>
        chamada.EstaAbertaEm(agora) ? StatusChamada.Aberta : StatusChamada.Encerrada;

    public static ChamadaDto ParaDto(this Chamada chamada, DateTime agora) => new(
        chamada.Id,
        chamada.BairroId,
        chamada.Tipo,
        chamada.StatusEm(agora),
        chamada.Pergunta,
        chamada.IniciadaEm,
        chamada.EncerradaEm,
        chamada.EncerramentoPrevistoEm,
        // Polimorfismo: cada tipo diz o que tem (o aviso só na chamada real, a duração só no treino).
        chamada.AvisoQueMotivou?.ParaDto(),
        chamada.DuracaoPrevistaEmMinutos,
        chamada.Resultado?.ParaDto());

    public static AvisoOficialDto ParaDto(this AvisoOficial aviso) => new()
    {
        Fonte = aviso.Fonte,
        Identificador = aviso.Identificador,
        Evento = aviso.Evento,
        Severidade = aviso.Severidade
    };

    public static AvisoOficial ParaDominio(this AvisoOficialDto dto) => new(
        dto.Fonte,
        dto.Identificador,
        dto.Evento,
        dto.Severidade ?? 0);

    public static RespostaDto ParaDto(this Resposta resposta) => new(
        resposta.Id,
        resposta.IdLocal,
        resposta.ChamadaId,
        resposta.RuaId,
        resposta.Situacao,
        resposta.Pessoas,
        resposta.Observacao,
        resposta.Canal,
        resposta.RespondidaEm,
        resposta.RecebidaEm);

    public static AcaoDto ParaDto(this AcaoCoordenador acao) =>
        new(acao.Id, acao.ChamadaId, acao.RuaId, acao.Tipo, acao.RegistradaEm);

    public static IndicadorDto ParaDto(this IndicadorDaChamada indicador) => new(
        indicador.TotalDeRuas,
        indicador.RuasComResposta,
        indicador.RuasEmSilencio,
        indicador.RuasConhecidasNaJanela,
        indicador.PercentualConhecidoNaJanela,
        indicador.PercentualComResposta,
        indicador.RuasQuePediramAjuda,
        indicador.MinutosAtePrimeiraRespostaEmMedia);

    // ---------- Painel ----------

    public static ItemDoPainelDto ParaItemDto(this SituacaoDaRua situacao, int posicao, Chamada chamada) => new(
        posicao,
        situacao.Rua.Id,
        situacao.Rua.Nome,
        situacao.Rua.Microarea,
        situacao.Rua.NivelRisco,
        situacao.Status,
        situacao.UltimaResposta?.RespondidaEm,
        situacao.UltimaResposta?.Pessoas,
        situacao.UltimaResposta?.Observacao,
        situacao.UltimaResposta?.Canal,
        situacao.AtendimentoACaminho,
        situacao.UltimaLeitura?.ParaDto(),
        // Polimorfismo: no treino a mensagem deixa claro que é simulado.
        situacao.Rua.Lideranca?.GerarLinkWhatsApp(chamada.MontarMensagemParaRua(situacao.Rua.Nome)));

    public static SensorNoPainelDto ParaDto(this LeituraRecente leitura) => new(
        leitura.CodigoSensor,
        leitura.NivelCm,
        leitura.LimiteCm,
        leitura.AcimaDoLimite,
        leitura.MedidaEm);

    // ---------- Sensores ----------

    public static SensorDto ParaDto(this SensorComLeitura item) => new(
        item.Sensor.Id,
        item.Sensor.RuaId,
        item.Sensor.Codigo,
        item.Sensor.Local,
        item.Sensor.LimiteAlertaCm,
        item.Sensor.Ativo,
        item.UltimaLeitura?.ParaDto());

    public static LeituraDto ParaDto(this LeituraSensor leitura) => new(
        leitura.Id,
        leitura.SensorId,
        leitura.NivelCm,
        leitura.LimiteNaHoraCm,
        leitura.AcimaDoLimite,
        leitura.MedidaEm,
        leitura.RecebidaEm);
}

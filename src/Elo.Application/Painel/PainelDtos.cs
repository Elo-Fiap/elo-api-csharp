using Elo.Application.Chamadas;
using Elo.Domain.Chamadas;
using Elo.Domain.Painel;
using Elo.Domain.Ruas;

namespace Elo.Application.Painel;

/// <summary>Indicador de impacto: ruas com situação conhecida em até 15 minutos.</summary>
public sealed record IndicadorDto(
    int TotalDeRuas,
    int RuasComResposta,
    int RuasEmSilencio,
    int RuasConhecidasEm15Min,
    double PercentualConhecidoEm15Min,
    double PercentualComResposta,
    int RuasQuePediramAjuda,
    double? MinutosAtePrimeiraRespostaEmMedia);

public sealed record SensorNoPainelDto(
    string Codigo,
    double NivelCm,
    double LimiteCm,
    bool AcimaDoLimite,
    DateTime MedidaEm);

/// <summary>Uma linha do painel do coordenador, já na ordem de prioridade.</summary>
public sealed record ItemDoPainelDto(
    int Posicao,
    int RuaId,
    string Rua,
    string Microarea,
    NivelRisco NivelRisco,
    StatusNoPainel Status,
    DateTime? UltimaRespostaEm,
    int? Pessoas,
    string? Observacao,
    CanalResposta? Canal,
    bool AtendimentoACaminho,
    SensorNoPainelDto? Sensor,
    string? LinkWhatsApp);

public sealed record ResumoDoPainelDto(
    int PrecisamDeAjuda,
    int EmSilencio,
    int AguaSubindo,
    int TudoBem,
    int PessoasPrecisandoDeAjuda);

/// <summary>Painel "Bairro": quem pediu ajuda primeiro, depois quem está em silêncio.</summary>
public sealed record PainelDto(
    int BairroId,
    string Bairro,
    bool DadosSimulados,
    ChamadaDto Chamada,
    DateTime GeradoEm,
    ResumoDoPainelDto Resumo,
    IndicadorDto Indicador,
    IReadOnlyList<ItemDoPainelDto> Ruas);

public sealed record IndicadorDeChamadaDto(
    int ChamadaId,
    TipoChamada Tipo,
    DateTime IniciadaEm,
    StatusChamada Status,
    IndicadorDto Indicador);

public sealed record MediaPorTipoDto(
    TipoChamada Tipo,
    int Chamadas,
    double PercentualMedioConhecidoEm15Min,
    double? MinutosMediosAtePrimeiraResposta);

/// <summary>Indicador de todas as chamadas do bairro, com a média por tipo (Real x Treino).</summary>
public sealed record IndicadoresDoBairroDto(
    int BairroId,
    string Bairro,
    IReadOnlyList<MediaPorTipoDto> MediaPorTipo,
    IReadOnlyList<IndicadorDeChamadaDto> Chamadas);

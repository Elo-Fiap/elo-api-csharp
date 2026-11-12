using Elo.Domain.Chamadas;
using Elo.Domain.Ruas;

namespace Elo.Application.PainelAdmin;

/// <summary>
/// Tudo o que o painel de administração precisa, numa resposta só: bairros com ruas, réguas com leituras,
/// chamadas com respostas e ações, e os avisos do INMET. Datas em UTC, medidas em metros.
/// </summary>
public sealed record InstantaneoDto(
    DateTime GeradoEm,
    IReadOnlyList<BairroAdminDto> Bairros,
    IReadOnlyList<SensorAdminDto> Sensores,
    IReadOnlyList<ChamadaAdminDto> Chamadas,
    IReadOnlyList<AvisoAdminDto> Avisos);

/// <summary>Bairro com todas as ruas (ativas e inativas) e a medição do simulado sem o app.</summary>
public sealed record BairroAdminDto(
    int Id,
    string Nome,
    string Cidade,
    string? Uf,
    string? CodigoIbge,
    double? Latitude,
    double? Longitude,
    bool DadosSimulados,
    double? PercentualSemApp,
    DateTime? SemAppMedidoEm,
    IReadOnlyList<RuaAdminDto> Ruas);

/// <summary>Rua no mapa. O traçado é uma lista de pares [longitude, latitude] (nulo quando a rua não tem).</summary>
public sealed record RuaAdminDto(
    int Id,
    string Nome,
    string Microarea,
    NivelRisco NivelRisco,
    bool Ativa,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<double[]>? Tracado);

/// <summary>Régua (sensor) com as leituras das últimas 72 horas, da mais antiga para a mais nova. Medidas em metros.</summary>
public sealed record SensorAdminDto(
    int Id,
    string Codigo,
    int RuaId,
    int BairroId,
    string Local,
    double? Latitude,
    double? Longitude,
    double LimiteMetros,
    bool Ativo,
    IReadOnlyList<LeituraAdminDto> Leituras);

public sealed record LeituraAdminDto(double NivelMetros, DateTime MedidaEm);

/// <summary>Chamada aberta agora ou aberta dentro da janela de dias, com tudo o que aconteceu nela.</summary>
public sealed record ChamadaAdminDto(
    int Id,
    int BairroId,
    TipoChamada Tipo,
    DateTime AbertaEm,
    DateTime? EncerradaEm,
    string? AvisoTitulo,
    IReadOnlyList<RespostaAdminDto> Respostas,
    IReadOnlyList<AcaoAdminDto> Acoes);

public sealed record RespostaAdminDto(
    int Id,
    int RuaId,
    SituacaoRua Situacao,
    int? Pessoas,
    DateTime RespondidaEm,
    DateTime RecebidaEm);

public sealed record AcaoAdminDto(int RuaId, TipoAcao Tipo, DateTime Em);

/// <summary>Aviso do INMET ativo para algum bairro. <c>Ufs</c> traz as UFs dos bairros a que o aviso vale.</summary>
public sealed record AvisoAdminDto(
    string Id,
    string Titulo,
    string Severidade,
    int Gravidade,
    DateTime? Inicio,
    DateTime? Fim,
    IReadOnlyList<string> Ufs);

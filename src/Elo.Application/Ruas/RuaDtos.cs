using System.ComponentModel.DataAnnotations;
using Elo.Application.Chamadas;
using Elo.Application.Comum;
using Elo.Application.Sensores;
using Elo.Domain.Ruas;

namespace Elo.Application.Ruas;

/// <summary>Contato da liderança da rua: só apelido e WhatsApp.</summary>
public sealed record LiderancaDto
{
    /// <summary>Apelido da liderança. Ex.: Dona Cida.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(60, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Apelido { get; init; } = string.Empty;

    /// <summary>WhatsApp com DDI e DDD. Ex.: 5511912345678.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [RegularExpression(@"^\+?[\d\s()-]{12,20}$", ErrorMessage = "Informe o WhatsApp com DDI e DDD. Ex.: 5511912345678.")]
    public string WhatsApp { get; init; } = string.Empty;
}

/// <summary>Dados para criar ou atualizar uma rua.</summary>
public sealed record RuaRequest
{
    /// <summary>Nome da rua. Ex.: Rua das Garças.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(120, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Nome { get; init; } = string.Empty;

    /// <summary>Grupo de ruas próximas. Ex.: Microárea 1.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(60, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Microarea { get; init; } = string.Empty;

    /// <summary>Quanto a rua costuma alagar: Baixo, Medio ou Alto.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    public NivelRisco? NivelRisco { get; init; }

    public LiderancaDto? Lideranca { get; init; }

    /// <summary>Só vale na atualização: falso desativa a rua (ela sai do painel). Na criação a rua nasce ativa.</summary>
    public bool Ativa { get; init; } = true;
}

public sealed record RuaResumoDto(
    int Id,
    int BairroId,
    string Nome,
    string Microarea,
    NivelRisco NivelRisco,
    bool Ativa,
    LiderancaDto? Lideranca,
    bool TemPlano);

public sealed record RuaDetalheDto(
    int Id,
    int BairroId,
    string Nome,
    string Microarea,
    NivelRisco NivelRisco,
    bool Ativa,
    LiderancaDto? Lideranca,
    PlanoDaRuaDto? Plano,
    IReadOnlyList<SensorDto> Sensores);

/// <summary>Plano da rua: rota, ponto de encontro, abrigo e o que levar.</summary>
public sealed record PlanoDaRuaRequest
{
    /// <summary>Por onde sair da rua quando a água sobe.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(500, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Rota { get; init; } = string.Empty;

    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(200, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string PontoDeEncontro { get; init; } = string.Empty;

    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(200, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Abrigo { get; init; } = string.Empty;

    /// <summary>Lista curta do que levar. Ex.: documentos, remédios, lanterna.</summary>
    [MaxLength(15, ErrorMessage = Mensagens.MaximoDeItens)]
    public List<string> ItensParaLevar { get; init; } = [];
}

public sealed record PlanoDaRuaDto(
    string Rota,
    string PontoDeEncontro,
    string Abrigo,
    IReadOnlyList<string> ItensParaLevar,
    DateTime AtualizadoEm);

/// <summary>Respostas de uma rua em um mês (tela "Registro").</summary>
public sealed record RegistroMensalDto(
    string Mes,
    int TotalDeRespostas,
    int PedidosDeAjuda,
    IReadOnlyList<RespostaDto> Respostas);

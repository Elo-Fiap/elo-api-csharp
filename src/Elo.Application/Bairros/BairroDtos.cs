using System.ComponentModel.DataAnnotations;
using Elo.Application.Comum;
using Elo.Application.Ruas;

namespace Elo.Application.Bairros;

/// <summary>Dados para criar ou atualizar um bairro.</summary>
public sealed record BairroRequest
{
    /// <summary>Nome do bairro. Ex.: Jardim Pantanal.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(120, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Nome { get; init; } = string.Empty;

    /// <summary>Cidade do bairro. Ex.: São Paulo.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(80, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Cidade { get; init; } = string.Empty;

    /// <summary>Subprefeitura (unidade usada pelo modelo de ML). Ex.: São Miguel Paulista.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(80, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Subprefeitura { get; init; } = string.Empty;

    /// <summary>Verdadeiro quando ruas e respostas são simuladas (caso da GS).</summary>
    public bool DadosSimulados { get; init; } = true;

    /// <summary>Sigla do estado (2 letras). Ex.: SP. Opcional.</summary>
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "A UF deve ter 2 letras. Ex.: SP.")]
    public string? Uf { get; init; }

    /// <summary>Código IBGE do município (7 dígitos, em texto). Ex.: 3550308. Opcional.</summary>
    [RegularExpression("^[0-9]{7}$", ErrorMessage = "O código IBGE deve ter 7 dígitos. Ex.: 3550308.")]
    public string? CodigoIbge { get; init; }

    /// <summary>Latitude do centro do bairro (-90 a 90). Opcional; vem sempre junto da longitude.</summary>
    [Range(-90.0, 90.0, ErrorMessage = Mensagens.Intervalo)]
    public double? Latitude { get; init; }

    /// <summary>Longitude do centro do bairro (-180 a 180). Opcional; vem sempre junto da latitude.</summary>
    [Range(-180.0, 180.0, ErrorMessage = Mensagens.Intervalo)]
    public double? Longitude { get; init; }

    /// <summary>
    /// Resultado de um simulado feito SEM o app (WhatsApp e telefone), de 0 a 100: percentual de ruas
    /// com situação conhecida em 15 minutos. Opcional.
    /// </summary>
    [Range(0.0, 100.0, ErrorMessage = Mensagens.Intervalo)]
    public double? PercentualSemApp { get; init; }

    /// <summary>Quando o simulado sem o app foi medido. Só vale junto do percentual. Opcional.</summary>
    public DateTimeOffset? SemAppMedidoEm { get; init; }
}

/// <summary>Bairro na listagem, com o total de ruas ativas e a chamada aberta (se houver).</summary>
public sealed record BairroResumoDto(
    int Id,
    string Nome,
    string Cidade,
    string Subprefeitura,
    bool DadosSimulados,
    int TotalDeRuasAtivas,
    int? ChamadaAbertaId,
    string? Uf,
    string? CodigoIbge,
    double? Latitude,
    double? Longitude,
    double? PercentualSemApp,
    DateTime? SemAppMedidoEm);

/// <summary>Bairro com as suas ruas.</summary>
public sealed record BairroDetalheDto(
    int Id,
    string Nome,
    string Cidade,
    string Subprefeitura,
    bool DadosSimulados,
    IReadOnlyList<RuaResumoDto> Ruas,
    string? Uf,
    string? CodigoIbge,
    double? Latitude,
    double? Longitude,
    double? PercentualSemApp,
    DateTime? SemAppMedidoEm);

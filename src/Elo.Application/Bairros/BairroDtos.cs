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
}

/// <summary>Bairro na listagem, com o total de ruas ativas e a chamada aberta (se houver).</summary>
public sealed record BairroResumoDto(
    int Id,
    string Nome,
    string Cidade,
    string Subprefeitura,
    bool DadosSimulados,
    int TotalDeRuasAtivas,
    int? ChamadaAbertaId);

/// <summary>Bairro com as suas ruas.</summary>
public sealed record BairroDetalheDto(
    int Id,
    string Nome,
    string Cidade,
    string Subprefeitura,
    bool DadosSimulados,
    IReadOnlyList<RuaResumoDto> Ruas);

using System.ComponentModel.DataAnnotations;

namespace Elo.Infrastructure.Avisos;

/// <summary>Configuração da busca de avisos no INMET (seção "Avisos" do appsettings.json).</summary>
public sealed class InmetOptions
{
    public const string Secao = "Avisos";

    /// <summary>Avisos ativos do Brasil inteiro (sem chave). O INMET não publica um contrato dessa API.</summary>
    [Required]
    [Url]
    public string UrlDoInmet { get; set; } = "https://apiprevmet3.inmet.gov.br/avisos/ativos";

    /// <summary>Liga o serviço em segundo plano que baixa os avisos de tempos em tempos.</summary>
    public bool AtualizacaoAutomatica { get; set; } = true;

    /// <summary>De quanto em quanto tempo o serviço em segundo plano baixa os avisos.</summary>
    [Range(1, 120)]
    public int IntervaloEmMinutos { get; set; } = 5;

    /// <summary>Até quando os avisos guardados servem sem baixar de novo.</summary>
    [Range(1, 240)]
    public int IdadeMaximaEmMinutos { get; set; } = 10;

    /// <summary>A resposta tem centenas de KB; em rede ruim, o INMET demora.</summary>
    [Range(5, 120)]
    public int TempoLimiteEmSegundos { get; set; } = 20;
}

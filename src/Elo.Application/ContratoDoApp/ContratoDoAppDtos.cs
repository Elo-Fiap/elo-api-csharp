using System.ComponentModel.DataAnnotations;
using Elo.Application.Comum;

namespace Elo.Application.ContratoDoApp;

/// <summary>Configuração das rotas do contrato do app (seção "ContratoDoApp" do appsettings.json).</summary>
public sealed class ContratoDoAppOptions
{
    public const string Secao = "ContratoDoApp";

    /// <summary>"O acesso do piloto vai por URL própria do bairro": cada instalação da API atende um bairro.</summary>
    [Range(1, int.MaxValue)]
    public int BairroId { get; set; } = 1;

    /// <summary>Régua usada no GET /regua quando o app não diz qual. O ESP32 da equipe usa "regua-exemplo".</summary>
    public string ReguaPadrao { get; set; } = "regua-exemplo";
}

/// <summary>GET /situacao: a situação do bairro na chamada atual.</summary>
public sealed record SituacaoDoBairroApp(
    string Bairro,
    string? ChamadaIniciadaEm,
    IReadOnlyList<RuaDoApp> Ruas);

/// <summary>Uma rua para o app. "status" nulo = silêncio (a rua ainda não respondeu nesta chamada).</summary>
public sealed record RuaDoApp(
    string Id,
    string Nome,
    string? Status,
    string? RespondidaEm);

/// <summary>POST /chamadas: o que a liderança da rua respondeu no app (a "chamada" do app).</summary>
public sealed record ChamadaDoAppRequest
{
    /// <summary>Id criado no app. O mesmo id enviado duas vezes vira um registro só.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(64, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Id { get; init; } = string.Empty;

    /// <summary>Id da rua, como veio no GET /situacao.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(20, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string RuaId { get; init; } = string.Empty;

    /// <summary>tudo_bem, agua_subindo ou preciso_ajuda.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [RegularExpression("^(tudo_bem|agua_subindo|preciso_ajuda)$", ErrorMessage = "Use tudo_bem, agua_subindo ou preciso_ajuda.")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Quantas pessoas, se a liderança informou.</summary>
    [Range(1, 500, ErrorMessage = Mensagens.Intervalo)]
    public int? Pessoas { get; init; }

    /// <summary>Texto livre curto (até 200 caracteres). Pode ter dado pessoal digitado sem querer: não vai para o log.</summary>
    [StringLength(200, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string? Observacao { get; init; }

    /// <summary>Quando a liderança respondeu (toISOString do app). Vazio = hora de chegada.</summary>
    public DateTimeOffset? CriadaEm { get; init; }
}

/// <summary>A chamada registrada, devolvida no POST /chamadas (o app precisa de um corpo JSON válido).</summary>
public sealed record ChamadaDoAppDto(
    string Id,
    string RuaId,
    string Status,
    int? Pessoas,
    string? Observacao,
    string CriadaEm,
    string RecebidaEm);

/// <summary>GET /regua: a última leitura da régua de nível, em metros.</summary>
public sealed record LeituraDaReguaApp(
    string ReguaId,
    string Local,
    double NivelMetros,
    double LimiteMetros,
    string MedidaEm);

/// <summary>POST /leituras: o que o ESP32 envia.</summary>
public sealed record LeituraDoEsp32Request
{
    /// <summary>Id fixo no firmware. Ex.: regua-exemplo.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(40, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string ReguaId { get; init; } = string.Empty;

    /// <summary>Altura da água em metros, com 2 casas (nível = altura do sensor - distância medida).</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [Range(0.0, 5.0, ErrorMessage = Mensagens.Intervalo)]
    public double? NivelMetros { get; init; }

    /// <summary>Opcional: o ESP32 sem relógio confiável não manda, e vale a hora de chegada.</summary>
    public DateTimeOffset? MedidaEm { get; init; }

    /// <summary>Opcional. Aceito, mas não guardado nesta versão.</summary>
    [Range(0, 100, ErrorMessage = Mensagens.Intervalo)]
    public int? BateriaPercentual { get; init; }
}

/// <summary>GET /ruas/{id}/plano: o plano de saída da rua.</summary>
public sealed record PlanoDaRuaApp(
    string RuaId,
    string RuaNome,
    string PontoEncontro,
    string RotaSaida,
    string Abrigo,
    IReadOnlyList<string> ItensParaLevar,
    IReadOnlyList<ContatoDeEmergenciaApp> Contatos);

public sealed record ContatoDeEmergenciaApp(string Nome, string Telefone);

using System.ComponentModel.DataAnnotations;
using Elo.Application.Comum;
using Elo.Application.Painel;
using Elo.Domain.Chamadas;

namespace Elo.Application.Chamadas;

/// <summary>Situação da chamada no momento da consulta.</summary>
public enum StatusChamada
{
    Aberta = 1,
    Encerrada = 2
}

/// <summary>Aviso oficial que motivou a chamada real.</summary>
public sealed record AvisoOficialDto
{
    /// <summary>Quem emitiu o aviso. Ex.: INMET.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(40, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Fonte { get; init; } = string.Empty;

    /// <summary>Identificador do aviso na fonte.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(80, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Identificador { get; init; } = string.Empty;

    /// <summary>Tipo de evento. Ex.: Acumulado de Chuva.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(120, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Evento { get; init; } = string.Empty;

    /// <summary>PerigoPotencial, Perigo ou GrandePerigo.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    public SeveridadeAviso? Severidade { get; init; }
}

/// <summary>Dados para abrir uma chamada no bairro.</summary>
public sealed record AbrirChamadaRequest
{
    /// <summary>Real (precisa de aviso oficial) ou Treino (simulado com prazo).</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    public TipoChamada? Tipo { get; init; }

    /// <summary>Obrigatório quando o tipo é Real.</summary>
    public AvisoOficialDto? Aviso { get; init; }

    /// <summary>Só para Treino. Padrão: 30 minutos.</summary>
    [Range(5, 180, ErrorMessage = Mensagens.Intervalo)]
    public int? DuracaoMinutos { get; init; }
}

/// <summary>Filtros da listagem de chamadas (query string).</summary>
public sealed record FiltroDeChamadas
{
    public int? BairroId { get; init; }

    public TipoChamada? Tipo { get; init; }

    public StatusChamada? Status { get; init; }

    [Range(1, 10_000, ErrorMessage = Mensagens.Intervalo)]
    public int Pagina { get; init; } = 1;

    [Range(1, 100, ErrorMessage = Mensagens.Intervalo)]
    public int TamanhoPagina { get; init; } = 10;
}

public sealed record ChamadaDto(
    int Id,
    int BairroId,
    TipoChamada Tipo,
    StatusChamada Status,
    string Pergunta,
    DateTime IniciadaEm,
    DateTime? EncerradaEm,
    DateTime? EncerramentoPrevistoEm,
    AvisoOficialDto? Aviso,
    int? DuracaoMinutos,
    IndicadorDto? Resultado);

/// <summary>Resposta da rua em um toque.</summary>
public sealed record RegistrarRespostaRequest
{
    /// <summary>
    /// Id gerado no celular (texto, até 64 caracteres). Se a mesma resposta for reenviada
    /// (depois de ficar offline), a API devolve a que já está salva, sem duplicar.
    /// </summary>
    [StringLength(64, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string? IdLocal { get; init; }

    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    public int? RuaId { get; init; }

    /// <summary>TudoBem, AguaSubindo ou PrecisoDeAjuda.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    public SituacaoRua? Situacao { get; init; }

    /// <summary>Quantas pessoas, se a liderança informou (opcional, de 1 a 500).</summary>
    [Range(1, 500, ErrorMessage = Mensagens.Intervalo)]
    public int? Pessoas { get; init; }

    /// <summary>Observação opcional, até 200 caracteres. CPF, telefone e e-mail são mascarados antes de salvar.</summary>
    [StringLength(200, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string? Observacao { get; init; }

    /// <summary>App (padrão), Sms, WhatsApp, Ligacao ou Presencial.</summary>
    public CanalResposta Canal { get; init; } = CanalResposta.App;

    /// <summary>Hora em que a liderança respondeu (do celular). Vazio = hora de chegada.</summary>
    public DateTimeOffset? RespondidaEm { get; init; }
}

public sealed record RespostaDto(
    int Id,
    string IdLocal,
    int ChamadaId,
    int RuaId,
    SituacaoRua Situacao,
    int? Pessoas,
    string? Observacao,
    CanalResposta Canal,
    DateTime RespondidaEm,
    DateTime RecebidaEm);

/// <summary>Ação do coordenador para uma rua.</summary>
public sealed record RegistrarAcaoRequest
{
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    public int? RuaId { get; init; }

    /// <summary>ContatoWhatsApp, Ligacao ou VouAteLa.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    public TipoAcao? Tipo { get; init; }
}

public sealed record AcaoDto(int Id, int ChamadaId, int RuaId, TipoAcao Tipo, DateTime RegistradaEm);

/// <summary>Tipos de evento no registro (linha do tempo) da chamada.</summary>
public enum TipoDeEvento
{
    ChamadaAberta = 1,
    Resposta = 2,
    AcaoDoCoordenador = 3,
    SensorAcimaDoLimite = 4,
    ChamadaEncerrada = 5
}

/// <summary>Um evento do registro: o que foi avisado e o que foi feito, com a hora.</summary>
public sealed record EventoDoRegistroDto(
    DateTime Momento,
    TipoDeEvento Tipo,
    int? RuaId,
    string? Rua,
    string Descricao);

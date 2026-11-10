using System.ComponentModel.DataAnnotations;
using Elo.Application.Comum;

namespace Elo.Application.Sensores;

/// <summary>Dados para cadastrar um sensor ESP32.</summary>
public sealed record CriarSensorRequest
{
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    public int? RuaId { get; init; }

    /// <summary>Código gravado no ESP32. Ex.: ESP32-PANTANAL-01.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(40, ErrorMessage = Mensagens.TamanhoMaximo)]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Use só letras, números, hífen e sublinhado no código.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Onde o sensor está. Ex.: ponte do córrego.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(150, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Local { get; init; } = string.Empty;

    /// <summary>Acima deste nível (cm) a leitura vira alerta.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [Range(1, 500, ErrorMessage = Mensagens.Intervalo)]
    public double? LimiteAlertaCm { get; init; }
}

/// <summary>Dados para atualizar um sensor (a rua e o código não mudam).</summary>
public sealed record AtualizarSensorRequest
{
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [StringLength(150, ErrorMessage = Mensagens.TamanhoMaximo)]
    public string Local { get; init; } = string.Empty;

    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [Range(1, 500, ErrorMessage = Mensagens.Intervalo)]
    public double? LimiteAlertaCm { get; init; }

    public bool Ativo { get; init; } = true;
}

/// <summary>Leitura enviada pelo ESP32.</summary>
public sealed record RegistrarLeituraRequest
{
    /// <summary>Nível da água em centímetros, calculado pelo ESP32 a partir do HC-SR04.</summary>
    [Required(ErrorMessage = Mensagens.Obrigatorio)]
    [Range(0, 500, ErrorMessage = Mensagens.Intervalo)]
    public double? NivelCm { get; init; }

    /// <summary>Hora da medição. Vazio = hora de chegada na API.</summary>
    public DateTimeOffset? MedidaEm { get; init; }
}

public sealed record LeituraDto(
    int Id,
    int SensorId,
    double NivelCm,
    double LimiteCm,
    bool AcimaDoLimite,
    DateTime MedidaEm,
    DateTime RecebidaEm);

public sealed record SensorDto(
    int Id,
    int RuaId,
    string Codigo,
    string Local,
    double LimiteAlertaCm,
    bool Ativo,
    LeituraDto? UltimaLeitura);

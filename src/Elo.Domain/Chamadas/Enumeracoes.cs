namespace Elo.Domain.Chamadas;

/// <summary>Tipo da chamada. Cada tipo é uma subclasse de <see cref="Chamada"/>.</summary>
public enum TipoChamada
{
    /// <summary>Disparada por um aviso oficial (INMET, Defesa Civil).</summary>
    Real = 1,

    /// <summary>Simulado para a liderança não esquecer o app entre uma chuva e outra.</summary>
    Treino = 2
}

/// <summary>As três respostas possíveis da rua, em um toque.</summary>
public enum SituacaoRua
{
    TudoBem = 1,
    AguaSubindo = 2,
    PrecisoDeAjuda = 3
}

/// <summary>Por onde a resposta chegou.</summary>
public enum CanalResposta
{
    /// <summary>A liderança tocou no botão do app.</summary>
    App = 1,

    /// <summary>Sem internet: o app abre um SMS pronto e o coordenador registra.</summary>
    Sms = 2,

    /// <summary>A rua respondeu no WhatsApp e o coordenador registrou.</summary>
    WhatsApp = 3,

    /// <summary>A rua respondeu por ligação e o coordenador registrou.</summary>
    Ligacao = 4,

    /// <summary>Alguém foi até a rua e registrou pessoalmente.</summary>
    Presencial = 5
}

/// <summary>O que o coordenador fez por uma rua durante a chamada.</summary>
public enum TipoAcao
{
    ContatoWhatsApp = 1,
    Ligacao = 2,

    /// <summary>"Vou até lá": alguém da coordenação está indo até a rua.</summary>
    VouAteLa = 3
}

/// <summary>Níveis de aviso usados pelo INMET.</summary>
public enum SeveridadeAviso
{
    PerigoPotencial = 1,
    Perigo = 2,
    GrandePerigo = 3
}

public static class SeveridadeAvisoExtensoes
{
    public static string Descricao(this SeveridadeAviso severidade) => severidade switch
    {
        SeveridadeAviso.PerigoPotencial => "perigo potencial",
        SeveridadeAviso.Perigo => "perigo",
        SeveridadeAviso.GrandePerigo => "grande perigo",
        _ => severidade.ToString()
    };
}

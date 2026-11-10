namespace Elo.Application.Comum;

/// <summary>O recurso pedido não existe. A API devolve HTTP 404.</summary>
public sealed class RecursoNaoEncontradoException(string mensagem) : Exception(mensagem)
{
    public static RecursoNaoEncontradoException Bairro(int id) => new($"Bairro {id} não encontrado.");

    public static RecursoNaoEncontradoException Rua(int id) => new($"Rua {id} não encontrada.");

    public static RecursoNaoEncontradoException Chamada(int id) => new($"Chamada {id} não encontrada.");

    public static RecursoNaoEncontradoException Resposta(int id) => new($"Resposta {id} não encontrada.");

    public static RecursoNaoEncontradoException Sensor(int id) => new($"Sensor {id} não encontrado.");
}

/// <summary>A operação conflita com o estado atual (ex.: já existe chamada aberta). A API devolve HTTP 409.</summary>
public sealed class ConflitoException(string mensagem) : Exception(mensagem);

/// <summary>Um serviço externo (o INMET) não respondeu e não há dado guardado. A API devolve HTTP 503.</summary>
public sealed class FonteIndisponivelException(string mensagem, Exception? causa = null) : Exception(mensagem, causa);

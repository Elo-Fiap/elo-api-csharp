namespace Elo.Domain.Comum;

/// <summary>
/// Lançada quando uma regra do domínio é violada
/// (ex.: uma resposta dada fora do horário da chamada).
/// A API transforma esta exceção em HTTP 422 (Unprocessable Entity).
/// </summary>
public sealed class RegraDeNegocioException(string mensagem) : Exception(mensagem);

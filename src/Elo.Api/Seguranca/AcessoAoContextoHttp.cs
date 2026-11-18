using Elo.Seguranca.Taint;

namespace Elo.Api.Seguranca;

/// <summary>
/// Entrega o contexto de contaminação da requisição em andamento (guardado em <c>HttpContext.Items</c>).
/// Fora de uma requisição (semente do banco, workers) não há <c>HttpContext</c> e o resultado é nulo.
/// </summary>
public sealed class AcessoAoContextoHttp(IHttpContextAccessor acessor) : IAcessoAoContexto
{
    private const string ChaveNosItens = "Elo.Seguranca.ContextoDeContaminacao";

    public ContextoDeContaminacao? Atual =>
        acessor.HttpContext?.Items.TryGetValue(ChaveNosItens, out var valor) == true
            ? valor as ContextoDeContaminacao
            : null;

    /// <summary>Abre o contexto da requisição (chamado uma vez, pelo middleware de entrada).</summary>
    public static ContextoDeContaminacao Abrir(HttpContext http)
    {
        var contexto = new ContextoDeContaminacao();
        http.Items[ChaveNosItens] = contexto;
        return contexto;
    }
}

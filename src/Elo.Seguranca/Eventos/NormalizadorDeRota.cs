namespace Elo.Seguranca.Eventos;

/// <summary>
/// Troca os ids da rota por {id}: "POST /api/chamadas/12/respostas" vira "POST /api/chamadas/{id}/respostas".
/// O caminho vem de quem chama a API e vai para o evento, o painel e o prompt do LLM. Por isso só sobrevive
/// o segmento curto feito só de letras e hífen (como as rotas da API); qualquer outro (número, GUID, CPF,
/// e-mail, texto longo) vira {id}.
/// </summary>
public static class NormalizadorDeRota
{
    private const int TamanhoMaximoDoSegmento = 30;

    public static string Normalizar(string metodo, string caminho)
    {
        var segmentos = caminho.Split('/').Select(s => s.Length == 0 || EhNomeDeRota(s) ? s : "{id}");
        return $"{metodo} {string.Join('/', segmentos)}";
    }

    private static bool EhNomeDeRota(string segmento) =>
        segmento.Length <= TamanhoMaximoDoSegmento && segmento.All(c => char.IsAsciiLetter(c) || c is '-' or '_');
}

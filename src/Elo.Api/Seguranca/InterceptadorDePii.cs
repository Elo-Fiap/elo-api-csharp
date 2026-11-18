using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;

namespace Elo.Api.Seguranca;

/// <summary>
/// Middleware de ENTRADA (a "source" da taint analysis): lê o JSON e a query string de cada requisição,
/// acha dado sensível com o <see cref="MotorDePii"/> e marca cada valor no contexto da requisição.
/// Não reescreve o corpo: marcar na entrada e mascarar antes de cada saída (log, banco, resposta) mantém os contratos
/// do app e do painel intactos. JSON inválido passa sem marcar (o model binding responde 400).
/// </summary>
public sealed class InterceptadorDePii(RequestDelegate proximo, MotorDePii motor, IRegistroDeEventos registro)
{
    /// <summary>Corpos maiores que isso não são lidos (a API só recebe JSON pequeno; evita gastar memória à toa).</summary>
    public const int TamanhoMaximoDoCorpo = 256 * 1024;

    public async Task InvokeAsync(HttpContext http)
    {
        var tinta = AcessoAoContextoHttp.Abrir(http);
        var achados = new Achados();

        AnalisarQuery(http.Request, tinta, achados);

        if (TemCorpoJson(http.Request))
        {
            await AnalisarCorpoAsync(http.Request, tinta, achados);
        }

        if (achados.Quantidade > 0)
        {
            registro.Registrar(
                EtapaDoEvento.Entrada,
                sink: null,
                AcaoDeSeguranca.Marcado,
                achados.Tipos,
                achados.Quantidade,
                NormalizadorDeRota.Normalizar(http.Request.Method, http.Request.Path));
        }

        await proximo(http);
    }

    private static bool TemCorpoJson(HttpRequest requisicao)
    {
        if (!(HttpMethods.IsPost(requisicao.Method) || HttpMethods.IsPut(requisicao.Method) || HttpMethods.IsPatch(requisicao.Method)))
        {
            return false;
        }

        var tipo = requisicao.ContentType;
        return tipo is not null
            && (tipo.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                || tipo.Contains("+json", StringComparison.OrdinalIgnoreCase));
    }

    private async Task AnalisarCorpoAsync(HttpRequest requisicao, ContextoDeContaminacao tinta, Achados achados)
    {
        if (requisicao.ContentLength > TamanhoMaximoDoCorpo)
        {
            return;
        }

        requisicao.EnableBuffering();
        // Buffer emprestado do pool: alocar 256 KB novos a cada POST pesaria no coletor de lixo.
        var bytes = ArrayPool<byte>.Shared.Rent(TamanhoMaximoDoCorpo + 1);
        try
        {
            // Lê no máximo o limite + 1 byte: corpo em pedaços (sem Content-Length) maior que o limite também é ignorado.
            var lidos = 0;
            int n;
            while (lidos <= TamanhoMaximoDoCorpo
                   && (n = await requisicao.Body.ReadAsync(bytes.AsMemory(lidos, TamanhoMaximoDoCorpo + 1 - lidos), requisicao.HttpContext.RequestAborted)) > 0)
            {
                lidos += n;
            }

            if (lidos == 0 || lidos > TamanhoMaximoDoCorpo)
            {
                return;
            }

            JsonNode? raiz;
            try
            {
                raiz = JsonNode.Parse(
                    bytes.AsSpan(0, lidos),
                    documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException)
            {
                return;
            }

            Percorrer(raiz, chave: null, caminho: "$", tinta, achados);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            requisicao.Body.Position = 0;
        }
    }

    private void AnalisarQuery(HttpRequest requisicao, ContextoDeContaminacao tinta, Achados achados)
    {
        foreach (var (chave, valores) in requisicao.Query)
        {
            if (motor.EhChaveIsenta(chave))
            {
                continue;
            }

            foreach (var valor in valores)
            {
                AnalisarTexto(valor, chave, ModoDeAnalise.CampoEstruturado, $"query {chave}", tinta, achados);
            }
        }
    }

    private void Percorrer(JsonNode? no, string? chave, string caminho, ContextoDeContaminacao tinta, Achados achados)
    {
        switch (no)
        {
            case JsonObject objeto:
                foreach (var (nome, filho) in objeto)
                {
                    if (!motor.EhChaveIsenta(nome))
                    {
                        Percorrer(filho, nome, $"{caminho}.{nome}", tinta, achados);
                    }
                }

                break;

            case JsonArray lista:
                for (var i = 0; i < lista.Count; i++)
                {
                    Percorrer(lista[i], chave, $"{caminho}[{i}]", tinta, achados);
                }

                break;

            case JsonValue valor when valor.TryGetValue<string>(out var texto):
                AnalisarTexto(texto, chave, chave is null ? ModoDeAnalise.CampoEstruturado : motor.ModoDaChave(chave), $"corpo {caminho}", tinta, achados);
                break;

            // "cpf": 12345678909 ou "pin": 1234 sem aspas: o número só conta quando o nome do campo diz o que ele é
            // (senão qualquer contagem ou data em milissegundos viraria telefone).
            case JsonValue numero when numero.GetValueKind() == JsonValueKind.Number && chave is not null && motor.ClassificarChave(chave) is not null:
                AnalisarTexto(numero.ToJsonString(), chave, ModoDeAnalise.CampoEstruturado, $"corpo {caminho}", tinta, achados);
                break;
        }
    }

    /// <summary>
    /// Se o nome do campo já diz o que ele guarda (senha, laudo, documento...), o valor inteiro é marcado com aquele tipo;
    /// senão o motor procura dentro do texto e cada trecho achado é marcado (o trecho original, não a máscara).
    /// </summary>
    private void AnalisarTexto(string? texto, string? chave, ModoDeAnalise modo, string origem, ContextoDeContaminacao tinta, Achados achados)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return;
        }

        if (chave is not null && motor.ClassificarChave(chave) is { } tipoDaChave)
        {
            tinta.Marcar(texto, tipoDaChave, origem);
            achados.Somar(tipoDaChave);
            return;
        }

        foreach (var achado in motor.Analisar(texto, modo).Achados)
        {
            tinta.Marcar(texto.Substring(achado.Inicio, achado.Tamanho), achado.Tipo, origem);
            achados.Somar(achado.Tipo);
        }
    }

    private sealed class Achados
    {
        private readonly List<TipoDePii> _tipos = [];

        public IReadOnlyList<TipoDePii> Tipos => _tipos;
        public int Quantidade { get; private set; }

        public void Somar(TipoDePii tipo)
        {
            Quantidade++;
            if (!_tipos.Contains(tipo))
            {
                _tipos.Add(tipo);
            }
        }
    }
}

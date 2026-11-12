using System.Text.RegularExpressions;
using Elo.Domain.Comum;
using Elo.Domain.Ruas;

namespace Elo.Domain.Bairros;

/// <summary>
/// Bairro acompanhado pelo Elo. O coordenador do bairro abre as chamadas
/// e vê, no painel, como cada rua está.
/// </summary>
public partial class Bairro : Entidade
{
    public const int TamanhoMaximoNome = 120;
    public const int TamanhoMaximoCidade = 80;
    public const int TamanhoMaximoSubprefeitura = 80;
    public const int TamanhoDaUf = 2;
    public const int TamanhoDoCodigoIbge = 7;

    // Encapsulamento: a lista real é privada; quem está fora só lê.
    private readonly List<Rua> _ruas = [];

    public string Nome { get; private set; } = string.Empty;
    public string Cidade { get; private set; } = string.Empty;

    /// <summary>A subprefeitura é a unidade usada pelo modelo de ML (nível de atenção).</summary>
    public string Subprefeitura { get; private set; } = string.Empty;

    /// <summary>Na GS as ruas e as respostas são simuladas, e o app avisa isso na tela.</summary>
    public bool DadosSimulados { get; private set; }

    /// <summary>Sigla do estado, em maiúsculas (2 letras). Ex.: SP. Opcional.</summary>
    public string? Uf { get; private set; }

    /// <summary>Código IBGE do município (7 dígitos, em texto). É com ele que o painel busca os avisos do INMET. Opcional.</summary>
    public string? CodigoIbge { get; private set; }

    /// <summary>Centro do bairro no mapa. Opcional, mas sempre junto da longitude.</summary>
    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    /// <summary>
    /// Resultado de um simulado feito SEM o app (WhatsApp e telefone), de 0 a 100: % de ruas
    /// com situação conhecida em 15 minutos. É a base de comparação do indicador de impacto.
    /// </summary>
    public double? PercentualSemApp { get; private set; }

    /// <summary>Quando o simulado sem o app foi medido (UTC). Só existe junto do percentual.</summary>
    public DateTime? SemAppMedidoEm { get; private set; }

    public IReadOnlyCollection<Rua> Ruas => _ruas.AsReadOnly();

    private Bairro()
    {
        // Usado pelo EF Core para materializar o objeto vindo do banco.
    }

    public Bairro(
        string nome,
        string cidade,
        string subprefeitura,
        bool dadosSimulados,
        string? uf = null,
        string? codigoIbge = null,
        double? latitude = null,
        double? longitude = null,
        double? percentualSemApp = null,
        DateTime? semAppMedidoEm = null)
    {
        Atualizar(nome, cidade, subprefeitura, dadosSimulados, uf, codigoIbge, latitude, longitude, percentualSemApp, semAppMedidoEm);
    }

    public void Atualizar(
        string nome,
        string cidade,
        string subprefeitura,
        bool dadosSimulados,
        string? uf = null,
        string? codigoIbge = null,
        double? latitude = null,
        double? longitude = null,
        double? percentualSemApp = null,
        DateTime? semAppMedidoEm = null)
    {
        // Valida tudo antes de mudar qualquer campo: se algo estiver errado, o bairro fica como estava.
        var nomeValido = Guarda.TextoObrigatorio(nome, "Nome do bairro", TamanhoMaximoNome);
        var cidadeValida = Guarda.TextoObrigatorio(cidade, "Cidade", TamanhoMaximoCidade);
        var subprefeituraValida = Guarda.TextoObrigatorio(subprefeitura, "Subprefeitura", TamanhoMaximoSubprefeitura);
        var ufValida = NormalizarUf(uf);
        var ibgeValido = NormalizarCodigoIbge(codigoIbge);
        var (latitudeValida, longitudeValida) = Guarda.ParDeCoordenadas(latitude, longitude, "Bairro");
        var (percentualValido, medidoEmValido) = ValidarSimuladoSemApp(percentualSemApp, semAppMedidoEm);

        Nome = nomeValido;
        Cidade = cidadeValida;
        Subprefeitura = subprefeituraValida;
        DadosSimulados = dadosSimulados;
        Uf = ufValida;
        CodigoIbge = ibgeValido;
        Latitude = latitudeValida;
        Longitude = longitudeValida;
        PercentualSemApp = percentualValido;
        SemAppMedidoEm = medidoEmValido;
    }

    /// <summary>UF vira maiúscula; vazio é "sem UF". Qualquer outra coisa que não sejam 2 letras é recusada.</summary>
    public static string? NormalizarUf(string? uf)
    {
        if (string.IsNullOrWhiteSpace(uf))
        {
            return null;
        }

        var sigla = uf.Trim();
        return SiglaDeUf().IsMatch(sigla)
            ? sigla.ToUpperInvariant()
            : throw new RegraDeNegocioException($"UF deve ter {TamanhoDaUf} letras. Ex.: SP.");
    }

    /// <summary>O código do município no IBGE tem 7 dígitos (guardado como texto, para não perder zeros à esquerda).</summary>
    public static string? NormalizarCodigoIbge(string? codigoIbge)
    {
        if (string.IsNullOrWhiteSpace(codigoIbge))
        {
            return null;
        }

        var codigo = codigoIbge.Trim();
        return CodigoDeSeteDigitos().IsMatch(codigo)
            ? codigo
            : throw new RegraDeNegocioException($"Código IBGE deve ter {TamanhoDoCodigoIbge} dígitos. Ex.: 3550308.");
    }

    private static (double? Percentual, DateTime? MedidoEm) ValidarSimuladoSemApp(double? percentual, DateTime? medidoEm)
    {
        if (percentual is null)
        {
            return medidoEm is null
                ? (null, null)
                : throw new RegraDeNegocioException("A data do simulado sem o app só vale junto do percentual.");
        }

        var valido = Guarda.Intervalo(percentual.Value, 0, 100, "Percentual sem o app");
        DateTime? dataUtc = medidoEm is DateTime data
            ? data.Kind == DateTimeKind.Local ? data.ToUniversalTime() : DateTime.SpecifyKind(data, DateTimeKind.Utc)
            : null;

        return (valido, dataUtc);
    }

    [GeneratedRegex("^[A-Za-z]{2}$")]
    private static partial Regex SiglaDeUf();

    [GeneratedRegex("^[0-9]{7}$")]
    private static partial Regex CodigoDeSeteDigitos();

    public override string ToString() => $"{Nome} - {Cidade}";
}

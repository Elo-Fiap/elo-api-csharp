using Elo.Domain.Comum;

namespace Elo.Application.Avisos;

/// <summary>
/// GET /avisos: um aviso do INMET como o app usa (os mesmos campos do tipo Aviso do elo-app).
/// O app recebe algumas centenas de bytes em vez da resposta inteira do INMET.
/// </summary>
public sealed record AvisoDoAppDto(
    string Id,
    string Titulo,
    string Severidade,
    int Gravidade,
    string Inicio,
    string Fim,
    string Quando,
    IReadOnlyList<string> Riscos,
    IReadOnlyList<string> Instrucoes);

public interface IAvisoService
{
    Task<IReadOnlyList<AvisoDoAppDto>> ListarDoMunicipioAsync(string codigoIbge, CancellationToken cancellationToken = default);
}

/// <summary>Filtra os avisos do INMET para um município, com as mesmas regras do app.</summary>
public sealed class AvisoService(IFonteDeAvisos fonte) : IAvisoService
{
    public async Task<IReadOnlyList<AvisoDoAppDto>> ListarDoMunicipioAsync(string codigoIbge, CancellationToken cancellationToken = default)
    {
        var codigo = (codigoIbge ?? string.Empty).Trim();
        if (!AvisoDoInmet.EhCodigoIbge(codigo))
        {
            throw new RegraDeNegocioException("Informe o código IBGE do município com 7 dígitos. Ex.: 3550308.");
        }

        var avisos = await fonte.ObterAvisosAsync(cancellationToken);

        // LINQ: só o que vale para o município, sem repetir o mesmo aviso em "hoje" e "futuro",
        // do mais grave ao mais leve e, no empate, o que começa antes.
        return [.. avisos
            .Where(aviso => aviso.ValePara(codigo))
            .DistinctBy(aviso => aviso.Id)
            .OrderByDescending(aviso => aviso.Gravidade)
            .ThenBy(aviso => aviso.InicioParaOrdenar)
            .Select(aviso => new AvisoDoAppDto(
                aviso.Id,
                aviso.Titulo,
                aviso.Severidade,
                aviso.Gravidade,
                aviso.Inicio,
                aviso.Fim,
                aviso.Quando,
                aviso.Riscos,
                aviso.Instrucoes))];
    }
}

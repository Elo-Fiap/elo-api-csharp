using System.Globalization;
using Elo.Application.Avisos;
using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Sensores;
using Microsoft.Extensions.Logging;

namespace Elo.Application.PainelAdmin;

/// <summary>Monta o instantâneo do painel de administração: uma leitura só, sem escrever nada.</summary>
public sealed class PainelAdminService(
    IPainelAdminRepository repositorio,
    IAvisoService avisos,
    TimeProvider relogio,
    ILogger<PainelAdminService> logger) : IPainelAdminService
{
    public const int DiasMinimos = 1;
    public const int DiasMaximos = 30;
    public const int HorasDeLeituras = 72;
    public const int MaximoDeLeiturasPorSensor = 500;

    public async Task<InstantaneoDto> ObterInstantaneoAsync(int dias, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dias, DiasMinimos);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dias, DiasMaximos);

        var agora = relogio.GetUtcNow().UtcDateTime;

        // As consultas rodam uma de cada vez: o DbContext não aceita duas ao mesmo tempo.
        var bairros = await repositorio.ListarBairrosComRuasAsync(cancellationToken);
        var sensores = await repositorio.ListarSensoresComLeiturasAsync(
            agora.AddHours(-HorasDeLeituras), MaximoDeLeiturasPorSensor, cancellationToken);
        var chamadas = await repositorio.ListarChamadasComDetalhesAsync(agora, agora.AddDays(-dias), cancellationToken);
        var avisosDosBairros = await ListarAvisosAsync(bairros, cancellationToken);

        return new InstantaneoDto(
            agora,
            [.. bairros.Select(ParaAdminDto)],
            [.. sensores.Select(ParaAdminDto)],
            [.. chamadas.Select(chamada => ParaAdminDto(chamada, agora))],
            avisosDosBairros);
    }

    /// <summary>
    /// Avisos ativos do INMET para o município de cada bairro. O mesmo aviso, valendo para bairros de
    /// UFs diferentes, aparece uma vez só, com todas as UFs. Se o INMET falhar, a lista vem vazia:
    /// o painel continua funcionando sem os avisos.
    /// </summary>
    private async Task<IReadOnlyList<AvisoAdminDto>> ListarAvisosAsync(
        IReadOnlyList<Bairro> bairros,
        CancellationToken cancellationToken)
    {
        var porAviso = new Dictionary<string, (AvisoDoAppDto Aviso, SortedSet<string> Ufs)>();

        // Os bairros do mesmo município (mesmo código IBGE) perguntam uma vez só.
        foreach (var municipio in bairros
            .Where(bairro => AvisoDoInmet.EhCodigoIbge(bairro.CodigoIbge))
            .GroupBy(bairro => bairro.CodigoIbge!))
        {
            IReadOnlyList<AvisoDoAppDto> doMunicipio;
            try
            {
                doMunicipio = await avisos.ListarDoMunicipioAsync(municipio.Key, cancellationToken);
            }
            catch (Exception erro) when (!cancellationToken.IsCancellationRequested)
            {
                // Sem INMET e sem lista guardada: não adianta insistir bairro por bairro.
                logger.LogWarning(erro, "Painel admin: avisos do INMET indisponíveis; a lista segue vazia.");
                return [];
            }

            var ufs = municipio.Select(bairro => bairro.Uf).OfType<string>();
            foreach (var aviso in doMunicipio)
            {
                if (!porAviso.TryGetValue(aviso.Id, out var existente))
                {
                    existente = (aviso, new SortedSet<string>(StringComparer.Ordinal));
                    porAviso[aviso.Id] = existente;
                }

                existente.Ufs.UnionWith(ufs);
            }
        }

        return [.. porAviso.Values
            .OrderByDescending(item => item.Aviso.Gravidade)
            .ThenBy(item => ParaUtc(item.Aviso.Inicio) ?? DateTime.MinValue)
            .ThenBy(item => item.Aviso.Id, StringComparer.Ordinal)
            .Select(item => new AvisoAdminDto(
                item.Aviso.Id,
                item.Aviso.Titulo,
                item.Aviso.Severidade,
                item.Aviso.Gravidade,
                ParaUtc(item.Aviso.Inicio),
                ParaUtc(item.Aviso.Fim),
                [.. item.Ufs]))];
    }

    /// <summary>O INMET manda datas com fuso (-03:00); o painel recebe tudo em UTC. Data ilegível vira nulo.</summary>
    private static DateTime? ParaUtc(string? data) =>
        DateTimeOffset.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.None, out var valor)
            ? valor.UtcDateTime
            : null;

    private static BairroAdminDto ParaAdminDto(Bairro bairro) => new(
        bairro.Id,
        bairro.Nome,
        bairro.Cidade,
        bairro.Uf,
        bairro.CodigoIbge,
        bairro.Latitude,
        bairro.Longitude,
        bairro.DadosSimulados,
        bairro.PercentualSemApp,
        bairro.SemAppMedidoEm,
        [.. bairro.Ruas.OrderBy(rua => rua.Id).Select(rua => new RuaAdminDto(
            rua.Id,
            rua.Nome,
            rua.Microarea,
            rua.NivelRisco,
            rua.Ativa,
            rua.Latitude,
            rua.Longitude,
            rua.Tracado is null ? null : [.. rua.Tracado.Select(ponto => new[] { ponto.Longitude, ponto.Latitude })]))]);

    private static SensorAdminDto ParaAdminDto(SensorComLeituras item) => new(
        item.Sensor.Id,
        item.Sensor.Codigo,
        item.Sensor.RuaId,
        item.BairroId,
        item.Sensor.Local,
        item.Sensor.Latitude,
        item.Sensor.Longitude,
        CentimetrosParaMetros(item.Sensor.LimiteAlertaCm),
        item.Sensor.Ativo,
        [.. item.Leituras
            .OrderBy(leitura => leitura.MedidaEm)
            .Select(leitura => new LeituraAdminDto(CentimetrosParaMetros(leitura.NivelCm), leitura.MedidaEm))]);

    private static ChamadaAdminDto ParaAdminDto(Chamada chamada, DateTime agora) => new(
        chamada.Id,
        chamada.BairroId,
        chamada.Tipo,
        chamada.IniciadaEm,
        // Treino que passou do prazo está encerrado, mesmo sem ninguém ter encerrado à mão.
        chamada.EstaAbertaEm(agora) ? null : chamada.FimDaJanela,
        chamada.AvisoQueMotivou?.Evento,
        [.. chamada.Respostas
            .OrderBy(resposta => resposta.RespondidaEm)
            .ThenBy(resposta => resposta.Id)
            .Select(resposta => new RespostaAdminDto(
                resposta.Id,
                resposta.RuaId,
                resposta.Situacao,
                resposta.Pessoas,
                resposta.RespondidaEm,
                resposta.RecebidaEm))],
        [.. chamada.Acoes
            .OrderBy(acao => acao.RegistradaEm)
            .ThenBy(acao => acao.Id)
            .Select(acao => new AcaoAdminDto(acao.RuaId, acao.Tipo, acao.RegistradaEm))]);

    /// <summary>A API guarda em centímetros; o painel recebe em metros (como o app e o ESP32 falam).</summary>
    private static double CentimetrosParaMetros(double centimetros) => Math.Round(centimetros / 100, 4);
}

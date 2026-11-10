using Elo.Application.Abstracoes;
using Elo.Application.Comum;
using Elo.Domain.Comum;
using Elo.Domain.Sensores;
using Microsoft.Extensions.Logging;

namespace Elo.Application.Sensores;

public interface ISensorService
{
    Task<IReadOnlyList<SensorDto>> ListarAsync(int? ruaId, CancellationToken cancellationToken = default);

    Task<SensorDto> ObterAsync(int id, CancellationToken cancellationToken = default);

    Task<SensorDto> CriarAsync(CriarSensorRequest request, CancellationToken cancellationToken = default);

    Task<SensorDto> AtualizarAsync(int id, AtualizarSensorRequest request, CancellationToken cancellationToken = default);

    Task RemoverAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Recebe a leitura enviada pelo ESP32.</summary>
    Task<LeituraDto> RegistrarLeituraAsync(int id, RegistrarLeituraRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeituraDto>> ListarLeiturasAsync(int id, int quantidade, CancellationToken cancellationToken = default);

    Task<LeituraDto> ObterLeituraAsync(int id, int leituraId, CancellationToken cancellationToken = default);
}

public sealed class SensorService(
    ISensorRepository sensores,
    IRuaRepository ruas,
    IUnitOfWork unitOfWork,
    TimeProvider relogio,
    ILogger<SensorService> logger) : ISensorService
{
    public const int MaximoDeLeiturasPorConsulta = 500;

    public async Task<IReadOnlyList<SensorDto>> ListarAsync(int? ruaId, CancellationToken cancellationToken = default)
    {
        var lista = await sensores.ListarComUltimaLeituraAsync(ruaId, cancellationToken);
        return [.. lista.Select(item => item.ParaDto())];
    }

    public async Task<SensorDto> ObterAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await sensores.ObterComUltimaLeituraAsync(id, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Sensor(id);

        return item.ParaDto();
    }

    public async Task<SensorDto> CriarAsync(CriarSensorRequest request, CancellationToken cancellationToken = default)
    {
        var ruaId = request.RuaId ?? throw new RegraDeNegocioException("Informe a rua do sensor.");
        if (!await ruas.ExisteAsync(ruaId, cancellationToken))
        {
            throw RecursoNaoEncontradoException.Rua(ruaId);
        }

        var codigo = Sensor.NormalizarCodigo(request.Codigo);
        if (await sensores.ExisteComCodigoAsync(codigo, cancellationToken))
        {
            throw new ConflitoException($"Já existe um sensor com o código {codigo}.");
        }

        var sensor = new Sensor(ruaId, codigo, request.Local, request.LimiteAlertaCm ?? 0);
        sensores.Adicionar(sensor);
        await unitOfWork.SalvarAsync(cancellationToken);

        logger.LogInformation("Sensor {Codigo} cadastrado na rua {RuaId}.", sensor.Codigo, ruaId);
        return new SensorComLeitura(sensor, null).ParaDto();
    }

    public async Task<SensorDto> AtualizarAsync(int id, AtualizarSensorRequest request, CancellationToken cancellationToken = default)
    {
        var sensor = await ObterSensorAsync(id, cancellationToken);
        sensor.Atualizar(request.Local, request.LimiteAlertaCm ?? 0, request.Ativo);
        await unitOfWork.SalvarAsync(cancellationToken);
        return await ObterAsync(id, cancellationToken);
    }

    public async Task RemoverAsync(int id, CancellationToken cancellationToken = default)
    {
        var sensor = await ObterSensorAsync(id, cancellationToken);
        sensores.Remover(sensor);
        await unitOfWork.SalvarAsync(cancellationToken);
        logger.LogInformation("Sensor {SensorId} excluído com as suas leituras.", id);
    }

    public async Task<LeituraDto> RegistrarLeituraAsync(int id, RegistrarLeituraRequest request, CancellationToken cancellationToken = default)
    {
        var sensor = await ObterSensorAsync(id, cancellationToken);
        var agora = relogio.GetUtcNow().UtcDateTime;

        var leitura = sensor.RegistrarLeitura(
            request.NivelCm ?? throw new RegraDeNegocioException("Informe o nível da água em centímetros."),
            request.MedidaEm?.UtcDateTime ?? agora,
            agora);

        await unitOfWork.SalvarAsync(cancellationToken);

        if (leitura.AcimaDoLimite)
        {
            logger.LogWarning(
                "Sensor {Codigo} acima do limite: {Nivel} cm (limite {Limite} cm).",
                sensor.Codigo,
                leitura.NivelCm,
                leitura.LimiteNaHoraCm);
        }

        return leitura.ParaDto();
    }

    public async Task<IReadOnlyList<LeituraDto>> ListarLeiturasAsync(int id, int quantidade, CancellationToken cancellationToken = default)
    {
        if (!await sensores.ExisteAsync(id, cancellationToken))
        {
            throw RecursoNaoEncontradoException.Sensor(id);
        }

        var leituras = await sensores.ListarLeiturasAsync(
            id,
            Math.Clamp(quantidade, 1, MaximoDeLeiturasPorConsulta),
            cancellationToken);

        return [.. leituras.Select(leitura => leitura.ParaDto())];
    }

    public async Task<LeituraDto> ObterLeituraAsync(int id, int leituraId, CancellationToken cancellationToken = default)
    {
        var leitura = await sensores.ObterLeituraAsync(id, leituraId, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Leitura(leituraId);

        return leitura.ParaDto();
    }

    private async Task<Sensor> ObterSensorAsync(int id, CancellationToken cancellationToken) =>
        await sensores.ObterPorIdAsync(id, cancellationToken) ?? throw RecursoNaoEncontradoException.Sensor(id);
}

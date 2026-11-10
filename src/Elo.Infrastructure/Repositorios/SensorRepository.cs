using Elo.Application.Abstracoes;
using Elo.Domain.Sensores;
using Elo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Elo.Infrastructure.Repositorios;

public sealed class SensorRepository(EloDbContext contexto) : RepositoryBase<Sensor>(contexto), ISensorRepository
{
    public async Task<IReadOnlyList<SensorComLeitura>> ListarComUltimaLeituraAsync(int? ruaId, CancellationToken cancellationToken = default)
    {
        var consulta = Conjunto.AsNoTracking();

        if (ruaId is int id)
        {
            consulta = consulta.Where(sensor => sensor.RuaId == id);
        }

        return await ComUltimaLeitura(consulta.OrderBy(sensor => sensor.Codigo)).ToListAsync(cancellationToken);
    }

    public Task<SensorComLeitura?> ObterComUltimaLeituraAsync(int id, CancellationToken cancellationToken = default) =>
        ComUltimaLeitura(Conjunto.AsNoTracking().Where(sensor => sensor.Id == id)).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExisteComCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        Conjunto.AnyAsync(sensor => sensor.Codigo == codigo, cancellationToken);

    public Task<Sensor?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        Conjunto.FirstOrDefaultAsync(sensor => sensor.Codigo == codigo, cancellationToken);

    public Task<SensorComLeitura?> ObterComUltimaLeituraPorCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        ComUltimaLeitura(Conjunto.AsNoTracking().Where(sensor => sensor.Codigo == codigo)).FirstOrDefaultAsync(cancellationToken);

    public async Task<SensorComLeitura?> ObterLeituraMaisRecenteDoBairroAsync(int bairroId, CancellationToken cancellationToken = default)
    {
        var sensoresDoBairro = await ComUltimaLeitura(Contexto.Ruas
                .AsNoTracking()
                .Where(rua => rua.BairroId == bairroId)
                .SelectMany(rua => rua.Sensores)
                .Where(sensor => sensor.Ativo))
            .ToListAsync(cancellationToken);

        // Poucos sensores por bairro: a escolha da leitura mais nova é feita na memória.
        return sensoresDoBairro
            .Where(item => item.UltimaLeitura is not null)
            .MaxBy(item => item.UltimaLeitura!.MedidaEm);
    }

    public async Task<IReadOnlyList<LeituraSensor>> ListarLeiturasAsync(int sensorId, int quantidade, CancellationToken cancellationToken = default) =>
        await Contexto.Leituras
            .AsNoTracking()
            .Where(leitura => leitura.SensorId == sensorId)
            .OrderByDescending(leitura => leitura.MedidaEm)
            .Take(quantidade)
            .ToListAsync(cancellationToken);

    public Task<LeituraSensor?> ObterLeituraAsync(int sensorId, int leituraId, CancellationToken cancellationToken = default) =>
        Contexto.Leituras
            .AsNoTracking()
            .FirstOrDefaultAsync(leitura => leitura.SensorId == sensorId && leitura.Id == leituraId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, LeituraRecente>> ObterUltimaLeituraPorRuaAsync(
        int bairroId,
        DateTime ate,
        CancellationToken cancellationToken = default)
    {
        // SelectMany "achata" os sensores de todas as ruas do bairro; a subconsulta pega a última leitura.
        var ultimas = await Contexto.Ruas
            .AsNoTracking()
            .Where(rua => rua.BairroId == bairroId)
            .SelectMany(rua => rua.Sensores)
            .Where(sensor => sensor.Ativo)
            .Select(sensor => new
            {
                sensor.RuaId,
                sensor.Codigo,
                Leitura = sensor.Leituras
                    .Where(leitura => leitura.MedidaEm <= ate)
                    .OrderByDescending(leitura => leitura.MedidaEm)
                    .Select(leitura => new { leitura.NivelCm, leitura.LimiteNaHoraCm, leitura.AcimaDoLimite, leitura.MedidaEm })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Se a rua tiver mais de um sensor, vale o mais crítico.
        return ultimas
            .Where(item => item.Leitura is not null)
            .GroupBy(item => item.RuaId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo
                    .OrderByDescending(item => item.Leitura!.AcimaDoLimite)
                    .ThenByDescending(item => item.Leitura!.NivelCm)
                    .Select(item => new LeituraRecente(
                        item.Codigo,
                        item.Leitura!.NivelCm,
                        item.Leitura.LimiteNaHoraCm,
                        item.Leitura.AcimaDoLimite,
                        item.Leitura.MedidaEm))
                    .First());
    }

    public async Task<IReadOnlyList<AlertaDeSensor>> ListarAlertasAsync(
        int bairroId,
        DateTime de,
        DateTime ate,
        CancellationToken cancellationToken = default)
    {
        // LINQ em "query syntax", com dois joins: leitura -> sensor -> rua.
        var consulta =
            from leitura in Contexto.Leituras.AsNoTracking()
            join sensor in Contexto.Sensores on leitura.SensorId equals sensor.Id
            join rua in Contexto.Ruas on sensor.RuaId equals rua.Id
            where rua.BairroId == bairroId
                && leitura.AcimaDoLimite
                && leitura.MedidaEm >= de
                && leitura.MedidaEm <= ate
            orderby leitura.MedidaEm
            select new AlertaDeSensor(rua.Id, rua.Nome, sensor.Codigo, leitura.NivelCm, leitura.LimiteNaHoraCm, leitura.MedidaEm);

        return await consulta.ToListAsync(cancellationToken);
    }

    // Para cada sensor, a leitura mais recente (subconsulta com OrderByDescending + FirstOrDefault).
    private static IQueryable<SensorComLeitura> ComUltimaLeitura(IQueryable<Sensor> sensores) =>
        sensores.Select(sensor => new SensorComLeitura(
            sensor,
            sensor.Leituras.OrderByDescending(leitura => leitura.MedidaEm).FirstOrDefault()));
}

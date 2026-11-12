using Elo.Application.PainelAdmin;
using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Elo.Infrastructure.Repositorios;

/// <summary>
/// Consultas de leitura do painel de administração. Tudo com <c>AsNoTracking</c> e sem N+1:
/// bairros e ruas vêm em um SELECT (Include), os sensores com as leituras em outro (subconsulta com Take),
/// e as chamadas em três (split query: chamadas, respostas e ações).
/// </summary>
public sealed class PainelAdminRepository(EloDbContext contexto) : IPainelAdminRepository
{
    public async Task<IReadOnlyList<Bairro>> ListarBairrosComRuasAsync(CancellationToken cancellationToken = default) =>
        await contexto.Bairros
            .AsNoTracking()
            .Include(bairro => bairro.Ruas)
            .OrderBy(bairro => bairro.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SensorComLeituras>> ListarSensoresComLeiturasAsync(
        DateTime desde,
        int maximoPorSensor,
        CancellationToken cancellationToken = default)
    {
        // As leituras mais recentes da janela, por sensor: a subconsulta com Take vira um único SQL.
        var linhas = await contexto.Sensores
            .AsNoTracking()
            .OrderBy(sensor => sensor.Id)
            .Select(sensor => new
            {
                Sensor = sensor,
                BairroId = contexto.Ruas
                    .Where(rua => rua.Id == sensor.RuaId)
                    .Select(rua => rua.BairroId)
                    .FirstOrDefault(),
                Leituras = sensor.Leituras
                    .Where(leitura => leitura.MedidaEm >= desde)
                    .OrderByDescending(leitura => leitura.MedidaEm)
                    .Take(maximoPorSensor)
                    .Select(leitura => new { leitura.NivelCm, leitura.MedidaEm })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. linhas.Select(linha => new SensorComLeituras(
                linha.Sensor,
                linha.BairroId,
                [.. linha.Leituras.Select(leitura => new LeituraDoSensor(leitura.NivelCm, leitura.MedidaEm))]))
        ];
    }

    public async Task<IReadOnlyList<Chamada>> ListarChamadasComDetalhesAsync(
        DateTime agora,
        DateTime desde,
        CancellationToken cancellationToken = default) =>
        await contexto.Chamadas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(chamada => chamada.Respostas)
            .Include(chamada => chamada.Acoes)
            .Where(chamada =>
                chamada.IniciadaEm >= desde
                || (chamada.EncerradaEm == null
                    && (chamada.EncerramentoPrevistoEm == null || chamada.EncerramentoPrevistoEm > agora)))
            .OrderByDescending(chamada => chamada.IniciadaEm)
            .ThenByDescending(chamada => chamada.Id)
            .ToListAsync(cancellationToken);
}

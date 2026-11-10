using Elo.Application.Abstracoes;
using Elo.Application.Comum;
using Elo.Application.Painel;
using Elo.Domain.Chamadas;
using Elo.Domain.Comum;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elo.Application.ContratoDoApp;

/// <summary>As rotas do contrato do app (docs/contrato-api.md do repositório elo-app).</summary>
public interface IContratoDoAppService
{
    Task<SituacaoDoBairroApp> ObterSituacaoAsync(int? bairroId, CancellationToken cancellationToken = default);

    Task<ResultadoDeRegistro<ChamadaDoAppDto>> RegistrarChamadaAsync(ChamadaDoAppRequest request, CancellationToken cancellationToken = default);

    Task<LeituraDaReguaApp> ObterUltimaLeituraAsync(string? reguaId, CancellationToken cancellationToken = default);

    Task RegistrarLeituraAsync(LeituraDoEsp32Request request, CancellationToken cancellationToken = default);

    Task<PlanoDaRuaApp> ObterPlanoAsync(string ruaId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Camada anticorrupção: atende o contrato do app traduzindo o vocabulário dele para o do domínio.
/// No app, "chamada" é o que a liderança responde; aqui isso vira uma <see cref="Resposta"/>
/// dentro da <see cref="Chamada"/> que o coordenador abriu. As regras continuam todas no domínio.
/// </summary>
public sealed class ContratoDoAppService(
    IBairroRepository bairros,
    IRuaRepository ruas,
    IChamadaRepository chamadas,
    ISensorRepository sensores,
    MontadorDeSituacoes montador,
    IUnitOfWork unitOfWork,
    TimeProvider relogio,
    IOptions<ContratoDoAppOptions> opcoes,
    ILogger<ContratoDoAppService> logger) : IContratoDoAppService
{
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public async Task<SituacaoDoBairroApp> ObterSituacaoAsync(int? bairroId, CancellationToken cancellationToken = default)
    {
        var id = bairroId ?? opcoes.Value.BairroId;
        var bairro = await bairros.ObterPorIdAsync(id, cancellationToken)
            ?? throw RecursoNaoEncontradoException.Bairro(id);

        var chamada = await chamadas.ObterAbertaAsync(id, Agora, cancellationToken);
        if (chamada is null)
        {
            // Sem chamada em andamento: "chamadaIniciadaEm" nulo e todas as ruas em silêncio.
            var ruasAtivas = await ruas.ListarPorBairroAsync(id, null, null, incluirInativas: false, cancellationToken);
            return new SituacaoDoBairroApp(
                bairro.Nome,
                null,
                [.. ruasAtivas.OrderBy(rua => rua.Nome).Select(rua => new RuaDoApp(FormatoDoApp.Id(rua.Id), rua.Nome, null, null))]);
        }

        // Mesma ordem do painel do coordenador (o LINQ do PriorizadorDeRuas); o app pode reordenar.
        var situacoes = await montador.MontarAsync(chamada, cancellationToken);
        return new SituacaoDoBairroApp(
            bairro.Nome,
            FormatoDoApp.Data(chamada.IniciadaEm),
            [.. situacoes.RuasEmOrdem.Select(situacao => new RuaDoApp(
                FormatoDoApp.Id(situacao.Rua.Id),
                situacao.Rua.Nome,
                situacao.UltimaResposta is { } resposta ? FormatoDoApp.Status(resposta.Situacao) : null,
                situacao.UltimaResposta is { } respondida ? FormatoDoApp.Data(respondida.RespondidaEm) : null))]);
    }

    public async Task<ResultadoDeRegistro<ChamadaDoAppDto>> RegistrarChamadaAsync(
        ChamadaDoAppRequest request,
        CancellationToken cancellationToken = default)
    {
        var idLocal = request.Id.Trim();

        // O app reenvia o que ficou guardado offline: o mesmo id vira um registro só.
        var existente = await chamadas.ObterRespostaPorIdLocalAsync(idLocal, cancellationToken);
        if (existente is not null)
        {
            return new ResultadoDeRegistro<ChamadaDoAppDto>(ParaApp(existente), Criado: false);
        }

        var situacao = FormatoDoApp.Situacao(request.Status)
            ?? throw new RegraDeNegocioException("Status inválido: use tudo_bem, agua_subindo ou preciso_ajuda.");
        var rua = await ObterRuaAsync(request.RuaId, cancellationToken);
        var agora = Agora;
        var criadaEm = request.CriadaEm?.UtcDateTime ?? agora;

        // O app não diz a chamada: vale a do bairro da rua cuja janela inclui a hora da resposta.
        var chamada = await chamadas.ObterDaJanelaAsync(rua.BairroId, criadaEm, cancellationToken)
            ?? throw new RegraDeNegocioException(
                "Não há chamada no bairro no horário desta resposta. O coordenador precisa abrir a chamada.");

        var resposta = chamada.RegistrarResposta(
            idLocal, rua, situacao, request.Pessoas, request.Observacao, CanalResposta.App, criadaEm, agora);
        await unitOfWork.SalvarAsync(cancellationToken);

        // A observação não vai para o log: ela pode ter dado pessoal digitado sem querer.
        logger.LogInformation(
            "App: resposta {Status} da rua {RuaId} na chamada {ChamadaId}.", request.Status, rua.Id, chamada.Id);

        return new ResultadoDeRegistro<ChamadaDoAppDto>(ParaApp(resposta), Criado: true);
    }

    public async Task<LeituraDaReguaApp> ObterUltimaLeituraAsync(string? reguaId, CancellationToken cancellationToken = default)
    {
        var codigo = string.IsNullOrWhiteSpace(reguaId) ? opcoes.Value.ReguaPadrao : reguaId;
        SensorComLeitura? item = null;

        if (!string.IsNullOrWhiteSpace(codigo))
        {
            item = await sensores.ObterComUltimaLeituraPorCodigoAsync(Sensor.NormalizarCodigo(codigo), cancellationToken);
        }

        // Sem régua pedida e sem a régua padrão: vale a leitura mais recente do bairro.
        if (item is null && string.IsNullOrWhiteSpace(reguaId))
        {
            item = await sensores.ObterLeituraMaisRecenteDoBairroAsync(opcoes.Value.BairroId, cancellationToken);
        }

        if (item is null)
        {
            throw new RecursoNaoEncontradoException($"Régua {codigo} não encontrada.");
        }

        var leitura = item.UltimaLeitura
            ?? throw new RecursoNaoEncontradoException($"A régua {item.Sensor.Codigo} ainda não mandou leitura.");

        return new LeituraDaReguaApp(
            item.Sensor.Codigo,
            item.Sensor.Local,
            FormatoDoApp.Metros(leitura.NivelCm),
            FormatoDoApp.Metros(leitura.LimiteNaHoraCm),
            FormatoDoApp.Data(leitura.MedidaEm));
    }

    public async Task RegistrarLeituraAsync(LeituraDoEsp32Request request, CancellationToken cancellationToken = default)
    {
        var codigo = Sensor.NormalizarCodigo(request.ReguaId);
        var sensor = await sensores.ObterPorCodigoAsync(codigo, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Régua {codigo} não encontrada.");

        var nivelMetros = request.NivelMetros
            ?? throw new RegraDeNegocioException("Informe o nível da água em metros.");
        var agora = Agora;

        // O domínio guarda em centímetros; o ESP32 manda em metros.
        var leitura = sensor.RegistrarLeitura(Math.Round(nivelMetros * 100, 1), request.MedidaEm?.UtcDateTime ?? agora, agora);
        await unitOfWork.SalvarAsync(cancellationToken);

        if (leitura.AcimaDoLimite)
        {
            logger.LogWarning("Régua {Codigo} acima do limite: {Nivel} m.", sensor.Codigo, nivelMetros);
        }
    }

    public async Task<PlanoDaRuaApp> ObterPlanoAsync(string ruaId, CancellationToken cancellationToken = default)
    {
        var rua = await ObterRuaAsync(ruaId, cancellationToken);
        var plano = rua.Plano
            ?? throw new RecursoNaoEncontradoException($"A rua {ruaId} não tem plano cadastrado.");

        return new PlanoDaRuaApp(
            FormatoDoApp.Id(rua.Id),
            rua.Nome,
            plano.PontoDeEncontro,
            plano.Rota,
            plano.Abrigo,
            [.. plano.ItensParaLevar],
            FormatoDoApp.ContatosDeEmergencia);
    }

    private async Task<Rua> ObterRuaAsync(string? ruaId, CancellationToken cancellationToken)
    {
        var id = FormatoDoApp.LerId(ruaId);
        var rua = id is int valor ? await ruas.ObterPorIdAsync(valor, cancellationToken) : null;
        return rua ?? throw new RecursoNaoEncontradoException($"Rua {ruaId} não encontrada.");
    }

    private static ChamadaDoAppDto ParaApp(Resposta resposta) => new(
        resposta.IdLocal,
        FormatoDoApp.Id(resposta.RuaId),
        FormatoDoApp.Status(resposta.Situacao),
        resposta.Pessoas,
        resposta.Observacao,
        FormatoDoApp.Data(resposta.RespondidaEm),
        FormatoDoApp.Data(resposta.RecebidaEm));
}

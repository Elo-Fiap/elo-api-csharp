using Elo.Application.Abstracoes;
using Elo.Application.Bairros;
using Elo.Application.Chamadas;
using Elo.Application.Comum;
using Elo.Application.Retencao;
using Elo.Application.Sensores;
using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Sensores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using static Elo.Tests.Fabrica;

namespace Elo.Tests.Unitarios.Aplicacao;

public class RespostaServiceTests
{
    private readonly Mock<IChamadaRepository> _chamadas = new();
    private readonly Mock<IRuaRepository> _ruas = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(Inicio.AddMinutes(20)));

    private RespostaService CriarServico() =>
        new(_chamadas.Object, _ruas.Object, _unitOfWork.Object, _relogio, NullLogger<RespostaService>.Instance);

    [Fact]
    public async Task Reenvio_com_o_mesmo_idLocal_devolve_a_resposta_salva_sem_gravar_de_novo()
    {
        var chamada = NovaChamadaReal(id: 2);
        var salva = Responder(chamada, NovaRua(1), SituacaoRua.TudoBem, 5);
        _chamadas.Setup(r => r.ObterRespostaPorIdLocalAsync(salva.IdLocal, It.IsAny<CancellationToken>())).ReturnsAsync(salva);

        var resultado = await CriarServico().RegistrarAsync(2, new RegistrarRespostaRequest
        {
            IdLocal = salva.IdLocal,
            RuaId = 1,
            Situacao = SituacaoRua.TudoBem
        });

        Assert.False(resultado.Criado);
        Assert.Equal(salva.IdLocal, resultado.Valor.IdLocal);
        _unitOfWork.Verify(u => u.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
        _chamadas.Verify(r => r.ObterPorIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IdLocal_usado_em_outra_chamada_gera_conflito()
    {
        var salva = Responder(NovaChamadaReal(id: 2), NovaRua(1), SituacaoRua.TudoBem, 5);
        _chamadas.Setup(r => r.ObterRespostaPorIdLocalAsync(salva.IdLocal, It.IsAny<CancellationToken>())).ReturnsAsync(salva);

        await Assert.ThrowsAsync<ConflitoException>(() => CriarServico().RegistrarAsync(9, new RegistrarRespostaRequest
        {
            IdLocal = salva.IdLocal,
            RuaId = 1,
            Situacao = SituacaoRua.TudoBem
        }));
    }

    [Fact]
    public async Task Resposta_nova_e_gravada_com_a_hora_de_chegada_do_relogio()
    {
        var chamada = NovaChamadaReal(id: 2);
        _chamadas.Setup(r => r.ObterPorIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(chamada);
        _ruas.Setup(r => r.ObterPorIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(NovaRua(1));

        var resultado = await CriarServico().RegistrarAsync(2, new RegistrarRespostaRequest
        {
            RuaId = 1,
            Situacao = SituacaoRua.PrecisoDeAjuda,
            Pessoas = 3,
            RespondidaEm = new DateTimeOffset(Inicio.AddMinutes(12))
        });

        Assert.True(resultado.Criado);
        Assert.Equal(Inicio.AddMinutes(12), resultado.Valor.RespondidaEm);
        Assert.Equal(Inicio.AddMinutes(20), resultado.Valor.RecebidaEm);
        Assert.Single(chamada.Respostas);
        _unitOfWork.Verify(u => u.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Resposta_para_chamada_inexistente_devolve_nao_encontrado()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarServico().RegistrarAsync(99, new RegistrarRespostaRequest
        {
            RuaId = 1,
            Situacao = SituacaoRua.TudoBem
        }));
    }
}

public class SensorServiceTests
{
    private readonly Mock<ISensorRepository> _sensores = new();
    private readonly Mock<IRuaRepository> _ruas = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private SensorService CriarServico() => new(
        _sensores.Object, _ruas.Object, _unitOfWork.Object, new FakeTimeProvider(new DateTimeOffset(Inicio)),
        NullLogger<SensorService>.Instance);

    [Fact]
    public async Task Leitura_do_ESP32_acima_do_limite_e_gravada_como_alerta()
    {
        var sensor = new Sensor(1, "regua-exemplo", "Ponte", 40).ComId(1);
        _sensores.Setup(r => r.ObterPorIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(sensor);

        var leitura = await CriarServico().RegistrarLeituraAsync(1, new RegistrarLeituraRequest { NivelCm = 45.5 });

        Assert.True(leitura.AcimaDoLimite);
        Assert.Equal(Inicio, leitura.MedidaEm);
        _unitOfWork.Verify(u => u.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Codigo_de_sensor_repetido_gera_conflito()
    {
        _ruas.Setup(r => r.ExisteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sensores.Setup(r => r.ExisteComCodigoAsync("esp32-01", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflitoException>(() => CriarServico().CriarAsync(new CriarSensorRequest
        {
            RuaId = 1,
            Codigo = "esp32-01",
            Local = "Ponte",
            LimiteAlertaCm = 40
        }));

        _sensores.Verify(r => r.Adicionar(It.IsAny<Sensor>()), Times.Never);
    }
}

public class BairroServiceTests
{
    private readonly Mock<IBairroRepository> _bairros = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private BairroService CriarServico() => new(
        _bairros.Object, _unitOfWork.Object, new FakeTimeProvider(new DateTimeOffset(Inicio)), NullLogger<BairroService>.Instance);

    [Fact]
    public async Task Bairro_com_chamadas_nao_pode_ser_excluido()
    {
        var bairro = new Bairro("Jardim Teste", "São Paulo", "Sé", dadosSimulados: true).ComId(1);
        _bairros.Setup(r => r.ObterPorIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(bairro);
        _bairros.Setup(r => r.PossuiChamadasAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflitoException>(() => CriarServico().RemoverAsync(1));

        _bairros.Verify(r => r.Remover(It.IsAny<Bairro>()), Times.Never);
    }

    [Fact]
    public async Task Bairro_com_nome_repetido_na_mesma_cidade_gera_conflito()
    {
        _bairros.Setup(r => r.ExisteComNomeAsync("Jardim Teste", "São Paulo", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflitoException>(() => CriarServico().CriarAsync(new BairroRequest
        {
            Nome = " Jardim Teste ",
            Cidade = "São Paulo",
            Subprefeitura = "Sé"
        }));
    }
}

public class RetencaoServiceTests
{
    [Fact]
    public async Task Encerra_treinos_vencidos_e_apaga_respostas_com_mais_de_30_dias()
    {
        var agora = Inicio;
        var chamadas = new Mock<IChamadaRepository>();
        var ruas = new Mock<IRuaRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var treinoVencido = NovoTreino(id: 4, inicio: agora.AddDays(-1));

        chamadas.Setup(r => r.ListarTreinosVencidosAsync(agora, It.IsAny<CancellationToken>())).ReturnsAsync([treinoVencido]);
        chamadas.Setup(r => r.ApagarRespostasRecebidasAntesDeAsync(agora.AddDays(-30), It.IsAny<CancellationToken>())).ReturnsAsync(12);
        ruas.Setup(r => r.ListarIdsAtivosAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([1, 2]);

        var servico = new RetencaoService(
            chamadas.Object,
            ruas.Object,
            unitOfWork.Object,
            new FakeTimeProvider(new DateTimeOffset(agora)),
            Options.Create(new RetencaoOptions { DiasParaApagarRespostas = 30 }),
            NullLogger<RetencaoService>.Instance);

        var resultado = await servico.ExecutarAsync();

        Assert.Equal(new ResultadoDaRetencao(TreinosEncerrados: 1, RespostasApagadas: 12), resultado);
        Assert.NotNull(treinoVencido.EncerradaEm);
        Assert.NotNull(treinoVencido.Resultado);
        chamadas.Verify(r => r.ApagarRespostasRecebidasAntesDeAsync(agora.AddDays(-30), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

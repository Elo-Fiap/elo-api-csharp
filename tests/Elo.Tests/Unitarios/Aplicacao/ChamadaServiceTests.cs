using Elo.Application.Abstracoes;
using Elo.Application.Chamadas;
using Elo.Application.Comum;
using Elo.Domain.Chamadas;
using Elo.Domain.Comum;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using static Elo.Tests.Fabrica;

namespace Elo.Tests.Unitarios.Aplicacao;

/// <summary>
/// Testes do serviço com repositórios falsos (Moq): nada de banco, só a regra.
/// É a injeção de dependência que permite trocar a implementação real pela falsa.
/// </summary>
public class ChamadaServiceTests
{
    private readonly Mock<IChamadaRepository> _chamadas = new();
    private readonly Mock<IBairroRepository> _bairros = new();
    private readonly Mock<IRuaRepository> _ruas = new();
    private readonly Mock<ISensorRepository> _sensores = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(Inicio));

    private ChamadaService CriarServico() => new(
        _chamadas.Object,
        _bairros.Object,
        _ruas.Object,
        _sensores.Object,
        _unitOfWork.Object,
        _relogio,
        NullLogger<ChamadaService>.Instance);

    public ChamadaServiceTests()
    {
        _bairros.Setup(r => r.ExisteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ruas.Setup(r => r.ListarIdsAtivosAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
    }

    [Fact]
    public async Task Abrir_com_outra_chamada_aberta_no_bairro_gera_conflito()
    {
        _chamadas.Setup(r => r.ListarNaoEncerradasAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([NovaChamadaReal(id: 7, inicio: Inicio.AddMinutes(-10))]);

        var erro = await Assert.ThrowsAsync<ConflitoException>(() =>
            CriarServico().AbrirAsync(1, new AbrirChamadaRequest { Tipo = TipoChamada.Treino }));

        Assert.Contains("chamada 7", erro.Message);
        _chamadas.Verify(r => r.Adicionar(It.IsAny<Chamada>()), Times.Never);
    }

    [Fact]
    public async Task Abrir_encerra_antes_o_treino_que_passou_do_prazo()
    {
        var treinoVencido = NovoTreino(id: 3, inicio: Inicio.AddHours(-2), duracaoMinutos: 30);
        _chamadas.Setup(r => r.ListarNaoEncerradasAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([treinoVencido]);

        var request = new AbrirChamadaRequest
        {
            Tipo = TipoChamada.Real,
            Aviso = new AvisoOficialDto { Fonte = "INMET", Identificador = "A1", Evento = "Chuvas Intensas", Severidade = SeveridadeAviso.Perigo }
        };

        var chamada = await CriarServico().AbrirAsync(1, request);

        Assert.Equal(TipoChamada.Real, chamada.Tipo);
        Assert.Equal(StatusChamada.Aberta, chamada.Status);
        Assert.Equal(Inicio.AddHours(-2).AddMinutes(30), treinoVencido.EncerradaEm);
        Assert.NotNull(treinoVencido.Resultado);
        _chamadas.Verify(r => r.Adicionar(It.Is<Chamada>(c => c is ChamadaReal)), Times.Once);
        _unitOfWork.Verify(u => u.SalvarAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Abrir_chamada_real_sem_aviso_oficial_e_recusado()
    {
        _chamadas.Setup(r => r.ListarNaoEncerradasAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            CriarServico().AbrirAsync(1, new AbrirChamadaRequest { Tipo = TipoChamada.Real }));

        _unitOfWork.Verify(u => u.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Abrir_treino_com_aviso_oficial_e_recusado()
    {
        _chamadas.Setup(r => r.ListarNaoEncerradasAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var request = new AbrirChamadaRequest
        {
            Tipo = TipoChamada.Treino,
            Aviso = new AvisoOficialDto { Fonte = "INMET", Identificador = "A1", Evento = "Chuva", Severidade = SeveridadeAviso.Perigo }
        };

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => CriarServico().AbrirAsync(1, request));
    }

    [Fact]
    public async Task Abrir_em_bairro_inexistente_devolve_nao_encontrado()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            CriarServico().AbrirAsync(99, new AbrirChamadaRequest { Tipo = TipoChamada.Treino }));
    }

    [Fact]
    public async Task Encerrar_calcula_o_resultado_com_as_ruas_ativas_do_bairro()
    {
        var chamada = NovaChamadaReal(id: 5, inicio: Inicio.AddMinutes(-30));
        Responder(chamada, NovaRua(1), SituacaoRua.TudoBem, 5);
        _chamadas.Setup(r => r.ObterComRespostasAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(chamada);

        var dto = await CriarServico().EncerrarAsync(5);

        Assert.Equal(StatusChamada.Encerrada, dto.Status);
        Assert.NotNull(dto.Resultado);
        Assert.Equal(3, dto.Resultado.TotalDeRuas);
        Assert.Equal(33.3, dto.Resultado.PercentualConhecidoEm15Min);
    }
}

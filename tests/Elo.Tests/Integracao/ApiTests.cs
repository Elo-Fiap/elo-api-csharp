using System.Net;
using System.Net.Http.Json;
using Elo.Application.Bairros;
using Elo.Application.Chamadas;
using Elo.Application.Comum;
using Elo.Application.Painel;
using Elo.Application.Ruas;
using Elo.Application.Sensores;
using Elo.Domain.Chamadas;
using Elo.Domain.Painel;
using Microsoft.AspNetCore.Mvc;

namespace Elo.Tests.Integracao;

/// <summary>Só leitura sobre os dados simulados: bairro, painel, indicadores e registro.</summary>
public class PainelApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Lista_o_bairro_simulado_com_a_chamada_real_aberta()
    {
        var bairros = await (await _cliente.GetAsync("/api/bairros")).LerAsync<List<BairroResumoDto>>();

        var bairro = Assert.Single(bairros);
        Assert.True(bairro.DadosSimulados);
        Assert.Equal(8, bairro.TotalDeRuasAtivas);
        Assert.Equal(2, bairro.ChamadaAbertaId);
    }

    [Fact]
    public async Task Painel_do_bairro_vem_na_ordem_de_prioridade_com_o_indicador()
    {
        var resposta = await _cliente.GetAsync("/api/bairros/1/painel");
        var painel = await resposta.LerAsync<PainelDto>();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(
        [
            "Rua do Aguapé", "Rua das Garças", "Rua das Palafitas", "Rua dos Barqueiros",
            "Rua do Córrego Seco", "Rua da Várzea", "Rua Ipê Amarelo", "Travessa da Taboa"
        ], painel.Ruas.Select(rua => rua.Rua));
        Assert.Equal(StatusNoPainel.PrecisaDeAjuda, painel.Ruas[0].Status);
        Assert.True(painel.Ruas[1].AtendimentoACaminho);
        Assert.True(painel.Ruas[2].Sensor!.AcimaDoLimite);
        Assert.Equal(62.5, painel.Indicador.PercentualConhecidoEm15Min);
        Assert.Equal(8, painel.Resumo.PessoasPrecisandoDeAjuda);
        Assert.StartsWith("https://wa.me/55009", painel.Ruas[2].LinkWhatsApp);
    }

    [Fact]
    public async Task Indicadores_comparam_treino_e_chamada_real()
    {
        var indicadores = await (await _cliente.GetAsync("/api/bairros/1/indicadores")).LerAsync<IndicadoresDoBairroDto>();

        Assert.Equal(2, indicadores.Chamadas.Count);
        Assert.Equal(75, indicadores.MediaPorTipo.Single(m => m.Tipo == TipoChamada.Treino).PercentualMedioConhecidoEm15Min);
        Assert.Equal(62.5, indicadores.MediaPorTipo.Single(m => m.Tipo == TipoChamada.Real).PercentualMedioConhecidoEm15Min);
    }

    [Fact]
    public async Task Registro_da_chamada_junta_respostas_acoes_e_sensor_em_ordem()
    {
        var eventos = await (await _cliente.GetAsync("/api/chamadas/2/registro")).LerAsync<List<EventoDoRegistroDto>>();

        Assert.Equal(TipoDeEvento.ChamadaAberta, eventos[0].Tipo);
        Assert.Contains(eventos, e => e.Tipo == TipoDeEvento.SensorAcimaDoLimite && e.Rua == "Rua das Palafitas");
        Assert.Contains(eventos, e => e.Tipo == TipoDeEvento.AcaoDoCoordenador && e.Descricao.Contains("Vou até lá"));
        Assert.Equal(eventos.OrderBy(e => e.Momento).Select(e => e.Momento), eventos.Select(e => e.Momento));
    }

    [Fact]
    public async Task Listagem_filtra_por_tipo_usando_a_heranca()
    {
        var pagina = await (await _cliente.GetAsync("/api/chamadas?tipo=Treino&status=Encerrada")).LerAsync<PaginaResultado<ChamadaDto>>();

        var treino = Assert.Single(pagina.Itens);
        Assert.Equal(TipoChamada.Treino, treino.Tipo);
        Assert.Equal(30, treino.DuracaoMinutos);
        Assert.NotNull(treino.Resultado);
    }

    [Fact]
    public async Task Recurso_inexistente_devolve_404_em_problem_details()
    {
        var resposta = await _cliente.GetAsync("/api/chamadas/999");
        var problema = await resposta.LerAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("Chamada 999 não encontrada.", problema.Detail);
    }
}

/// <summary>A resposta da rua: idempotência offline, validação e mascaramento.</summary>
public class RespostasApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Reenvio_offline_com_o_mesmo_idLocal_nao_duplica()
    {
        var corpo = new RegistrarRespostaRequest { IdLocal = Guid.NewGuid().ToString(), RuaId = 6, Situacao = SituacaoRua.TudoBem };

        var primeira = await _cliente.EnviarAsync("/api/chamadas/2/respostas", corpo);
        var reenvio = await _cliente.EnviarAsync("/api/chamadas/2/respostas", corpo);

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reenvio.StatusCode);
        Assert.Equal((await primeira.LerAsync<RespostaDto>()).Id, (await reenvio.LerAsync<RespostaDto>()).Id);
        Assert.NotNull(primeira.Headers.Location);
    }

    [Fact]
    public async Task Resposta_fora_do_horario_da_chamada_devolve_422()
    {
        // A chamada 2 começou há uns 25 minutos: uma resposta "dada" 3 horas atrás não é dela.
        var resposta = await _cliente.EnviarAsync("/api/chamadas/2/respostas", new RegistrarRespostaRequest
        {
            RuaId = 4,
            Situacao = SituacaoRua.TudoBem,
            RespondidaEm = DateTimeOffset.UtcNow.AddHours(-3)
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task Corpo_sem_rua_devolve_400_com_a_validacao()
    {
        var resposta = await _cliente.EnviarAsync("/api/chamadas/2/respostas", new { situacao = "TudoBem" });
        var problema = await resposta.LerAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("RuaId", problema.Errors.Keys);
    }

    [Fact]
    public async Task Observacao_chega_ao_banco_sem_cpf()
    {
        var resposta = await _cliente.EnviarAsync("/api/chamadas/2/respostas", new RegistrarRespostaRequest
        {
            RuaId = 3,
            Situacao = SituacaoRua.PrecisoDeAjuda,
            Pessoas = 2,
            Observacao = "Moradora do CPF 123.456.789-09 está ilhada"
        });

        var salva = await resposta.LerAsync<RespostaDto>();
        Assert.Equal("Moradora do CPF [CPF removido] está ilhada", salva.Observacao);
    }
}

/// <summary>Ciclo completo de uma chamada (em um só teste, porque cada passo depende do anterior).</summary>
public class CicloDaChamadaApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Abrir_encerrar_e_abrir_treino()
    {
        // Já existe a chamada real 2 aberta: abrir outra dá conflito.
        var conflito = await _cliente.EnviarAsync("/api/bairros/1/chamadas", new AbrirChamadaRequest { Tipo = TipoChamada.Treino });
        Assert.Equal(HttpStatusCode.Conflict, conflito.StatusCode);

        var encerrada = await (await _cliente.PostAsync("/api/chamadas/2/encerramento", null)).LerAsync<ChamadaDto>();
        Assert.Equal(StatusChamada.Encerrada, encerrada.Status);
        Assert.Equal(62.5, encerrada.Resultado!.PercentualConhecidoEm15Min);

        var criado = await _cliente.EnviarAsync("/api/bairros/1/chamadas", new AbrirChamadaRequest { Tipo = TipoChamada.Treino, DuracaoMinutos = 20 });
        var treino = await criado.LerAsync<ChamadaDto>();
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        Assert.StartsWith("TREINO", treino.Pergunta);
        Assert.Equal(StatusChamada.Aberta, treino.Status);

        // No painel do treino, o link do WhatsApp deixa claro que é simulado.
        var painel = await (await _cliente.GetAsync($"/api/chamadas/{treino.Id}/painel")).LerAsync<PainelDto>();
        Assert.All(painel.Ruas, rua => Assert.Equal(StatusNoPainel.EmSilencio, rua.Status));
        Assert.Contains("TREINO", Uri.UnescapeDataString(painel.Ruas[0].LinkWhatsApp!));
    }
}

/// <summary>Sensor (ESP32), plano da rua e regras de exclusão.</summary>
public class SensoresERuasApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Leitura_do_ESP32_acima_do_limite_e_marcada()
    {
        var resposta = await _cliente.EnviarAsync("/api/sensores/2/leituras", new RegistrarLeituraRequest { NivelCm = 160 });
        var leitura = await resposta.LerAsync<LeituraDto>();

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.True(leitura.AcimaDoLimite);
        Assert.Equal(150, leitura.LimiteCm);
    }

    [Fact]
    public async Task Leitura_negativa_devolve_400()
    {
        var resposta = await _cliente.EnviarAsync("/api/sensores/2/leituras", new { nivelCm = -3 });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Plano_da_rua_pode_ser_substituido()
    {
        var primeiro = new PlanoDaRuaRequest { Rota = "Rota 1", PontoDeEncontro = "Praça", Abrigo = "Escola", ItensParaLevar = ["Documentos"] };
        var segundo = new PlanoDaRuaRequest { Rota = "Rota 2", PontoDeEncontro = "Igreja", Abrigo = "CEU", ItensParaLevar = ["Lanterna", "Remédios"] };

        Assert.Equal(HttpStatusCode.OK, (await _cliente.PutAsJsonAsync("/api/ruas/4/plano", primeiro, EloApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.PutAsJsonAsync("/api/ruas/4/plano", segundo, EloApiFactory.Json)).StatusCode);

        var plano = await (await _cliente.GetAsync("/api/ruas/4/plano")).LerAsync<PlanoDaRuaDto>();
        Assert.Equal("Rota 2", plano.Rota);
        Assert.Equal(["Lanterna", "Remédios"], plano.ItensParaLevar);
    }

    [Fact]
    public async Task Rua_com_historico_nao_pode_ser_excluida()
    {
        var resposta = await _cliente.DeleteAsync("/api/ruas/1");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Rua_nova_e_criada_e_aparece_no_bairro()
    {
        var criada = await _cliente.EnviarAsync("/api/bairros/1/ruas", new RuaRequest
        {
            Nome = "Rua Nova de Teste",
            Microarea = "Microárea 3",
            NivelRisco = Elo.Domain.Ruas.NivelRisco.Medio,
            Lideranca = new LiderancaDto { Apelido = "Teste", WhatsApp = "5500900000099" }
        });

        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var ruas = await (await _cliente.GetAsync("/api/bairros/1/ruas?microarea=Microárea 3")).LerAsync<List<RuaResumoDto>>();
        Assert.Contains(ruas, rua => rua.Nome == "Rua Nova de Teste");
    }
}

/// <summary>Documentação, saúde e a página do resumo do bairro.</summary>
public class InfraestruturaDaApiTests(EloApiFactory fabrica) : IClassFixture<EloApiFactory>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Theory]
    [InlineData("/openapi/v1.json", "Elo API")]
    [InlineData("/health", "Healthy")]
    [InlineData("/", "Resumo do bairro")]
    [InlineData("/swagger/index.html", "swagger")]
    public async Task Endpoints_de_apoio_respondem(string caminho, string trecho)
    {
        var resposta = await _cliente.GetAsync(caminho);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains(trecho, await resposta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}

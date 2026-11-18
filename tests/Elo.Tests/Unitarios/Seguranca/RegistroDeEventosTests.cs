using Elo.Seguranca.Eventos;
using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Microsoft.Extensions.Time.Testing;

namespace Elo.Tests.Unitarios.Seguranca;

public class RegistroDeEventosTests
{
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));

    private EventoDeSeguranca Novo(RegistroDeEventosEmMemoria registro, params TipoDePii[] tipos) =>
        registro.Registrar(EtapaDoEvento.Saida, Sink.Log, AcaoDeSeguranca.Bloqueado, tipos, tipos.Length, "Categoria");

    [Fact]
    public void Registra_com_id_crescente_hora_e_severidade_a_maior_dos_tipos()
    {
        var registro = new RegistroDeEventosEmMemoria(_relogio);

        var a = Novo(registro, TipoDePii.Telefone);
        var b = Novo(registro, TipoDePii.Telefone, TipoDePii.Cpf);

        Assert.Equal([1L, 2L], [a.Id, b.Id]);
        Assert.Equal(Severidade.Media, a.Severidade);
        Assert.Equal(Severidade.Alta, b.Severidade);
        Assert.Equal(_relogio.GetUtcNow(), a.OcorridoEm);
    }

    [Fact]
    public void Listar_devolve_do_mais_recente_para_o_mais_antigo_com_limite_e_filtro_de_data()
    {
        var registro = new RegistroDeEventosEmMemoria(_relogio);
        Novo(registro, TipoDePii.Cpf);
        _relogio.Advance(TimeSpan.FromHours(2));
        var segundo = Novo(registro, TipoDePii.Rg);
        var terceiro = Novo(registro, TipoDePii.Senha);

        Assert.Equal([terceiro.Id, segundo.Id], registro.Listar(null, 2).Select(e => e.Id));
        Assert.Equal(2, registro.Listar(_relogio.GetUtcNow().AddHours(-1), 50).Count);
    }

    [Fact]
    public void Buffer_circular_guarda_so_os_500_ultimos()
    {
        var registro = new RegistroDeEventosEmMemoria(_relogio);
        for (var i = 0; i < RegistroDeEventosEmMemoria.Capacidade + 20; i++)
        {
            Novo(registro, TipoDePii.Cpf);
        }

        var todos = registro.Listar(null, 1000);
        Assert.Equal(RegistroDeEventosEmMemoria.Capacidade, todos.Count);
        Assert.Equal(520, todos[0].Id);
        Assert.Equal(21, todos[^1].Id);
    }

    [Fact]
    public void Definir_mensagem_preenche_o_evento_e_avisa_falso_se_ele_nao_existe_mais()
    {
        var registro = new RegistroDeEventosEmMemoria(_relogio);
        var evento = Novo(registro, TipoDePii.Cpf);

        Assert.True(registro.DefinirMensagem(evento.Id, "Escondemos o seu dado.", "modelo padrão"));
        Assert.Equal("Escondemos o seu dado.", evento.MensagemAoCidadao);
        Assert.Equal("modelo padrão", evento.TraduzidaPor);
        Assert.False(registro.DefinirMensagem(999, "x", "y"));
    }

    [Fact]
    public async Task Evento_novo_avisa_pelo_canal_de_ids()
    {
        var registro = new RegistroDeEventosEmMemoria(_relogio);
        var evento = Novo(registro, TipoDePii.Cpf);

        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(evento.Id, await registro.NovosEventos.ReadAsync(limite.Token));
    }

    [Theory]
    [InlineData("POST", "/chamadas", "POST /chamadas")]
    [InlineData("POST", "/api/chamadas/12/respostas", "POST /api/chamadas/{id}/respostas")]
    [InlineData("GET", "/api/ruas/3f2504e0-4f89-41d3-9a0c-0305e82c3301", "GET /api/ruas/{id}")]
    [InlineData("GET", "/", "GET /")]
    [InlineData("GET", "/api/pessoas/123.456.789-00", "GET /api/pessoas/{id}")] // dado pessoal no caminho não chega ao evento
    [InlineData("POST", "/contato/maria@exemplo.com/x", "POST /contato/{id}/x")]
    public void Rota_normalizada_troca_ids_por_chave(string metodo, string caminho, string esperado) =>
        Assert.Equal(esperado, NormalizadorDeRota.Normalizar(metodo, caminho));
}

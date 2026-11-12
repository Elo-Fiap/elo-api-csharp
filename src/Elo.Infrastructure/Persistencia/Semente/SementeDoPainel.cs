using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Comum;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;

namespace Elo.Infrastructure.Persistencia.Semente;

/// <summary>
/// Bairros extras do painel de administração (os bairros 2 a 12 do arquivo <c>semente-painel.json</c>), com ruas,
/// réguas e leituras, chamadas, respostas e ações. Tudo simulado, nada de dado pessoal.
/// <para>
/// O arquivo vem do painel: os ids dele (ex.: rua "2-3", chamada "ch-007") só servem aqui, para ligar uma coisa
/// à outra. Na API, os ids são os inteiros gerados pelo banco. Os tempos do arquivo são "minutos atrás"
/// e viram datas a partir do momento da semente. O bairro 1 do arquivo é ignorado: o piloto é o da API.
/// </para>
/// Tudo é criado pelos construtores e métodos do domínio, então passa pelas mesmas regras da API.
/// </summary>
internal static class SementeDoPainel
{
    public const string Arquivo = "semente-painel.json";
    public const string SubprefeituraPadrao = "Sem subprefeitura (simulado)";
    public const string AvisoPadrao = "Chuvas intensas (simulado)";

    /// <summary>Semeia os extras e devolve os ajustes que foi preciso fazer para respeitar as regras do domínio (se algum).</summary>
    public static async Task<IReadOnlyList<string>> IncluirAsync(EloDbContext contexto, DateTime agora, CancellationToken cancellationToken)
    {
        var ajustes = new List<string>();
        var arquivo = ArquivosDaSemente.Ler<ArquivoDoPainel>(Arquivo);
        var bairrosDoArquivo = arquivo.Bairros.Where(bairro => bairro.Id != 1).OrderBy(bairro => bairro.Id).ToList();

        // 1. Bairros (salvos antes, para as ruas receberem o Id deles).
        var bairros = new Dictionary<int, Bairro>();
        foreach (var origem in bairrosDoArquivo)
        {
            var bairro = new Bairro(
                origem.Nome, origem.Cidade, SubprefeituraPadrao, dadosSimulados: true,
                origem.Uf, origem.CodigoIbge, origem.Latitude, origem.Longitude,
                origem.PercentualSemApp, agora.AddMinutes(-origem.SemAppMedidoMinutosAtras));
            bairros[origem.Id] = bairro;
        }

        contexto.Bairros.AddRange(bairros.Values);
        await contexto.SaveChangesAsync(cancellationToken);

        // 2. Ruas, sem liderança (o arquivo não traz). O id do arquivo ("2-3") aponta para a rua criada.
        var ruas = new Dictionary<string, Rua>();
        foreach (var origem in bairrosDoArquivo)
        {
            var bairro = bairros[origem.Id];
            foreach (var rua in origem.Ruas)
            {
                var nova = new Rua(bairro.Id, rua.Nome, rua.Microarea, ParaRisco(rua.NivelRisco));
                nova.DefinirGeografia(rua.Latitude, rua.Longitude, rua.Tracado?.Select(par => new Coordenada(par[0], par[1])));
                ruas[rua.Id] = nova;
            }
        }

        contexto.Ruas.AddRange(ruas.Values);
        await contexto.SaveChangesAsync(cancellationToken);

        // 3. Réguas com as leituras (a régua do bairro 1 do arquivo não entra: o piloto já tem a dele).
        foreach (var origem in arquivo.Reguas.Where(regua => regua.BairroId != 1))
        {
            var sensor = new Sensor(ruas[origem.RuaId].Id, origem.Id, origem.Local, EmCentimetros(origem.LimiteMetros));
            sensor.DefinirLocalizacao(origem.Latitude, origem.Longitude);
            contexto.Sensores.Add(sensor);
            await contexto.SaveChangesAsync(cancellationToken);

            foreach (var leitura in origem.Leituras)
            {
                var medidaEm = agora.AddMinutes(-leitura.MinutosAtras);
                sensor.RegistrarLeitura(EmCentimetros(leitura.NivelMetros), medidaEm, medidaEm);
            }
        }

        await contexto.SaveChangesAsync(cancellationToken);

        // 4. Chamadas, da mais antiga para a mais nova (os ids ficam em ordem cronológica).
        foreach (var origem in arquivo.Chamadas.Where(chamada => chamada.BairroId != 1).OrderByDescending(chamada => chamada.AbertaMinutosAtras))
        {
            var bairro = bairros[origem.BairroId];
            var chamada = CriarChamada(origem, bairro, agora, ajustes);
            RegistrarRespostas(chamada, origem, ruas, agora);
            RegistrarAcoes(chamada, origem, ruas, agora, ajustes);

            if (origem.EncerradaMinutosAtras is int encerradaHa)
            {
                var idsDasRuas = bairrosDoArquivo.Single(b => b.Id == origem.BairroId).Ruas.Select(rua => ruas[rua.Id].Id).ToList();
                chamada.Encerrar(agora.AddMinutes(-encerradaHa), idsDasRuas);
            }

            contexto.Chamadas.Add(chamada);
            await contexto.SaveChangesAsync(cancellationToken);
        }

        return ajustes;
    }

    private static Chamada CriarChamada(ChamadaDoArquivo origem, Bairro bairro, DateTime agora, List<string> ajustes)
    {
        var inicio = agora.AddMinutes(-origem.AbertaMinutosAtras);

        if (origem.Tipo == "real")
        {
            // Chamada real exige aviso oficial: usa o título do arquivo ou um genérico do INMET.
            var titulo = string.IsNullOrWhiteSpace(origem.AvisoTitulo) ? AvisoPadrao : origem.AvisoTitulo;
            var aviso = new AvisoOficial("INMET", $"SIMULADO-{origem.Id}", titulo, SeveridadeAviso.Perigo);
            return new ChamadaReal(bairro.Id, aviso, inicio);
        }

        if (origem.Tipo != "treino")
        {
            throw new InvalidOperationException($"Tipo de chamada desconhecido na semente: {origem.Tipo}.");
        }

        // O treino tem prazo: dura o tempo entre a abertura e o encerramento do arquivo (ou o padrão, se estiver aberto).
        var duracao = origem.EncerradaMinutosAtras is int encerradaHa
            ? origem.AbertaMinutosAtras - encerradaHa
            : ChamadaTreino.DuracaoPadraoMinutos;
        var duracaoValida = Math.Clamp(duracao, ChamadaTreino.DuracaoMinimaMinutos, ChamadaTreino.DuracaoMaximaMinutos);
        if (duracaoValida != duracao)
        {
            ajustes.Add($"Chamada {origem.Id}: duração do treino ajustada de {duracao} para {duracaoValida} minutos.");
        }

        return new ChamadaTreino(bairro.Id, inicio, duracaoValida);
    }

    private static void RegistrarRespostas(Chamada chamada, ChamadaDoArquivo origem, Dictionary<string, Rua> ruas, DateTime agora)
    {
        foreach (var resposta in origem.Respostas)
        {
            chamada.RegistrarResposta(
                idLocal: null,
                ruas[resposta.RuaId],
                ParaSituacao(resposta.Situacao),
                resposta.Pessoas,
                observacao: null,
                CanalResposta.App,
                respondidaEm: agora.AddMinutes(-resposta.RespondidaMinutosAtras),
                agora: agora.AddMinutes(-resposta.RecebidaMinutosAtras));
        }
    }

    private static void RegistrarAcoes(Chamada chamada, ChamadaDoArquivo origem, Dictionary<string, Rua> ruas, DateTime agora, List<string> ajustes)
    {
        foreach (var acao in origem.Acoes)
        {
            var momento = agora.AddMinutes(-acao.MinutosAtras);

            // Só dá para registrar ação enquanto a chamada está aberta: se o arquivo marcar uma ação
            // no instante do prazo do treino ou depois dele, ela vai para 1 minuto antes do fim.
            if (chamada.EncerramentoPrevistoEm is DateTime prazo && momento >= prazo)
            {
                ajustes.Add($"Chamada {origem.Id}: ação em {acao.RuaId} ajustada para 1 minuto antes do fim do treino.");
                momento = prazo.AddMinutes(-1);
            }

            chamada.RegistrarAcao(ruas[acao.RuaId], ParaAcao(acao.Tipo), momento);
        }
    }

    private static double EmCentimetros(double metros) => Math.Round(metros * 100, 2);

    private static NivelRisco ParaRisco(string nivel) => nivel switch
    {
        "baixo" => NivelRisco.Baixo,
        "medio" => NivelRisco.Medio,
        "alto" => NivelRisco.Alto,
        _ => throw new InvalidOperationException($"Nível de risco desconhecido na semente: {nivel}.")
    };

    private static SituacaoRua ParaSituacao(string situacao) => situacao switch
    {
        "tudo_bem" => SituacaoRua.TudoBem,
        "agua_subindo" => SituacaoRua.AguaSubindo,
        "preciso_ajuda" => SituacaoRua.PrecisoDeAjuda,
        _ => throw new InvalidOperationException($"Situação desconhecida na semente: {situacao}.")
    };

    private static TipoAcao ParaAcao(string tipo) =>
        Enum.TryParse<TipoAcao>(tipo, ignoreCase: false, out var acao) && Enum.IsDefined(acao)
            ? acao
            : throw new InvalidOperationException($"Tipo de ação desconhecido na semente: {tipo}.");

    // ---------- Formato do arquivo (ids do painel, tempos em "minutos atrás") ----------

    private sealed record ArquivoDoPainel(List<BairroDoArquivo> Bairros, List<ReguaDoArquivo> Reguas, List<ChamadaDoArquivo> Chamadas);

    private sealed record BairroDoArquivo(
        int Id,
        string Nome,
        string Cidade,
        string Uf,
        string CodigoIbge,
        double Latitude,
        double Longitude,
        double PercentualSemApp,
        int SemAppMedidoMinutosAtras,
        List<RuaDoArquivo> Ruas);

    private sealed record RuaDoArquivo(
        string Id,
        string Nome,
        string Microarea,
        string NivelRisco,
        double Latitude,
        double Longitude,
        List<double[]>? Tracado);

    private sealed record ReguaDoArquivo(
        string Id,
        int BairroId,
        string RuaId,
        string Local,
        double? Latitude,
        double? Longitude,
        double LimiteMetros,
        List<LeituraDoArquivo> Leituras);

    private sealed record LeituraDoArquivo(int MinutosAtras, double NivelMetros);

    private sealed record ChamadaDoArquivo(
        string Id,
        int BairroId,
        string Tipo,
        int AbertaMinutosAtras,
        int? EncerradaMinutosAtras,
        string? AvisoTitulo,
        List<RespostaDoArquivo> Respostas,
        List<AcaoDoArquivo> Acoes);

    private sealed record RespostaDoArquivo(
        string RuaId,
        string Situacao,
        int? Pessoas,
        int RespondidaMinutosAtras,
        int RecebidaMinutosAtras);

    private sealed record AcaoDoArquivo(string RuaId, string Tipo, int MinutosAtras);
}

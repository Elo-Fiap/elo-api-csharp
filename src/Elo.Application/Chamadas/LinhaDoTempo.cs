using Elo.Application.Abstracoes;
using Elo.Domain.Chamadas;

namespace Elo.Application.Chamadas;

/// <summary>
/// Monta o registro da chamada ("fica o registro"): junta, com LINQ, eventos de origens
/// diferentes (abertura, respostas, ações, sensor, encerramento) e ordena pela hora.
/// </summary>
internal static class LinhaDoTempo
{
    public static IReadOnlyList<EventoDoRegistroDto> Montar(
        Chamada chamada,
        IEnumerable<Resposta> respostas,
        IEnumerable<AcaoCoordenador> acoes,
        IEnumerable<AlertaDeSensor> alertas,
        IReadOnlyDictionary<int, string> nomesDasRuas,
        DateTime agora)
    {
        string? NomeDa(int ruaId) => nomesDasRuas.GetValueOrDefault(ruaId);

        var abertura = new EventoDoRegistroDto(
            chamada.IniciadaEm,
            TipoDeEvento.ChamadaAberta,
            null,
            null,
            chamada is ChamadaReal real
                ? $"Chamada real aberta pelo aviso {real.Aviso}."
                : "Treino da chamada iniciado (simulado).");

        var eventosDeResposta = respostas.Select(resposta => new EventoDoRegistroDto(
            resposta.RespondidaEm,
            TipoDeEvento.Resposta,
            resposta.RuaId,
            NomeDa(resposta.RuaId),
            DescreverResposta(resposta)));

        var eventosDeAcao = acoes.Select(acao => new EventoDoRegistroDto(
            acao.RegistradaEm,
            TipoDeEvento.AcaoDoCoordenador,
            acao.RuaId,
            NomeDa(acao.RuaId),
            DescreverAcao(acao.Tipo)));

        // O ESP32 manda leituras o tempo todo: mostramos a primeira vez que cada sensor passou do limite.
        var eventosDeSensor = alertas
            .GroupBy(alerta => alerta.CodigoSensor)
            .Select(grupo =>
            {
                var primeiro = grupo.MinBy(alerta => alerta.MedidaEm)!;
                var maximo = grupo.Max(alerta => alerta.NivelCm);
                return new EventoDoRegistroDto(
                    primeiro.MedidaEm,
                    TipoDeEvento.SensorAcimaDoLimite,
                    primeiro.RuaId,
                    primeiro.Rua,
                    $"Sensor {grupo.Key} passou do limite de {primeiro.LimiteCm:0.#} cm " +
                    $"({primeiro.NivelCm:0.#} cm). Máximo no período: {maximo:0.#} cm.");
            });

        IEnumerable<EventoDoRegistroDto> encerramento = chamada switch
        {
            { EncerradaEm: DateTime encerrada } =>
                [new EventoDoRegistroDto(encerrada, TipoDeEvento.ChamadaEncerrada, null, null, "Chamada encerrada.")],
            { EncerramentoPrevistoEm: DateTime prazo } when prazo <= agora =>
                [new EventoDoRegistroDto(prazo, TipoDeEvento.ChamadaEncerrada, null, null, "Treino terminou no prazo previsto.")],
            _ => []
        };

        return [.. new[] { abertura }
            .Concat(eventosDeResposta)
            .Concat(eventosDeAcao)
            .Concat(eventosDeSensor)
            .Concat(encerramento)
            .OrderBy(evento => evento.Momento)
            .ThenBy(evento => evento.Tipo)];
    }

    private static string DescreverResposta(Resposta resposta)
    {
        var texto = resposta.Situacao switch
        {
            SituacaoRua.PrecisoDeAjuda => $"Preciso de ajuda ({resposta.Pessoas} pessoa(s))",
            SituacaoRua.AguaSubindo => "Água subindo",
            _ => "Tudo bem"
        };

        var canal = resposta.Canal == CanalResposta.App ? "pelo app" : $"por {resposta.Canal}";
        return $"{texto}, {canal}.";
    }

    private static string DescreverAcao(TipoAcao tipo) => tipo switch
    {
        TipoAcao.VouAteLa => "Coordenação marcou \"Vou até lá\".",
        TipoAcao.Ligacao => "Coordenação ligou para a rua.",
        _ => "Coordenação chamou a rua no WhatsApp."
    };
}

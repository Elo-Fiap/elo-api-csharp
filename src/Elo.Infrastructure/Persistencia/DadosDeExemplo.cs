using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Ruas;
using Elo.Domain.Sensores;
using Elo.Infrastructure.Persistencia.Semente;

namespace Elo.Infrastructure.Persistencia;

/// <summary>
/// Dados de exemplo da GS. Tudo é simulado: ruas, apelidos, telefones (DDD 00, que não existe)
/// e respostas, como pede a regra "nada de dados pessoais reais".
/// Os dados são criados pelos construtores e métodos do domínio, então passam pelas mesmas regras da API.
/// <para>
/// As ruas do piloto usam traçados reais do OpenStreetMap (© colaboradores do OpenStreetMap, licença ODbL,
/// https://www.openstreetmap.org/copyright) com os nomes fictícios da API; os arquivos estão em <c>Semente/</c>.
/// </para>
/// </summary>
internal static class DadosDeExemplo
{
    /// <summary>
    /// Cadastra o piloto (bairro 1) e, se <paramref name="incluirBairrosDoPainel"/>, os bairros extras do painel.
    /// Devolve os ajustes que a semente dos extras precisou fazer para respeitar as regras do domínio.
    /// </summary>
    public static async Task<IReadOnlyList<string>> PopularAsync(
        EloDbContext contexto,
        DateTime agora,
        bool incluirBairrosDoPainel,
        CancellationToken cancellationToken)
    {
        // 1. Bairro (salvo antes, para as ruas receberem o Id dele). O piloto também tem UF, código IBGE,
        //    centro no mapa e o resultado do simulado feito sem o app (31%, medido há 45 dias).
        var bairro = new Bairro(
            "Jardim Pantanal (simulado)", "São Paulo", "São Miguel Paulista", dadosSimulados: true,
            uf: "SP", codigoIbge: "3550308", latitude: -23.47939, longitude: -46.40912,
            percentualSemApp: 31, semAppMedidoEm: agora.AddDays(-45));
        contexto.Bairros.Add(bairro);
        await contexto.SaveChangesAsync(cancellationToken);

        // 2. Oito ruas em três microáreas, como no simulado da proposta.
        var garcas = NovaRua(bairro, "Rua das Garças", "Microárea 1", NivelRisco.Alto, "Dona Cida", 1);
        var aguape = NovaRua(bairro, "Rua do Aguapé", "Microárea 1", NivelRisco.Alto, "Seu Zé", 2);
        var taboa = NovaRua(bairro, "Travessa da Taboa", "Microárea 1", NivelRisco.Medio, "Rose", 3);
        var barqueiros = NovaRua(bairro, "Rua dos Barqueiros", "Microárea 2", NivelRisco.Alto, "Tião", 4);
        var varzea = NovaRua(bairro, "Rua da Várzea", "Microárea 2", NivelRisco.Medio, "Neide", 5);
        var corrego = NovaRua(bairro, "Rua do Córrego Seco", "Microárea 2", NivelRisco.Baixo, "Juca", 6);
        var palafitas = NovaRua(bairro, "Rua das Palafitas", "Microárea 3", NivelRisco.Alto, "Dona Fátima", 7);
        var ipe = NovaRua(bairro, "Rua Ipê Amarelo", "Microárea 3", NivelRisco.Baixo, "Marquinhos", 8);
        Rua[] ruas = [garcas, aguape, taboa, barqueiros, varzea, corrego, palafitas, ipe];

        garcas.DefinirPlano(new PlanoDaRua(
            "Subir pela Rua Ipê Amarelo, que é o ponto mais alto da microárea.",
            "Praça da caixa d'água (simulada)",
            "Escola estadual do bairro (simulada)",
            ["Documentos em saco plástico", "Remédios de uso contínuo", "Lanterna e pilhas", "Carregador de celular", "Água potável"],
            agora.AddDays(-20)));

        aguape.DefinirPlano(new PlanoDaRua(
            "Sair pelo fim da rua, longe do córrego, até a Rua Ipê Amarelo.",
            "Praça da caixa d'água (simulada)",
            "Escola estadual do bairro (simulada)",
            ["Documentos em saco plástico", "Remédios de uso contínuo", "Roupa seca"],
            agora.AddDays(-20)));

        palafitas.DefinirPlano(new PlanoDaRua(
            "Atravessar a ponte antes de a água chegar ao segundo degrau e seguir pela Rua da Várzea.",
            "Igreja da Rua da Várzea (simulada)",
            "CEU do bairro (simulado)",
            ["Documentos em saco plástico", "Remédios", "Roupa seca", "Ração dos animais"],
            agora.AddDays(-15)));

        // Traçado e ponto de cada rua, na mesma ordem em que elas foram criadas acima.
        var geografia = GeografiaDoPiloto.Carregar();
        if (geografia.Ruas.Count != ruas.Length)
        {
            throw new InvalidOperationException(
                $"A semente tem {ruas.Length} ruas, mas o arquivo de traçados tem {geografia.Ruas.Count}.");
        }

        for (var i = 0; i < ruas.Length; i++)
        {
            ruas[i].DefinirGeografia(geografia.Ruas[i].Ponto.Latitude, geografia.Ruas[i].Ponto.Longitude, geografia.Ruas[i].Tracado);
        }

        contexto.Ruas.AddRange(ruas);
        await contexto.SaveChangesAsync(cancellationToken);

        // 3. Réguas (ESP32 + HC-SR04) nos pontos críticos, com leituras da última hora (a cada 10 minutos).
        //    "regua-exemplo" é o id fixo no firmware da régua da equipe (repositório elo-regua-esp32), limite de 1,20 m.
        var reguaPalafitas = new Sensor(palafitas.Id, "regua-exemplo", "Córrego do bairro, na ponte da Rua das Palafitas (simulado)", 120);
        var reguaBarqueiros = new Sensor(barqueiros.Id, "regua-barqueiros", "Bueiro da esquina com a Rua da Várzea (simulado)", 150);
        reguaPalafitas.DefinirLocalizacao(geografia.PontoDaRegua.Latitude, geografia.PontoDaRegua.Longitude);
        contexto.Sensores.AddRange(reguaPalafitas, reguaBarqueiros);
        await contexto.SaveChangesAsync(cancellationToken);

        RegistrarLeituras(reguaPalafitas, agora, [80, 95, 105, 112, 125, 142]);             // passa do limite de 1,20 m
        RegistrarLeituras(reguaBarqueiros, agora.AddMinutes(-1), [60, 72, 85, 90, 94, 97]); // abaixo do limite de 1,50 m

        // 4. Treino de uma semana atrás, já encerrado (serve para comparar o indicador).
        var idsDasRuas = ruas.Select(rua => rua.Id).ToList();
        var inicioDoTreino = agora.AddDays(-7);
        var treino = new ChamadaTreino(bairro.Id, inicioDoTreino, duracaoMinutos: 30);
        Responder(treino, garcas, SituacaoRua.TudoBem, inicioDoTreino, 3);
        Responder(treino, aguape, SituacaoRua.AguaSubindo, inicioDoTreino, 4);
        Responder(treino, taboa, SituacaoRua.TudoBem, inicioDoTreino, 6);
        Responder(treino, varzea, SituacaoRua.TudoBem, inicioDoTreino, 7);
        Responder(treino, palafitas, SituacaoRua.PrecisoDeAjuda, inicioDoTreino, 9, pessoas: 2);
        Responder(treino, ipe, SituacaoRua.TudoBem, inicioDoTreino, 12);
        Responder(treino, barqueiros, SituacaoRua.TudoBem, inicioDoTreino, 20); // chegou depois dos 15 minutos
        treino.Encerrar(inicioDoTreino.AddMinutes(30), idsDasRuas);              // Rua do Córrego Seco ficou em silêncio
        contexto.Chamadas.Add(treino);
        await contexto.SaveChangesAsync(cancellationToken);                      // treino = chamada 1

        // 5. Chamada real aberta há 25 minutos, motivada por um aviso (simulado) do INMET.
        var inicio = agora.AddMinutes(-25);
        var aviso = new AvisoOficial("INMET", "SIMULADO-2026-0001", "Acumulado de Chuva", SeveridadeAviso.PerigoPotencial);
        var real = new ChamadaReal(bairro.Id, aviso, inicio);
        Responder(real, taboa, SituacaoRua.TudoBem, inicio, 4);
        Responder(real, aguape, SituacaoRua.AguaSubindo, inicio, 5);
        Responder(real, ipe, SituacaoRua.TudoBem, inicio, 6);
        Responder(real, garcas, SituacaoRua.PrecisoDeAjuda, inicio, 8, pessoas: 3,
            observacao: "Água entrando nas casas do fim da rua.");
        real.RegistrarAcao(garcas, TipoAcao.VouAteLa, inicio.AddMinutes(10));
        Responder(real, varzea, SituacaoRua.AguaSubindo, inicio, 12, canal: CanalResposta.Sms, chegouAposMinutos: 14);
        real.RegistrarAcao(barqueiros, TipoAcao.ContatoWhatsApp, inicio.AddMinutes(16));
        Responder(real, aguape, SituacaoRua.PrecisoDeAjuda, inicio, 18, pessoas: 5,
            observacao: "Água na altura do joelho, 5 pessoas no térreo.");
        // Rua dos Barqueiros, Rua das Palafitas e Rua do Córrego Seco estão em silêncio.

        contexto.Chamadas.Add(real);
        await contexto.SaveChangesAsync(cancellationToken);                      // chamada real aberta = chamada 2

        // 6. Bairros extras do painel de administração (ids 2 a 12), só se a configuração pedir.
        return incluirBairrosDoPainel
            ? await SementeDoPainel.IncluirAsync(contexto, agora, cancellationToken)
            : [];
    }

    private static Rua NovaRua(Bairro bairro, string nome, string microarea, NivelRisco risco, string apelido, int numero) =>
        new(bairro.Id, nome, microarea, risco, new Lideranca(apelido, $"55009000000{numero:D2}"));

    private static void RegistrarLeituras(Sensor sensor, DateTime agora, double[] niveis)
    {
        for (var i = 0; i < niveis.Length; i++)
        {
            var momento = agora.AddMinutes(-10 * (niveis.Length - 1 - i));
            sensor.RegistrarLeitura(niveis[i], momento, momento);
        }
    }

    private static void Responder(
        Chamada chamada,
        Rua rua,
        SituacaoRua situacao,
        DateTime inicio,
        int aposMinutos,
        int? pessoas = null,
        string? observacao = null,
        CanalResposta canal = CanalResposta.App,
        int? chegouAposMinutos = null)
    {
        var respondidaEm = inicio.AddMinutes(aposMinutos);
        var recebidaEm = inicio.AddMinutes(chegouAposMinutos ?? aposMinutos);
        chamada.RegistrarResposta(idLocal: null, rua, situacao, pessoas, observacao, canal, respondidaEm, recebidaEm);
    }
}

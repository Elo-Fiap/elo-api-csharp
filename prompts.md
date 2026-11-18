# Uso de IA (prompts)

A regra da equipe para a GS diz: "IA pode, com os prompts em prompts.md".
Este arquivo registra como a IA foi usada na API em C#.

## Ferramenta

Claude Code (modelo Claude Opus 5.5, da Anthropic), no aplicativo desktop do Claude, em 09/11/2026.

## Prompt principal

> Claude, seguinte, faz uma pasta em desktop chamada gs-sharp, vou te mandar a documentação do escopo, e quero que vc desenvolva uma api em c# seguindo tudo que se pede no print da atividade que eu mandei, e seguindo o escopo da documentação e seu tema abordado

Anexos enviados junto com o prompt:

- print do enunciado "GS - C# Software Development" (entregáveis e critérios de avaliação);
- PDF "Elo. Proposta para o grupo" (a proposta da CitizenTech, com o papel da API em C#).

## Prompt seguinte

> aqui o link do repo, quero que faça um PR e com o que fizemos e adicione o nome do João victor tambem no trabalho
>
> (link: https://github.com/Elo-Fiap/elo-regua-esp32.git)

A IA viu que a conta só tinha leitura no `elo-regua-esp32` (a parte de Physical Computing) e que a organização
bloqueia fork. Ao ler o `elo-app`, achou o contrato oficial da API (`docs/contrato-api.md`), que até então não estava
disponível. As respostas dadas às perguntas dela foram:

> criei um chamado gs-csharp, adiciona la

(o repositório criado na organização é o `Elo-Fiap/elo-api-csharp`)

> Adaptar antes do PR

> bora filho faz oq vc falou

Com isso, a IA adaptou a API ao contrato do app (rotas `/situacao`, `/chamadas`, `/regua`, `/ruas/{id}/plano` e
`/leituras` com a chave do ESP32), ajustou as regras (pessoas opcional, observação de até 200 caracteres, id em texto),
rodou os 115 testes, gerou as evidências de novo e abriu o pull request no `elo-api-csharp`.

Depois, para completar o contrato:

> Fazer o /avisos

A IA acrescentou o `GET /avisos?codigoIbge=`: a API baixa os avisos do INMET em segundo plano, guarda em cache e devolve
só os do município, com as mesmas regras do `inmet.ts` do app (e os mesmos casos de teste, com o fixture do app).

## Perguntas que a IA fez e as respostas

| Pergunta | Resposta escolhida |
|---|---|
| O PC não tinha o .NET SDK (só os runtimes). Pode instalar o .NET 10 SDK pelo winget? | Pode instalar |
| Qual banco usar com o EF Core? | SQLite (sem instalar nada; as migrations criam o banco) |
| O PDF cita um `docs/contrato-api.md` que não estava no PC. Como seguir? | Seguir o PDF e documentar o contrato em `docs/contrato-api.md` |
| Seguir direto com o desenho proposto ou revisar antes? | Seguir direto |

## O que a IA fez

- Leu o PDF e o enunciado e propôs o desenho (domínio, camadas, endpoints).
- Escreveu o código dos projetos `Elo.Domain`, `Elo.Application`, `Elo.Infrastructure`, `Elo.Api` e `Elo.Tests`,
  a migration inicial, os dados simulados, a página do resumo do bairro e a documentação.
- Compilou, rodou os 83 testes, subiu a API e gerou as evidências em `docs/evidencias/` com requisições reais.

## O que a equipe precisa fazer

A proposta lembra que todo mundo precisa saber explicar cada linha.
Antes da entrega, revisem juntos o código, principalmente:

- `src/Elo.Domain/Chamadas/Chamada.cs` (herança e polimorfismo);
- `src/Elo.Domain/Painel/PriorizadorDeRuas.cs` (o LINQ que ordena as ruas em silêncio);
- `src/Elo.Infrastructure/Persistencia/Configuracoes/ChamadaConfiguration.cs` (mapeamento TPH no EF Core);
- `src/Elo.Application/Chamadas/RespostaService.cs` (reenvio offline sem duplicar);
- `src/Elo.Application/ContratoDoApp/ContratoDoAppService.cs` (como o app e a régua conversam com a API);
- `src/Elo.Infrastructure/Avisos/FonteDeAvisosEmCache.cs` (o padrão Decorator no cache do INMET).

Se usarem IA para mudar algo depois, acrescentem o prompt aqui.

## Revisão do PR

> com base nos criterios do print e na documentação, quero q vc revise o PR q eu criei e verifique se está compativel 100% com o que se pede, quero uma nota de 0 a 100

Anexos: o mesmo print do enunciado e o PDF da proposta.

A IA leu o código, rodou os testes e subiu uma cópia da API para conferir os endpoints. Ela apontou seis pontos a
melhorar e lembrou que o repositório está privado e que a `main` ainda não tem o código. A resposta foi:

> Sim, corrigir

Com isso, a IA:

- corrigiu a documentação desatualizada: `pessoas` é opcional no Swagger e no roteiro `.http`, e o `&gt;` saiu da descrição do `GET /regua`;
- deixou o `PlanoDaRua` imutável e comparado por valor, inclusive a lista de itens;
- trocou os `is`/`as` da linha do tempo e do DTO da chamada por polimorfismo (`DescreverAbertura`, `AvisoQueMotivou` e `DuracaoPrevistaEmMinutos`);
- transformou o cache do INMET num Decorator de verdade: o `ClienteDoInmet` e a `FonteDeAvisosEmCache` implementam a mesma `IFonteDeAvisos`;
- criou `GET /api/chamadas/{id}/acoes/{acaoId}` e `GET /api/sensores/{id}/leituras/{leituraId}`, para o `Location` do 201 apontar para o recurso criado;
- explicou no README por que a segunda migration não gera SQL.

No fim, 154 testes passando e build sem avisos.

## Painel de administração e dados para o app (João, 12 e 14/11/2026)

> queria tambem fazer um site web de admin, só que com gráficos e monitoramentos com overview geral [...] com o mapa do brasil [...] e ver numa rua em especifico [...] onde está localizado o esp e onde foi o chamado

> Você pode agora atualizar o mobile e o de SOA para receber esses dados [...] e aí atualizar todas as documentações

Ferramenta: Claude Code. Com isso, a IA:

- acrescentou ao domínio UF, código IBGE e coordenadas do bairro, coordenadas e traçado da rua (do OpenStreetMap), coordenadas da régua e o resultado do simulado sem o app (`PercentualSemApp`, `SemAppMedidoEm`), com as migrations `PainelAdmin` e `NivelDeAtencao`;
- semeou 11 bairros simulados extras para o painel (`semente-painel.json`, desligável por `DadosDeExemplo:IncluirBairrosDoPainel`);
- criou `GET /api/admin/instantaneo`, que o painel web (`elo-admin`) lê a cada 15 segundos;
- acrescentou o nível de atenção da régua (`AtencaoCm`, 100 cm na `regua-exemplo`, como o firmware) e campos opcionais em `GET /situacao` (bairro, coordenadas, "sem o app") e `GET /regua` (rua, coordenadas, atenção), que o app passa a ler;
- documentou tudo em `docs/contrato-painel.md`, `docs/contrato-api.md` e neste README.

Decisões da IA que a equipe precisa conferir:

- Os percentuais "sem o app" da semente (31% no piloto, de 22% a 40% nos outros bairros) são inventados. O valor de verdade sai do simulado por WhatsApp e telefone e entra pelo `PUT /api/bairros/{id}`.
- No `PUT /api/sensores/{id}`, omitir `atencaoCm` apaga o nível de atenção (o PUT troca o recurso inteiro).
- O painel e o app leem tudo desta API; o Elo Alertas tem as mesmas rotas da régua, mas os dois serviços não trocam dados.

No fim, 243 testes passando. Na revisão do PR, mais 5 (248): o nível de atenção do `/regua` sempre abaixo do limite da mesma resposta, e o cache do INMET espera 1 minuto depois de falha mesmo sem lista guardada.

## Módulo de Cybersecurity (CitizenTech, 18/11/2026)

O professor de Cybersecurity pediu quatro coisas para a mesma entrega: (1) parsing de PII, achando CPF, RG, laudos de saúde e senhas no que entra e mascarando antes de gravar (exemplo dele: `***.456.***-00`);
(2) taint analysis, seguindo o dado marcado da entrada até as saídas e barrando a impressão dele no log e nas respostas HTTP; (3) um LLM que traduz o bloqueio para um cidadão leigo de 60 anos em 2 frases;
(4) um painel com farol verde, amarelo e vermelho e a mensagem traduzida. O pedido ao Claude Code foi acrescentar isso à API sem mudar nenhum contrato do app nem do painel e sem quebrar os testes existentes.
(Este resumo não é uma citação literal do prompt.)

Ferramenta: Claude Code. Com isso, a IA:

- criou o projeto `Elo.Seguranca` (motor de PII com regex e validação de CPF e CNS, guarda de saída, registro de eventos em memória, calculadora do Risco Cidadão e tradutor com mensagens prontas), sem mexer no `Elo.Domain`;
- ligou o módulo à API: um middleware de entrada que marca o dado, um provider de log que passa tudo pela guarda, um middleware de resposta e um interceptor do EF Core que mascara o que vai para o banco;
- fez o `TradutorGemini` (chamada REST ao Gemini, com timeout e queda para as mensagens prontas) e o worker que traduz em segundo plano, com cache por assinatura do evento;
- criou `GET /api/seguranca/painel` e `GET /api/seguranca/eventos`, a página `wwwroot/risco-cidadao.html` e o roteiro de demonstração `docs/cyber/demo.sh`;
- escreveu os testes, incluindo os de integração que conferem que o dado não aparece no banco, no log nem nas respostas, e que o prompt enviado ao LLM não tem o dado.

Decisões da IA que a equipe precisa conferir:

- O dado não é reescrito na entrada: ele é só marcado, e a máscara é aplicada nas saídas. Assim o corpo que o domínio recebe não muda e nenhum contrato quebra.
- O contato da liderança (`whatsapp`, `linkWhatsapp`, `apelido`) é isento de propósito. É um risco aceito, porque a coordenação precisa chamar a liderança.
- Os eventos de segurança ficam em memória (sem migration) e somem quando a API reinicia.
- Só eventos de segurança, com metadados e sem o dado, vão para o LLM. Aviso de chuva nunca passa por ele.
- A chave do Gemini fica fora do repositório (`dotnet user-secrets` ou `Ia__Gemini__ChaveApi`). Sem ela, o painel usa mensagens prontas.
- A chamada real ao Gemini não foi testada (os testes usam um servidor falso, sem rede): conferir uma vez com uma chave de verdade antes de gravar o vídeo.
- As regex de telefone, CPF sem pontuação e cartão do SUS exigem fronteira de palavra: um GUID no caminho do banco, no log de inicialização, parecia telefone e deixava o farol vermelho às vezes (teste intermitente).

Na revisão final, a IA corrigiu: emoji na observação derrubava o pedido de ajuda (500), porque tirar o acento de meio emoji lança exceção;
a rota que vai para o evento, o painel e o prompt agora só guarda nomes de rota (dado pessoal no caminho vira `{id}`), e o prompt manda o modelo ignorar instruções dentro da descrição (prompt injection);
número sem aspas em campo com nome sensível (`"pin": 1234`) também é marcado; e o aviso de "API fora do ar" do painel não é mais repetido pelo leitor de tela a cada 3 segundos.

Depois, a pedido do João, o LLM passou a ser o Claude: a IA criou o `TradutorClaude` com o SDK oficial da Anthropic para C# (pacote `Anthropic`, modelo `claude-opus-5-5`,
esforço baixo e fallback do servidor para recusas), a interface `IProvedorDeIa` para o worker não depender de um LLM só, e a opção `Ia:Provedor` (padrão `Claude`; o Gemini continua como alternativa).
A chave fica em `Ia:Claude:ChaveApi` (user-secrets ou `Ia__Claude__ChaveApi`). A chamada real ao Claude também não foi testada: conferir com a chave antes de gravar o vídeo.

No fim, 476 testes passando (os 248 anteriores, os do módulo de Cybersecurity e os de integração) e build sem avisos.

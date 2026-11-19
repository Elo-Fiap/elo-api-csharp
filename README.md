# Elo API (C# / .NET 10)

## Ficha da CitizenTech

| | |
|---|---|
| **Nome** | Elo |
| **Cidade e bairro** | São Paulo, Jardim Pantanal (bairro do piloto) |
| **Cidadão beneficiado** | Moradores de ruas que alagam, por meio da liderança da rua ou do voluntário do núcleo comunitário de Defesa Civil (NUPDEC) |
| **Problema, em uma frase** | Durante a chuva forte, quem cuida do bairro não consegue saber quais ruas estão bem, quais precisam de ajuda e quais ficaram em silêncio. |
| **Como funciona** | O aviso oficial chega, a liderança de cada rua responde em um toque, mesmo com internet ruim, e o coordenador vê as ruas sem resposta. Um sensor mede a água no ponto crítico. |
| **O que cada parte faz** | App: aviso, chamada, plano e registro. **API (este repositório): guarda as chamadas e ordena as ruas em silêncio.** ESP32: mede a água e toca o buzzer. ML: indica o nível de atenção por subprefeitura. Segurança: evita vazamento do que é digitado. Linux: hospeda a API. Testes: requisitos e casos de teste. |
| **Link do repositório** | https://github.com/Elo-Fiap/elo-api-csharp |
| **Indicador de impacto** | Percentual de ruas com situação conhecida em 15 minutos, comparando o simulado sem e com o app |
| **ODS conectados** | ODS 11 (meta 11.5) e ODS 13 (meta 13.1) |

## Integrantes

| Nome | RM |
|---|---|
| Bruno Leão | RM555563 |
| João Victor Franco | RM556790 |
| Lucca Borges | RM554608 |
| Rodrigo Jimenez | RM558148 |
| Ruan Melo | RM557599 |

---

## Sumário

- [O que é a API](#o-que-é-a-api)
- [Como executar](#como-executar)
- [Ligar o app e a régua (ESP32)](#ligar-o-app-e-a-régua-esp32)
- [Endpoints](#endpoints)
- [Painel de administração](#painel-de-administração)
- [Módulo de Cybersecurity (CitizenTech)](#módulo-de-cybersecurity-citizentech)
- [Arquitetura](#arquitetura)
- [Onde cada critério da avaliação aparece](#onde-cada-critério-da-avaliação-aparece)
- [Regras de negócio](#regras-de-negócio)
- [Evidências de funcionamento](#evidências-de-funcionamento)
- [Decisões e limitações](#decisões-e-limitações)
- [Uso de IA](#uso-de-ia)

## O que é a API

O alerta oficial avisa a cidade, mas ninguém fica sabendo quem está bem depois dele. No Elo, durante a chuva forte,
o coordenador do bairro abre uma **chamada**; a liderança de cada rua responde em um toque
(**Tudo bem**, **Água subindo** ou **Preciso de ajuda**), e o coordenador vê **primeiro quem pediu ajuda e depois quem ficou em silêncio**.

Esta API é o backend dessa ideia. Ela:

- guarda bairros, ruas, chamadas, respostas e ações do coordenador ("Vou até lá");
- **ordena as ruas com LINQ** para o painel do coordenador (quem pediu ajuda, depois quem está em silêncio, com a régua e o risco da rua pesando na ordem);
- calcula o **indicador de impacto** da proposta: percentual de ruas com situação conhecida em 15 minutos;
- recebe as leituras da **régua (ESP32)** e marca as que passam do limite;
- **fala a língua do app**: implementa as rotas do contrato do `elo-app` (`/situacao`, `/chamadas`, `/regua`, `/ruas/{id}/plano`, `/leituras` e `/avisos`);
- repassa os **avisos do INMET já filtrados pelo município**: baixa o Brasil inteiro em segundo plano e devolve ao celular só o que vale para a área (de cerca de 150 KB, ou mais de 580 KB em dias de muitos avisos, para uns 2 KB);
- aceita o **reenvio offline** das respostas sem duplicar (cada resposta tem um id gerado no celular);
- separa **chamada real** (motivada por um aviso oficial do INMET, porque o Elo não cria alerta próprio) de **treino** (simulado com prazo);
- protege a privacidade: mascara CPF, telefone e e-mail digitados na observação e **apaga as respostas depois de 30 dias**, guardando só o indicador.

```mermaid
flowchart LR
    INMET["Avisos oficiais (INMET)"] -- "avisos/ativos, a cada 5 min" --> API
    APP["App Expo (elo-app)<br/>liderança e coordenador"] -- "/situacao, /chamadas, /ruas/{id}/plano, /regua, /avisos" --> API["Elo API<br/>C# .NET 10 + EF Core"]
    ESP["Régua ESP32 + HC-SR04<br/>(elo-regua-esp32)"] -- "POST /leituras" --> API
    COORD["Coordenador (Swagger ou página do bairro)"] -- "/api/... abre e encerra chamadas" --> API
    VM["VM Linux"] -. "hospeda" .-> API
```

> Todos os dados de exemplo são **simulados**: bairro "Jardim Pantanal (simulado)", 8 ruas em 3 microáreas,
> apelidos de lideranças e telefones fictícios (DDD 00, que não existe). Nada de dados pessoais reais.

## Como executar

**Pré-requisito:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Não precisa instalar banco: é SQLite.

```bash
dotnet run --project src/Elo.Api
```

Na primeira execução a API aplica as migrations, cria o arquivo `src/Elo.Api/elo.db` e cadastra os dados simulados
(uma chamada real aberta, um treino antigo e a régua `regua-exemplo`). Depois abra:

| Endereço | O que é |
|---|---|
| http://localhost:5080/swagger | Documentação e testes no **Swagger UI** |
| http://localhost:5080/scalar | A mesma documentação no **Scalar** |
| http://localhost:5080/openapi/v1.json | Documento OpenAPI 3.1 |
| http://localhost:5080/ | Resumo do bairro com a cara do Elo: chamada aberta, ruas por prioridade e a última leitura da régua |
| http://localhost:5080/risco-cidadao.html | Painel **Risco Cidadão** (Cybersecurity): farol, índice de risco e as falhas traduzidas para o cidadão |
| http://localhost:5080/health | Saúde da API e do banco |

A mesma API está publicada na internet em **http://elo-api-fiap.azurewebsites.net**, com as mesmas rotas (veja "Endereço público da entrega" abaixo).

O arquivo [`src/Elo.Api/Elo.Api.http`](src/Elo.Api/Elo.Api.http) tem o roteiro de requisições pronto para o Visual Studio, Rider ou VS Code (REST Client).
Para começar do zero, pare a API e apague `src/Elo.Api/elo.db`.

**Testes:**

```bash
dotnet test
```

**Migrations** (opcional, já estão no repositório):

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add NomeDaMudanca --project src/Elo.Infrastructure --startup-project src/Elo.Api --output-dir Persistencia/Migrations
```

**Publicar na VM Linux** (o ESP32 no Wokwi só alcança endereço público):

```bash
dotnet publish src/Elo.Api -c Release -o publicar
```

```bash
ASPNETCORE_URLS=http://0.0.0.0:5080 Dispositivos__Chave=<chave-da-regua> dotnet publicar/Elo.Api.dll
```

**Endereço público da entrega (Azure App Service):** http://elo-api-fiap.azurewebsites.net

A régua no Wokwi envia em HTTP puro: com HTTPS, o envio no ESP32 simulado leva cerca de 7 segundos e falha
com o buzzer tocando. Por isso o App Service fica com o **HTTPS Only desligado** e aceita `http://` sem
redirecionar. Render, Railway, Vercel e similares redirecionam tudo para HTTPS e não servem para a régua.

- Plano gratuito F1 (Linux, Brazil South), na assinatura Azure for Students. Ele dorme quando fica parado:
  a primeira chamada depois disso leva alguns segundos.
- Configurações do App Service: `Dispositivos__Chave` (chave só de demonstração, trocada depois da entrega),
  `ConnectionStrings__Elo=Data Source=/home/elo.db` (o `/home` é a pasta que sobrevive a reinícios e
  publicações) e `ASPNETCORE_ENVIRONMENT=Production`, para a API nunca aceitar leitura sem chave.
- Não precisa de banco à parte: o SQLite e os dados de exemplo são criados na primeira execução.

Para publicar uma versão nova (com o Azure CLI logado na assinatura):

```bash
dotnet publish src/Elo.Api -c Release -o publicar && (cd publicar && zip -qr ../elo-api.zip .)
```

```bash
az webapp deploy -g elo-rg -n elo-api-fiap --src-path elo-api.zip --type zip
```

Conferir de fora: `GET /health` responde 200; `POST /leituras` com o header `X-Chave-Dispositivo` e o corpo
`{"reguaId":"regua-exemplo","nivelMetros":1.42}` responde 204 (sem a chave, 401); `GET /regua` devolve 1.42.

## Ligar o app e a régua (ESP32)

**App (`elo-app`).** O app chama `EXPO_PUBLIC_API_URL` + `/situacao`, `/chamadas`, `/regua` e `/ruas/{id}/plano`.
Basta apontar para a API, sem `/api` no final:

```bash
EXPO_PUBLIC_API_URL=http://elo-api-fiap.azurewebsites.net
```

Com a API rodando no computador, use `http://<ip-da-maquina>:5080` (o celular não alcança `localhost`).

A resposta da liderança só entra se houver **chamada aberta** no bairro (o coordenador abre pelo Swagger:
`POST /api/bairros/1/chamadas`). Nos dados simulados já existe uma aberta. O bairro atendido pelas rotas do app é o
`ContratoDoApp:BairroId` do `appsettings.json` (o contrato diz que cada bairro do piloto tem a sua URL).

**Dados que o app recebe além do básico.** O `GET /situacao` traz `bairroId`, `cidade`, `uf`, `codigoIbge`, `latitude`, `longitude`,
`percentualSemApp` e `semAppMedidoEm` (o simulado sem o app, base do indicador de impacto) e, em cada rua, `latitude` e `longitude`
(para o botão "Ver no mapa"). O `GET /regua` traz `ruaId`, `latitude`, `longitude` e `atencaoMetros` (o nível de atenção, abaixo do limite;
na `regua-exemplo`, 1,00 m, como o firmware). São campos opcionais: o app que só lê os antigos continua funcionando.

Para os avisos, o app usa `GET /avisos?codigoIbge=<código da área>` quando a API do bairro está configurada;
sem ela, continua baixando o INMET direto.
A resposta já vem com os campos do tipo `Aviso` do app (`id`, `titulo`, `severidade`, `gravidade`, `inicio`, `fim`,
`quando`, `riscos`, `instrucoes`), filtrada e ordenada, e o app usa a lista sem passar pela `normalizarAvisos`.

**Régua (`elo-regua-esp32`).** O `sketch.ino` manda `POST /leituras` com `{"reguaId":"regua-exemplo","nivelMetros":1.42}`
e o header `X-Chave-Dispositivo`. A `regua-exemplo` já vem cadastrada na Rua das Palafitas, com limite de 1,20 m e atenção de 1,00 m, como no firmware.
A chave **não fica no repositório**: configure por variável de ambiente (`Dispositivos__Chave`) ou `dotnet user-secrets`.
Sem chave configurada, só o ambiente de desenvolvimento aceita a leitura (para testar no Swagger).

## Endpoints

São 42 operações REST, todas assíncronas e documentadas no Swagger/Scalar.

### Rotas do contrato do app e do ESP32 (sem `/api`)

Seguem o [`docs/contrato-api.md`](https://github.com/Elo-Fiap/elo-app/blob/main/docs/contrato-api.md) do `elo-app`:
status `tudo_bem`, `agua_subindo`, `preciso_ajuda` (nulo = silêncio), ids em texto, datas no formato do `toISOString()` e medidas em metros.

| Método | Rota | O que faz |
|---|---|---|
| GET | `/situacao` | Situação do bairro na chamada atual, na ordem do painel (tela "Bairro" do app), com cidade, UF, código IBGE, coordenadas e o `percentualSemApp` |
| POST | `/chamadas` | Recebe a resposta da rua enviada pelo app (201; 200 se o mesmo `id` já tinha chegado) |
| GET | `/regua` | Última leitura da régua, em metros (`?reguaId=` opcional), com `ruaId`, coordenadas e `atencaoMetros` |
| GET | `/ruas/{id}/plano` | Plano da rua com contatos de emergência (404 = rua sem plano) |
| GET | `/avisos?codigoIbge=` | Avisos do INMET que valem para o município, já filtrados e limpos (503 se o INMET estiver fora e não houver nada guardado) |
| POST | `/leituras` | Leitura do ESP32, com o header `X-Chave-Dispositivo` (204) |

### API completa (`/api`)

Nos dados simulados: bairro `1`, treino antigo = chamada `1`, chamada real aberta = chamada `2`.

| Método | Rota | O que faz |
|---|---|---|
| GET | `/api/bairros` | Lista os bairros, com total de ruas e a chamada aberta |
| POST | `/api/bairros` | Cadastra um bairro |
| GET / PUT / DELETE | `/api/bairros/{id}` | Detalha, atualiza ou exclui (409 se tiver chamadas) |
| GET | `/api/bairros/{id}/painel` | **Painel do coordenador** da chamada aberta, na ordem de prioridade |
| GET | `/api/bairros/{id}/indicadores` | Indicador de 15 minutos de cada chamada e média Real x Treino |
| GET | `/api/bairros/{bairroId}/ruas` | Ruas do bairro (filtros: `microarea`, `nivelRisco`, `incluirInativas`) |
| POST | `/api/bairros/{bairroId}/ruas` | Cadastra uma rua |
| GET / PUT / DELETE | `/api/ruas/{id}` | Detalha (com plano e sensores), atualiza ou exclui (409 se tiver histórico) |
| GET / PUT | `/api/ruas/{id}/plano` | Plano da rua: rota, encontro, abrigo e o que levar |
| GET | `/api/ruas/{id}/registro` | Respostas da rua agrupadas por mês |
| POST | `/api/bairros/{bairroId}/chamadas` | Abre chamada `Real` (com aviso oficial) ou `Treino` |
| GET | `/api/chamadas` | Lista com filtros (`bairroId`, `tipo`, `status`) e paginação |
| GET | `/api/chamadas/{id}` | Detalha uma chamada |
| POST | `/api/chamadas/{id}/encerramento` | Encerra e guarda a foto do indicador |
| GET | `/api/chamadas/{id}/painel` | Painel de qualquer chamada (aberta ou antiga) |
| GET | `/api/chamadas/{id}/registro` | Linha do tempo: o que foi avisado e o que foi feito |
| GET / POST | `/api/chamadas/{id}/respostas` | Lista (filtros `situacao`, `ruaId`) ou registra a resposta de uma rua |
| GET | `/api/chamadas/{id}/respostas/{respostaId}` | Detalha uma resposta |
| GET / POST | `/api/chamadas/{id}/acoes` | Lista ou registra ação do coordenador (`ContatoWhatsApp`, `Ligacao`, `VouAteLa`) |
| GET | `/api/chamadas/{id}/acoes/{acaoId}` | Detalha uma ação (é para cá que aponta o `Location` do 201) |
| GET / POST | `/api/sensores` | Lista (filtro `ruaId`) ou cadastra uma régua |
| GET / PUT / DELETE | `/api/sensores/{id}` | Detalha, atualiza ou exclui uma régua |
| GET / POST | `/api/sensores/{id}/leituras` | Lista as últimas leituras ou registra uma leitura (em cm) |
| GET | `/api/sensores/{id}/leituras/{leituraId}` | Detalha uma leitura (é para cá que aponta o `Location` do 201) |
| GET | `/api/seguranca/painel` | **Risco Cidadão**: farol, índice de 0 a 100 e a mensagem ao cidadão (veja [Módulo de Cybersecurity](#módulo-de-cybersecurity-citizentech)) |
| GET | `/api/seguranca/eventos` | Eventos de segurança mais recentes (`limite` de 1 a 500), só metadados |

Os detalhes de campos e fluxos estão em [`docs/contrato-api.md`](docs/contrato-api.md). O painel de administração usa uma rota própria, `GET /api/admin/instantaneo` (veja [Painel de administração](#painel-de-administração)).

## Painel de administração

O painel web de administração (`elo-admin`) lê tudo de **uma rota só**:

```bash
curl 'http://localhost:5080/api/admin/instantaneo?dias=30'
```

Devolve todos os bairros com todas as ruas (com UF, código IBGE, coordenadas, traçado no mapa e o resultado do simulado sem o app),
as réguas com o limite e o nível de atenção (`limiteMetros`, `atencaoMetros`) e as leituras das últimas 72 horas (em metros), as chamadas abertas ou abertas nos últimos `dias` dias (de 1 a 30; fora disso, 400)
com respostas e ações, e os avisos ativos do INMET para o município de cada bairro (se o INMET cair, `avisos` vem vazio, nunca 503).
O formato completo está em [`docs/contrato-painel.md`](docs/contrato-painel.md) e um exemplo real em [`docs/evidencias/instantaneo.json`](docs/evidencias/instantaneo.json).

**Bairros extras.** Além do piloto, a semente cria mais 11 bairros simulados (ids 2 a 12, de outras cidades) com ruas, réguas, chamadas, respostas e ações,
a partir de [`Semente/semente-painel.json`](src/Elo.Infrastructure/Persistencia/Semente/semente-painel.json). Quem controla é a configuração
`DadosDeExemplo:IncluirBairrosDoPainel` no `appsettings.json` (padrão `true`; use `false` para ter só o piloto, como nos testes de integração atuais).
Os tempos do arquivo são "minutos atrás" e viram datas a partir do momento em que o banco é criado.

**Recriar o banco.** A semente só roda com o banco vazio. Para recomeçar (ou para os dados voltarem a ser "de agora"), pare a API,
apague `src/Elo.Api/elo.db` e rode de novo:

```bash
rm src/Elo.Api/elo.db
dotnet run --project src/Elo.Api
```

**Créditos.** Os traçados das 8 ruas do piloto são reais, do **OpenStreetMap** (© colaboradores do OpenStreetMap, licença
[ODbL](https://www.openstreetmap.org/copyright)), usados com os nomes fictícios da API. Os dados de bairros, ruas, réguas e chamadas são todos simulados.

## Módulo de Cybersecurity (CitizenTech)

Um acréscimo à API, no projeto [`src/Elo.Seguranca`](src/Elo.Seguranca): nenhum contrato do app nem do painel mudou.
Ele protege o que as pessoas digitam sem querer na observação (CPF, RG, senha, doença, nome) em quatro passos:

1. **Parsing de PII.** Um middleware lê o JSON de entrada (POST, PUT e PATCH, e também a query string) e acha CPF, RG, cartão do SUS, e-mail, telefone, senha, dado de saúde e nome
   com regex e validação do dígito verificador. Reconhece também pelo nome da chave: um `"documento"` ou uma `"senha"` que a API nem conhece é marcado.
   O que é achado vira **máscara** antes de ir para o banco ou para o log (`123.456.789-00` vira `***.456.***-00`).
2. **Taint analysis.** O dado marcado na entrada é seguido até as saídas (banco, log, resposta HTTP e LLM). Uma camada, a `GuardaDeSaida`, fica antes de cada saída e troca o valor marcado pela máscara,
   mesmo quando ele não tem cara de dado sensível. Também varre o que já estava no banco: dado antigo, gravado por fora da API, sai mascarado na resposta.
   Cada bloqueio vira um **evento de segurança** (só tipo, quantidade e lugar, nunca o dado).
3. **Tradução por IA.** Em segundo plano, cada evento ganha uma mensagem de duas frases para um cidadão leigo de 60 anos. O Claude (Anthropic) escreve a mensagem a partir de um prompt que só tem metadados
   (e que também passa pela guarda). Sem chave, desligado, com erro, recusa ou timeout, entram as mensagens prontas do `TradutorPadrao`. **Aviso de chuva nunca passa por LLM.**
4. **Painel Risco Cidadão.** Uma página com farol verde, amarelo ou vermelho (sempre com texto, não só cor), índice de 0 a 100, a frase ao cidadão e, lado a lado, a falha técnica e o que o cidadão lê.

**Farol e índice.** Verde: nenhum evento nas últimas 24 horas. Amarelo: só chegou dado sensível (foi marcado, nada tentou sair). Vermelho: pelo menos uma saída foi bloqueada.
O índice é a soma dos pontos de cada evento vezes 4, no máximo 100. Pontos = peso da severidade (média 2, alta 3; telefone e e-mail são médios, o resto é alto) x peso da etapa (entrada 1, saída 3).

**Painel.** Com a API no ar, abra http://localhost:5080/risco-cidadao.html (atualiza a cada 3 segundos). Os mesmos dados saem em `GET /api/seguranca/painel` e `GET /api/seguranca/eventos`,
documentados no Swagger e no Scalar. A página segue o visual do app e do painel de administração: as mesmas cores, as fontes Manrope e Space Grotesk (servidas pela própria API,
em `wwwroot/fontes`, sem CDN), seções sem caixa e a situação sempre escrita. Tem tema claro e escuro (segue o aparelho, com botão para trocar; `?tema=claro` ou `?tema=escuro` na URL força um deles).

**Demonstração.** O roteiro para gravar o vídeo está em [`docs/cyber/demo.sh`](docs/cyber/demo.sh): com a API no ar, rode `docs/cyber/demo.sh` em outro terminal.
Ele envia um "Preciso de ajuda" com CPF, RG, senha, doença e nome; um JSON com as chaves `documento` e `senha` (que a API nem conhece); e um dado antigo gravado direto no banco.
Entre os passos, mostra o painel. A saída de uma execução real, com o Claude ligado, está em [`docs/evidencias/cyber-demo.txt`](docs/evidencias/cyber-demo.txt).
Na mesma execução, o log da API saiu assim (a observação original tinha "Sra. Maria Souza, CPF 123.456.789-00, RG 12.345.678-9, senha: abc12345. Ela é diabética"):

```text
warn: Elo.Application.ContratoDoApp.ContratoDoAppService[0]
      Pedido de ajuda na rua 6 (chamada 2): Sra. [nome oculto], CPF ***.456.***-00, RG [RG oculto], senha: [senha oculta]. Ela é [dado de saúde oculto] e está sozinha.
```

Os CPFs e RGs da demonstração são inventados.

**Como ligar o Claude.** O tradutor usa o SDK oficial da Anthropic para C# (pacote `Anthropic`), com o modelo `claude-opus-5-5`, esforço baixo (são duas frases)
e o fallback do servidor ligado (se o modelo recusar, outro modelo responde na mesma chamada). A chave é da API da Anthropic (criada em platform.claude.com, cobrada por uso,
separada da assinatura do Claude) e **não fica no repositório**: a seção `Ia` do `appsettings.json` vem com `ChaveApi` vazia. Use uma das duas formas:

```bash
dotnet user-secrets set "Ia:Claude:ChaveApi" "<sua-chave>" --project src/Elo.Api
```

```bash
Ia__Claude__ChaveApi=<sua-chave> dotnet run --project src/Elo.Api
```

Sem chave, a API usa as mensagens prontas e o painel funciona igual. Outras opções: `Ia:Claude:Modelo` (padrão `claude-opus-5-5`; `claude-haiku-5-5` sai bem mais barato),
`Ia:TempoLimiteEmSegundos` (padrão 20) e `Ia:Habilitada` (`false` desliga o LLM; os testes usam assim, então nenhum teste acessa a rede).
Cada falha igual (mesma etapa, saída e tipos) chama o LLM uma vez só: a tradução fica guardada em memória.

**Gemini como alternativa.** Com `Ia:Provedor` = `Gemini` e a chave em `Ia:Gemini:ChaveApi` (modelo em `Ia:Gemini:Modelo`, padrão `gemini-2.5-flash`), o Gemini faz o mesmo papel.
Os dois implementam a interface `IProvedorDeIa`, então o worker não muda.

**Limites conhecidos.**

- Os eventos ficam **em memória** (os últimos 500) e somem quando a API reinicia: o painel volta a verde. Foi de propósito, para não criar tabela nem migration.
- O **contato da liderança é isento de propósito**: `whatsapp`, `linkWhatsapp` e `apelido` são informados para a coordenação poder chamar. É um risco aceito. Telefone e e-mail também não são barrados nas respostas HTTP, a não ser que tenham chegado marcados na mesma requisição (avisos do INMET e contatos institucionais têm telefone).
- Os **escopos de log não passam pela guarda** (`BeginScope`): só a mensagem e a exceção são limpas.
- A API não tem login, então **toda resposta é tratada como não autenticada**.
- O motor reconhece uns 40 termos de saúde e nomes só em frases como "meu nome é" e "Sra."; texto livre fora desses padrões pode escapar. Não é um DLP completo.

## Arquitetura

Arquitetura em camadas: cada projeto só conhece o de dentro, e a Aplicação depende de **interfaces**, nunca do EF Core.

```mermaid
flowchart TB
    API["Elo.Api<br/>controllers REST, chave do ESP32, tratamento de erros, OpenAPI, página do bairro"]
    APP["Elo.Application<br/>serviços, DTOs, interfaces dos repositórios e o contrato do app"]
    INFRA["Elo.Infrastructure<br/>EF Core + SQLite, repositórios, migrations, serviço em segundo plano"]
    DOM["Elo.Domain<br/>entidades, value objects, regras e a ordem do painel"]
    TESTES["Elo.Tests<br/>unitários e de integração"]
    API --> APP
    API --> INFRA
    INFRA --> APP
    APP --> DOM
    TESTES -.-> API
```

```
elo-api-csharp/
├── Elo.sln
├── src/
│   ├── Elo.Domain/          Bairros, Ruas, Chamadas, Sensores, Painel, Comum
│   ├── Elo.Application/     Abstracoes (interfaces), ContratoDoApp, Avisos, serviços e DTOs por assunto
│   ├── Elo.Infrastructure/  Persistencia (DbContext, Configuracoes, Migrations), Repositorios, Avisos (INMET), Servicos,
│   │                        Seguranca (tradutores Claude e Gemini, worker, interceptor do EF Core)
│   ├── Elo.Seguranca/       Pii, Taint, Eventos, Traducao, Risco (módulo de Cybersecurity, sem ASP.NET)
│   └── Elo.Api/             Controllers, Configuracao, Seguranca (middlewares e log protegido), wwwroot (páginas e fontes), Elo.Api.http
├── tests/Elo.Tests/         Unitarios/Dominio, Unitarios/Aplicacao, Unitarios/Seguranca, Integracao
└── docs/                    contrato-api.md, cyber/demo.sh e evidencias/
```

**Contrato do app como camada anticorrupção.** O app fala outra língua: para ele, "chamada" é o que a liderança responde,
o status é `preciso_ajuda` e o id da rua é texto. O [`ContratoDoAppService`](src/Elo.Application/ContratoDoApp/ContratoDoAppService.cs)
traduz isso para o domínio (a resposta entra na `Chamada` aberta do bairro), sem duplicar regra: tudo passa pelos mesmos
métodos de `Chamada`, `Resposta` e `Sensor`. O painel e o `/situacao` usam o mesmo [`MontadorDeSituacoes`](src/Elo.Application/Painel/MontadorDeSituacoes.cs).

**Avisos do INMET (padrão Decorator).** A Aplicação só conhece a interface `IFonteDeAvisos`. Na Infraestrutura, duas
classes implementam essa interface: o [`ClienteDoInmet`](src/Elo.Infrastructure/Avisos/ClienteDoInmet.cs) vai direto ao INMET
(o [`LeitorDeAvisosDoInmet`](src/Elo.Infrastructure/Avisos/LeitorDeAvisosDoInmet.cs) aplica as mesmas regras do `inmet.ts` do app)
e a [`FonteDeAvisosEmCache`](src/Elo.Infrastructure/Avisos/FonteDeAvisosEmCache.cs) embrulha o cliente e acrescenta o cache,
guardando a última lista boa. Quem pede `IFonteDeAvisos` recebe o cache sem saber disso. O
[`AtualizadorDeAvisosWorker`](src/Elo.Infrastructure/Avisos/AtualizadorDeAvisosWorker.cs) atualiza o cache a cada 5 minutos
pela interface `ICacheDeAvisos`, e o [`AvisoService`](src/Elo.Application/Avisos/AvisoService.cs) filtra pelo município com LINQ.

Modelo de domínio:

```mermaid
classDiagram
    class Entidade {
        <<abstract>>
        +int Id
    }
    class Chamada {
        <<abstract>>
        +TipoChamada Tipo
        +string Pergunta
        +MontarMensagemParaRua(nomeDaRua)*
        +RegistrarResposta(...)
        +RegistrarAcao(...)
        +Encerrar(agora, ruas)
    }
    Entidade <|-- Bairro
    Entidade <|-- Rua
    Entidade <|-- Chamada
    Entidade <|-- Resposta
    Entidade <|-- AcaoCoordenador
    Entidade <|-- Sensor
    Entidade <|-- LeituraSensor
    Chamada <|-- ChamadaReal
    Chamada <|-- ChamadaTreino
    Bairro "1" --> "*" Rua
    Rua "1" --> "*" Sensor
    Sensor "1" --> "*" LeituraSensor
    Chamada "1" --> "*" Resposta
    Chamada "1" --> "*" AcaoCoordenador
    ChamadaReal --> AvisoOficial
    Chamada --> IndicadorDaChamada
    Rua --> Lideranca
    Rua --> PlanoDaRua
```

## Onde cada critério da avaliação aparece

### POO (3,0)

| Conceito | Onde |
|---|---|
| **Abstração e herança** | [`Entidade`](src/Elo.Domain/Comum/Entidade.cs) é a base de todas as entidades. [`Chamada`](src/Elo.Domain/Chamadas/Chamada.cs) é abstrata, com as subclasses [`ChamadaReal`](src/Elo.Domain/Chamadas/ChamadaReal.cs) e [`ChamadaTreino`](src/Elo.Domain/Chamadas/ChamadaTreino.cs) |
| **Polimorfismo** | `Tipo`, `Pergunta`, `MontarMensagemParaRua` e `DescreverAbertura` são abstratos, e `AvisoQueMotivou` e `DuracaoPrevistaEmMinutos` são virtuais: cada chamada responde do seu jeito (o treino avisa "TREINO" para ninguém confundir com alerta real). O painel, o link do WhatsApp, o registro e o DTO usam isso sem saber o tipo, sem `is` nem `as` |
| **Encapsulamento** | Setters privados, coleções expostas só para leitura (`IReadOnlyCollection` sobre lista privada), construtor `internal` de [`Resposta`](src/Elo.Domain/Chamadas/Resposta.cs): só a chamada cria respostas, depois de validar janela de tempo e bairro |
| **Value objects** | [`Lideranca`](src/Elo.Domain/Ruas/Lideranca.cs), [`PlanoDaRua`](src/Elo.Domain/Ruas/PlanoDaRua.cs), [`AvisoOficial`](src/Elo.Domain/Chamadas/AvisoOficial.cs) e [`IndicadorDaChamada`](src/Elo.Domain/Chamadas/IndicadorDaChamada.cs) são `record`s imutáveis, comparados por valor. No `PlanoDaRua`, a lista de itens é só leitura e entra na comparação (`Equals` e `GetHashCode` escritos à mão, porque o `record` compararia a lista por referência) |
| **Interfaces** | Repositórios ([`IRepository<T>`](src/Elo.Application/Abstracoes/IRepository.cs) genérico e os específicos), `IUnitOfWork`, uma interface por serviço, inclusive `IContratoDoAppService`, e `IFonteDeAvisos`, implementada pelo cliente do INMET e pelo cache que o embrulha (Decorator) |
| **Generics** | `IRepository<T> where T : Entidade`, [`RepositoryBase<T>`](src/Elo.Infrastructure/Repositorios/RepositoryBase.cs), `PaginaResultado<T>`, `ResultadoDeRegistro<T>` |

### Web API (2,5)

- Controllers REST em [`src/Elo.Api/Controllers`](src/Elo.Api/Controllers) com `[ApiController]`, rotas por recurso e sub-recurso, verbos corretos e códigos 200, 201 (com `Location` apontando para o recurso criado), 204, 400, 401, 404, 409 e 422.
- **Rotas do contrato do app** ([`ContratoDoAppController`](src/Elo.Api/Controllers/ContratoDoAppController.cs)) no formato que o `elo-app` e o ESP32 já usam.
- DTOs de entrada e saída separados das entidades (a API nunca expõe entidade), com validação por DataAnnotations e mensagens em português.
- Erros padronizados em ProblemDetails (RFC 9457) pelo [`TratadorGlobalDeExcecoes`](src/Elo.Api/Configuracao/TratadorGlobalDeExcecoes.cs): os controllers não têm `try/catch`.
- Filtro de autorização [`ExigeChaveDeDispositivo`](src/Elo.Api/Configuracao/ChaveDeDispositivo.cs): confere o header do ESP32 em tempo constante, antes de qualquer outra coisa.
- Documentação OpenAPI 3.1 gerada pelo ASP.NET Core, com os comentários `///` virando descrição, exibida no **Swagger UI** e no **Scalar** ([`DocumentacaoDaApi`](src/Elo.Api/Configuracao/DocumentacaoDaApi.cs)).
- Paginação, filtros por query string, idempotência no reenvio offline, health check e CORS para o app.

### EF Core + LINQ (1,5)

- [`EloDbContext`](src/Elo.Infrastructure/Persistencia/EloDbContext.cs) com Fluent API em classes `IEntityTypeConfiguration` ([`Configuracoes`](src/Elo.Infrastructure/Persistencia/Configuracoes)).
- **Herança TPH**: `ChamadaReal` e `ChamadaTreino` na tabela `Chamadas`, com a coluna `Discriminador`.
- **Owned types** para os value objects, **coleção primitiva** (lista do plano salva em JSON), enums gravados como texto, conversor de datas para UTC.
- **Índice único filtrado** que garante no banco uma chamada aberta por bairro, e índice único no id local das respostas.
- [Migrations](src/Elo.Infrastructure/Persistencia/Migrations) aplicadas sozinhas na inicialização. A segunda registra a adaptação ao contrato do app (id local em texto e observação de até 200 caracteres) e não gera SQL: no SQLite essas colunas já eram `TEXT` e o tamanho máximo não é aplicado pelo banco, então só o modelo mudou.
- LINQ em vários níveis:
  - [`PriorizadorDeRuas`](src/Elo.Domain/Painel/PriorizadorDeRuas.cs): `Where`, `OrderBy`/`ThenBy`/`ThenByDescending` e concatenação: **é o LINQ que ordena as ruas em silêncio**;
  - [`IndicadorDaChamada`](src/Elo.Domain/Chamadas/IndicadorDaChamada.cs): `GroupBy`, `Min`, `Any`, `Count`, `Average`;
  - [`SensorRepository`](src/Elo.Infrastructure/Repositorios/SensorRepository.cs): `SelectMany`, subconsulta com `OrderByDescending().FirstOrDefault()`, `MaxBy` e **query syntax com dois `join`**;
  - [`BairroRepository`](src/Elo.Infrastructure/Repositorios/BairroRepository.cs): projeção direto para DTO com subconsultas;
  - [`ChamadaRepository`](src/Elo.Infrastructure/Repositorios/ChamadaRepository.cs): filtros montados aos poucos, filtro por subclasse (`is ChamadaReal`), a janela da chamada traduzida para SQL, `Skip`/`Take`, `ExecuteDeleteAsync`;
  - [`LinhaDoTempo`](src/Elo.Application/Chamadas/LinhaDoTempo.cs): `Concat` de eventos de origens diferentes, `MinBy`, `OrderBy`;
  - [`MontadorDeSituacoes`](src/Elo.Application/Painel/MontadorDeSituacoes.cs) e [`PainelService`](src/Elo.Application/Painel/PainelService.cs): `ToLookup`, `Sum`, `GroupBy` para a média por tipo;
  - [`AvisoService`](src/Elo.Application/Avisos/AvisoService.cs): `Where`, `DistinctBy`, `OrderByDescending`/`ThenBy` sobre os avisos do INMET.

### Programação assíncrona (1,0)

- `async`/`await` de ponta a ponta: controller → serviço → repositório → EF Core (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`, `ExecuteDeleteAsync`), com `CancellationToken` repassado em todas as camadas.
- [`RetencaoWorker`](src/Elo.Infrastructure/Servicos/RetencaoWorker.cs): `BackgroundService` com `PeriodicTimer` que roda a [rotina de privacidade](src/Elo.Application/Retencao/RetencaoService.cs) sem travar a API, criando um escopo de DI por execução.
- Inicialização assíncrona do banco (`MigrateAsync` e carga dos dados simulados) antes de a API aceitar requisições.
- Avisos do INMET: `HttpClient` criado pelo `IHttpClientFactory`, resposta lida em fluxo (`JsonDocument.ParseAsync`), um
  `SemaphoreSlim` para só uma requisição baixar por vez e o [`AtualizadorDeAvisosWorker`](src/Elo.Infrastructure/Avisos/AtualizadorDeAvisosWorker.cs)
  (outro `BackgroundService` com `PeriodicTimer`), para o `GET /avisos` responder na hora sem esperar o INMET.

### Qualidade do código (2,0)

- Camadas com dependências apontando para dentro; injeção de dependência em tudo (inclusive o relógio, via `TimeProvider`, para testar o tempo).
- Regras de negócio no domínio, validação de entrada na borda, camada anticorrupção para o contrato do app, erros padronizados, logs estruturados **sem dado sensível** (a observação só chega ao log mascarada pela guarda do [módulo de Cybersecurity](#módulo-de-cybersecurity-citizentech)).
- Build sem nenhum aviso. Nomes do domínio em português, iguais aos da proposta (chamada, rua em silêncio, "Vou até lá").
- **476 testes automatizados** ([`tests/Elo.Tests`](tests/Elo.Tests)), nenhum depende de internet: 248 da API e 228 do [módulo de Cybersecurity](#módulo-de-cybersecurity-citizentech). Os 248 da API são os 154 abaixo mais 94 do painel de administração e dos campos novos do contrato: validações de UF, IBGE, coordenadas, traçado e nível de atenção, o serviço do instantâneo, as rotas com os dados do piloto e com os 12 bairros, e a espera do cache do INMET depois de falha:
  - 54 unitários do domínio (polimorfismo, janela de tempo, offline, indicador, ordem do painel, mascaramento, value objects);
  - 36 unitários da aplicação: serviços com **Moq** e relógio falso (`FakeTimeProvider`), a tradução para o formato do app e o filtro de avisos;
  - 19 unitários da infraestrutura: a leitura da resposta do INMET (com o mesmo fixture e os mesmos casos de borda dos testes do app), o cache e a ligação do Decorator na injeção de dependência;
  - 45 de integração com a API inteira (`WebApplicationFactory`) e um banco SQLite temporário por classe, incluindo as rotas do contrato, a chave do ESP32, o `/avisos` com um INMET falso e o `Location` de cada 201.
  - 228 do módulo de Cybersecurity: o motor de PII (cada tipo, positivo e negativo, e os falsos positivos como "Rua Dona Ana" e GUID), a guarda de saída, o registro de eventos,
    o cálculo do farol e do índice, as mensagens prontas, os tradutores do Claude e do Gemini com um servidor falso (sem rede) e 14 de integração: o dado sensível não chega ao banco,
    ao log nem à resposta, o dado antigo sai mascarado, os contratos do app e do painel não mudam e o painel fica verde, amarelo e vermelho.

## Regras de negócio

| Regra | Onde |
|---|---|
| Chamada real só existe com aviso oficial (o Elo não cria alerta próprio) | `ChamadaReal` |
| Treino dura de 5 a 180 minutos (padrão 30) e termina sozinho | `ChamadaTreino` |
| Só uma chamada aberta por bairro (no serviço e no banco) | `ChamadaService` + índice filtrado |
| A quantidade de pessoas é opcional (o contrato do app permite nulo); quando vem, vai de 1 a 500 | `Resposta` |
| Observação de até 200 caracteres, com CPF, telefone e e-mail mascarados | `Resposta`, `FiltroDeDadosPessoais` |
| Resposta vale se foi **dada** dentro da janela da chamada, mesmo chegando depois (offline) | `Chamada.AceitaRespostaDadaEm` |
| Resposta enviada pelo app entra na chamada do bairro aberta na hora do `criadaEm`; sem chamada, 422 | `ContratoDoAppService` |
| Mesmo id local reenviado devolve a resposta salva (200), sem duplicar | `RespostaService`, `ContratoDoAppService` + índice único |
| A resposta mais recente da rua é a que vale no painel | `SituacaoDaRua` |
| "Vou até lá" só conta se foi marcado depois da última resposta da rua | `SituacaoDaRua` |
| Ordem do painel: ajuda (sem ninguém a caminho, depois quem espera há mais tempo) → silêncio (régua acima do limite, depois risco da rua) → água subindo → tudo bem | `PriorizadorDeRuas` |
| Indicador conta a hora em que a resposta **chegou**, não a hora do toque | `IndicadorDaChamada` |
| Leitura da régua no limite ou acima vira alerta; guarda o limite que valia na hora | `Sensor`, `LeituraSensor` |
| O ESP32 só grava leitura com a chave do dispositivo | `ExigeChaveDeDispositivo` |
| Aviso do INMET vale para o município só pelo código IBGE inteiro (nunca por prefixo); encerrados saem; o mesmo aviso não se repete em "hoje" e "futuro"; ordem por gravidade e início | `AvisoDoInmet`, `AvisoService` |
| Com o INMET fora do ar, vale a última lista baixada; sem nenhuma, 503 | `FonteDeAvisosEmCache` |
| Respostas com mais de 30 dias são apagadas; o indicador fica guardado na chamada | `RetencaoService` |
| Rua com histórico não é excluída (é desativada); bairro com chamadas não é excluído | `RuaService`, `BairroService` |

## Evidências de funcionamento

- [`docs/evidencias/fluxo-completo.md`](docs/evidencias/fluxo-completo.md): 27 requisições reais feitas contra a API rodando, em quatro partes: a API completa (painel antes e depois, reenvio offline, erros 400/404/409/422), as **rotas do contrato do app e do ESP32** (`/situacao`, `/chamadas`, `/leituras` com 401 sem chave e 204 com chave, `/regua`, `/ruas/{id}/plano`), o fim da chamada com o indicador e os **avisos reais do INMET** (148.353 bytes do INMET viraram 2.333 bytes para o município).
- [`docs/evidencias/resultado-dos-testes.txt`](docs/evidencias/resultado-dos-testes.txt): os 154 testes aprovados.
- [`docs/evidencias/cyber-demo.txt`](docs/evidencias/cyber-demo.txt): a saída do roteiro de demonstração do módulo de Cybersecurity, com as mensagens escritas pelo Claude.

**Página do resumo do bairro** (http://elo-api-fiap.azurewebsites.net): a chamada aberta, os números da chamada, as ruas em ordem de prioridade e a régua do bairro com o limite e o nível de atenção, com as fontes, as cores e o logo do app e do painel:

![Resumo do bairro](docs/evidencias/01-resumo-do-bairro.png)

**Swagger UI:**

![Swagger](docs/evidencias/02-swagger.png)

**Scalar:**

![Scalar](docs/evidencias/03-scalar.png)

**Painel Risco Cidadão** (módulo de Cybersecurity), depois da demonstração com o Claude ligado: farol vermelho, índice 100 e, em cada evento, a falha técnica ao lado do que o cidadão lê:

![Risco Cidadão](docs/evidencias/04-risco-cidadao.png)

No celular, no tema escuro:

<img src="docs/evidencias/05-risco-cidadao-celular.png" alt="Risco Cidadão no celular, tema escuro" width="320">

## Decisões e limitações

- **SQLite** para quem avaliar rodar sem instalar banco. Trocar para Oracle ou SQL Server muda só o provedor no `AddInfrastructure` e as migrations.
- **Sem login de usuário** nesta versão, como no contrato do app ("o app não faz login"). O ESP32 usa uma chave; hoje é **uma chave para todos os dispositivos**. O próximo passo seria uma chave por régua e login do coordenador.
- **Datas em UTC** na API; o app converte para o horário local.
- O `GET /avisos` já filtra o INMET no servidor, como o contrato recomenda. O app passa a usar essa lista com a API configurada (veja [Ligar o app e a régua](#ligar-o-app-e-a-régua-esp32)).
- Sem internet no servidor, o `/avisos` devolve 503; o resto da API funciona normalmente. O INMET não publica um contrato dessa API, então o leitor aceita as variações já vistas (listas em texto do Python, `encerrado` como texto).
- O `bateriaPercentual` do ESP32 é aceito, mas não é guardado (a régua simulada não tem bateria).
- Sem chamada aberta, `POST /chamadas` devolve 422 e o app deixa a resposta pendente para tentar de novo. Abrir a chamada é papel do coordenador (`/api/bairros/{id}/chamadas`).
- **Os PUT trocam o registro inteiro.** Em `PUT /api/bairros/{id}`, deixar de fora `uf`, `codigoIbge`, as coordenadas ou o `percentualSemApp` apaga esses campos; em `PUT /api/sensores/{id}`, deixar de fora `atencaoCm` apaga o nível de atenção. Quem edita manda o registro todo, como ele veio no GET.
- **Posição de rua e de régua só pela semente.** Não há rota para gravar a coordenada ou o traçado da rua, nem a posição da régua. Um `elo.db` criado antes da migração `PainelAdmin` fica sem mapa até ser recriado (apagar o arquivo recria tudo com os dados de exemplo, e as leituras reais se perdem).
- O mascaramento do domínio (`FiltroDeDadosPessoais`) acha CPF, telefone e e-mail na observação. Nomes, doenças, RG, cartão do SUS e senhas são tratados pelo [módulo de Cybersecurity](#módulo-de-cybersecurity-citizentech), que também mascara o log, o banco e as respostas.

## Uso de IA

A API foi feita com ajuda de IA (Claude Code). Os prompts, as perguntas feitas e o que precisa ser revisado pela equipe estão em [`prompts.md`](prompts.md).

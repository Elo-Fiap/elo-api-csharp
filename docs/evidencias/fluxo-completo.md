# Evidências: fluxo completo da API

Requisições feitas de verdade contra a API rodando localmente (`dotnet run --project src/Elo.Api`), em 10/11/2026 01:41 UTC, a partir do banco recém-criado com os dados simulados. Os ids e horários vêm dessa execução.

A primeira parte usa as rotas da API completa (`/api/...`). A segunda usa as **rotas do contrato do app** (sem `/api`, como no `docs/contrato-api.md` do repositório elo-app), do jeito que o app e o ESP32 chamam. O mesmo roteiro está em [`src/Elo.Api/Elo.Api.http`](../../src/Elo.Api/Elo.Api.http).

## Parte 1: API completa (`/api`)

### 1. A API está no ar

Health check usado pela VM Linux (Sistemas Operacionais) para monitorar o serviço.

```http
GET /health
```

Resposta: **HTTP 200**

```
Healthy
```

### 2. Bairros cadastrados

O bairro simulado tem 8 ruas e a chamada real aberta (id 2).

```http
GET /api/bairros
```

Resposta: **HTTP 200**

```json
[
  {
    "id": 1,
    "nome": "Jardim Pantanal (simulado)",
    "cidade": "São Paulo",
    "subprefeitura": "São Miguel Paulista",
    "dadosSimulados": true,
    "totalDeRuasAtivas": 8,
    "chamadaAbertaId": 2
  }
]
```

### 3. Painel do coordenador (tela Bairro)

A ordem vem do LINQ do `PriorizadorDeRuas`: primeiro quem pediu ajuda (sem ninguém a caminho na frente), depois quem está em silêncio (a régua acima do limite na frente), depois água subindo e tudo bem. Resposta resumida aqui; o JSON completo tem também régua, observação e o link do WhatsApp de cada rua.

```http
GET /api/bairros/1/painel
```

Resposta: **HTTP 200**

```json
{
  "chamada": {
    "id": 2,
    "tipo": "Real",
    "status": "Aberta",
    "pergunta": "Aviso INMET de Acumulado de Chuva (perigo potencial). Como está a sua rua agora?"
  },
  "resumo": {
    "precisamDeAjuda": 2,
    "emSilencio": 3,
    "aguaSubindo": 1,
    "tudoBem": 2,
    "pessoasPrecisandoDeAjuda": 8
  },
  "indicador": {
    "totalDeRuas": 8,
    "ruasComResposta": 5,
    "ruasEmSilencio": 3,
    "ruasConhecidasEm15Min": 5,
    "percentualConhecidoEm15Min": 62.5,
    "percentualComResposta": 62.5,
    "ruasQuePediramAjuda": 2,
    "minutosAtePrimeiraRespostaEmMedia": 7.4
  },
  "ruas (ordem do painel)": [
    "1. Rua do Aguapé: PrecisaDeAjuda",
    "2. Rua das Garças: PrecisaDeAjuda (alguém a caminho)",
    "3. Rua das Palafitas: EmSilencio | régua 1.42 m, acima do limite",
    "4. Rua dos Barqueiros: EmSilencio",
    "5. Rua do Córrego Seco: EmSilencio",
    "6. Rua da Várzea: AguaSubindo",
    "7. Rua Ipê Amarelo: TudoBem",
    "8. Travessa da Taboa: TudoBem"
  ]
}
```

### 4. Uma rua pede ajuda (um toque)

A Rua do Córrego Seco estava em silêncio e agora pede ajuda. CPF e telefone da observação são mascarados antes de salvar.

```http
POST /api/chamadas/2/respostas
Content-Type: application/json

{
  "idLocal": "6e20fd7f-02a5-48da-ad94-56d0192330f2",
  "ruaId": 6,
  "situacao": "PrecisoDeAjuda",
  "pessoas": 4,
  "observacao": "Família ilhada. Liguem 11 91234-5678, CPF 123.456.789-09"
}
```

Resposta: **HTTP 201** (Location: `http://localhost:5080/api/chamadas/2/respostas/14`)

```json
{
  "id": 14,
  "idLocal": "6e20fd7f-02a5-48da-ad94-56d0192330f2",
  "chamadaId": 2,
  "ruaId": 6,
  "situacao": "PrecisoDeAjuda",
  "pessoas": 4,
  "observacao": "Família ilhada. Liguem [telefone removido], CPF [CPF removido]",
  "canal": "App",
  "respondidaEm": "2026-11-10T01:41:58.906283Z",
  "recebidaEm": "2026-11-10T01:41:58.906283Z"
}
```

### 5. O celular reenvia a mesma resposta (internet voltou)

Mesmo `idLocal`: a API devolve **200** com a resposta já salva, sem duplicar.

```http
POST /api/chamadas/2/respostas
Content-Type: application/json

{
  "idLocal": "6e20fd7f-02a5-48da-ad94-56d0192330f2",
  "ruaId": 6,
  "situacao": "PrecisoDeAjuda",
  "pessoas": 4,
  "observacao": "Família ilhada. Liguem 11 91234-5678, CPF 123.456.789-09"
}
```

Resposta: **HTTP 200**

```json
{
  "id": 14,
  "idLocal": "6e20fd7f-02a5-48da-ad94-56d0192330f2",
  "chamadaId": 2,
  "ruaId": 6,
  "situacao": "PrecisoDeAjuda",
  "pessoas": 4,
  "observacao": "Família ilhada. Liguem [telefone removido], CPF [CPF removido]",
  "canal": "App",
  "respondidaEm": "2026-11-10T01:41:58.906283Z",
  "recebidaEm": "2026-11-10T01:41:58.906283Z"
}
```

### 6. Regra de negócio: resposta fora do horário da chamada

Uma resposta "dada" 3 horas atrás não pertence à chamada aberta há 25 minutos: o domínio recusa e a API devolve **422** em ProblemDetails.

```http
POST /api/chamadas/2/respostas
Content-Type: application/json

{
  "ruaId": 4,
  "situacao": "TudoBem",
  "respondidaEm": "2026-11-09T22:41:58.000Z"
}
```

Resposta: **HTTP 422**

```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "A resposta foi dada fora do horário desta chamada.",
  "instance": "/api/chamadas/2/respostas",
  "traceId": "00-52e06647204b65ade6dc74c62abe89ef-c4ca343574635395-00"
}
```

### 7. Validação do corpo: falta a rua

Validação de entrada (DataAnnotations): **400** com o campo que faltou.

```http
POST /api/chamadas/2/respostas
Content-Type: application/json

{
  "situacao": "TudoBem"
}
```

Resposta: **HTTP 400**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "RuaId": [
      "O campo RuaId é obrigatório."
    ]
  },
  "traceId": "00-81df684b72b920c578b8fc761fc7be12-cf14971445a3dcd8-00"
}
```

### 8. O coordenador marca "Vou até lá" na Rua das Palafitas

Ação registrada; a rua desce na fila do painel porque já tem alguém indo.

```http
POST /api/chamadas/2/acoes
Content-Type: application/json

{
  "ruaId": 7,
  "tipo": "VouAteLa"
}
```

Resposta: **HTTP 201** (Location: `http://localhost:5080/api/chamadas/2/acoes`)

```json
{
  "id": 3,
  "chamadaId": 2,
  "ruaId": 7,
  "tipo": "VouAteLa",
  "registradaEm": "2026-11-10T01:41:58.9812477Z"
}
```

### 9. A régua da Rua dos Barqueiros manda uma leitura (rota completa, em cm)

160 cm com limite de 150 cm: a leitura entra como alerta.

```http
POST /api/sensores/2/leituras
Content-Type: application/json

{
  "nivelCm": 160
}
```

Resposta: **HTTP 201** (Location: `http://localhost:5080/api/sensores/2/leituras`)

```json
{
  "id": 13,
  "sensorId": 2,
  "nivelCm": 160,
  "limiteCm": 150,
  "acimaDoLimite": true,
  "medidaEm": "2026-11-10T01:41:59.0054388Z",
  "recebidaEm": "2026-11-10T01:41:59.0054388Z"
}
```

### 10. Painel depois das mudanças

Córrego Seco subiu para os pedidos de ajuda; Palafitas desceu entre as ruas em silêncio (alguém a caminho); Barqueiros passou a ter a régua acima do limite.

```http
GET /api/bairros/1/painel
```

Resposta: **HTTP 200**

```json
{
  "chamada": {
    "id": 2,
    "tipo": "Real",
    "status": "Aberta",
    "pergunta": "Aviso INMET de Acumulado de Chuva (perigo potencial). Como está a sua rua agora?"
  },
  "resumo": {
    "precisamDeAjuda": 3,
    "emSilencio": 2,
    "aguaSubindo": 1,
    "tudoBem": 2,
    "pessoasPrecisandoDeAjuda": 12
  },
  "indicador": {
    "totalDeRuas": 8,
    "ruasComResposta": 6,
    "ruasEmSilencio": 2,
    "ruasConhecidasEm15Min": 5,
    "percentualConhecidoEm15Min": 62.5,
    "percentualComResposta": 75,
    "ruasQuePediramAjuda": 3,
    "minutosAtePrimeiraRespostaEmMedia": 10.3
  },
  "ruas (ordem do painel)": [
    "1. Rua do Aguapé: PrecisaDeAjuda",
    "2. Rua do Córrego Seco: PrecisaDeAjuda",
    "3. Rua das Garças: PrecisaDeAjuda (alguém a caminho)",
    "4. Rua dos Barqueiros: EmSilencio | régua 1.60 m, acima do limite",
    "5. Rua das Palafitas: EmSilencio (alguém a caminho) | régua 1.42 m, acima do limite",
    "6. Rua da Várzea: AguaSubindo",
    "7. Rua Ipê Amarelo: TudoBem",
    "8. Travessa da Taboa: TudoBem"
  ]
}
```

### 11. Registro da chamada (o que foi avisado e o que foi feito)

Linha do tempo montada com LINQ (Concat + OrderBy) juntando abertura, respostas, ações e alertas das réguas.

```http
GET /api/chamadas/2/registro
```

Resposta: **HTTP 200**

```json
[
  {
    "momento": "2026-11-10T01:16:53.7935871Z",
    "tipo": "ChamadaAberta",
    "ruaId": null,
    "rua": null,
    "descricao": "Chamada real aberta pelo aviso INMET: Acumulado de Chuva (perigo potencial)."
  },
  {
    "momento": "2026-11-10T01:20:53.7935871Z",
    "tipo": "Resposta",
    "ruaId": 3,
    "rua": "Travessa da Taboa",
    "descricao": "Tudo bem, pelo app."
  },
  {
    "momento": "2026-11-10T01:21:53.7935871Z",
    "tipo": "Resposta",
    "ruaId": 2,
    "rua": "Rua do Aguapé",
    "descricao": "Água subindo, pelo app."
  },
  {
    "momento": "2026-11-10T01:22:53.7935871Z",
    "tipo": "Resposta",
    "ruaId": 8,
    "rua": "Rua Ipê Amarelo",
    "descricao": "Tudo bem, pelo app."
  },
  {
    "momento": "2026-11-10T01:24:53.7935871Z",
    "tipo": "Resposta",
    "ruaId": 1,
    "rua": "Rua das Garças",
    "descricao": "Preciso de ajuda (3 pessoa(s)), pelo app."
  },
  {
    "momento": "2026-11-10T01:26:53.7935871Z",
    "tipo": "AcaoDoCoordenador",
    "ruaId": 1,
    "rua": "Rua das Garças",
    "descricao": "Coordenação marcou \"Vou até lá\"."
  },
  {
    "momento": "2026-11-10T01:28:53.7935871Z",
    "tipo": "Resposta",
    "ruaId": 5,
    "rua": "Rua da Várzea",
    "descricao": "Água subindo, por Sms."
  },
  {
    "momento": "2026-11-10T01:31:53.7935871Z",
    "tipo": "SensorAcimaDoLimite",
    "ruaId": 7,
    "rua": "Rua das Palafitas",
    "descricao": "Sensor regua-exemplo passou do limite de 120 cm (125 cm). Máximo no período: 142 cm."
  },
  {
    "momento": "2026-11-10T01:32:53.7935871Z",
    "tipo": "AcaoDoCoordenador",
    "ruaId": 4,
    "rua": "Rua dos Barqueiros",
    "descricao": "Coordenação chamou a rua no WhatsApp."
  },
  {
    "momento": "2026-11-10T01:34:53.7935871Z",
    "tipo": "Resposta",
    "ruaId": 2,
    "rua": "Rua do Aguapé",
    "descricao": "Preciso de ajuda (5 pessoa(s)), pelo app."
  },
  {
    "momento": "2026-11-10T01:41:58.906283Z",
    "tipo": "Resposta",
    "ruaId": 6,
    "rua": "Rua do Córrego Seco",
    "descricao": "Preciso de ajuda (4 pessoa(s)), pelo app."
  },
  {
    "momento": "2026-11-10T01:41:58.9812477Z",
    "tipo": "AcaoDoCoordenador",
    "ruaId": 7,
    "rua": "Rua das Palafitas",
    "descricao": "Coordenação marcou \"Vou até lá\"."
  },
  {
    "momento": "2026-11-10T01:41:59.0054388Z",
    "tipo": "SensorAcimaDoLimite",
    "ruaId": 4,
    "rua": "Rua dos Barqueiros",
    "descricao": "Sensor regua-barqueiros passou do limite de 150 cm (160 cm). Máximo no período: 160 cm."
  }
]
```

## Parte 2: rotas do contrato do app (`elo-app`) e do ESP32

### 12. GET /situacao (tela Bairro do app)

Status no vocabulário do app (`tudo_bem`, `agua_subindo`, `preciso_ajuda`; `null` = silêncio), ids em texto e datas no formato do `toISOString()`.

```http
GET /situacao
```

Resposta: **HTTP 200**

```json
{
  "bairro": "Jardim Pantanal (simulado)",
  "chamadaIniciadaEm": "2026-11-10T01:16:53.793Z",
  "ruas": [
    {
      "id": "2",
      "nome": "Rua do Aguapé",
      "status": "preciso_ajuda",
      "respondidaEm": "2026-11-10T01:34:53.793Z"
    },
    {
      "id": "6",
      "nome": "Rua do Córrego Seco",
      "status": "preciso_ajuda",
      "respondidaEm": "2026-11-10T01:41:58.906Z"
    },
    {
      "id": "1",
      "nome": "Rua das Garças",
      "status": "preciso_ajuda",
      "respondidaEm": "2026-11-10T01:24:53.793Z"
    },
    {
      "id": "4",
      "nome": "Rua dos Barqueiros",
      "status": null,
      "respondidaEm": null
    },
    {
      "id": "7",
      "nome": "Rua das Palafitas",
      "status": null,
      "respondidaEm": null
    },
    {
      "id": "5",
      "nome": "Rua da Várzea",
      "status": "agua_subindo",
      "respondidaEm": "2026-11-10T01:28:53.793Z"
    },
    {
      "id": "8",
      "nome": "Rua Ipê Amarelo",
      "status": "tudo_bem",
      "respondidaEm": "2026-11-10T01:22:53.793Z"
    },
    {
      "id": "3",
      "nome": "Travessa da Taboa",
      "status": "tudo_bem",
      "respondidaEm": "2026-11-10T01:20:53.793Z"
    }
  ]
}
```

### 13. POST /chamadas: a liderança da Rua Ipê Amarelo responde pelo app

No app, "chamada" é a resposta da rua. O `id` é criado no celular; a resposta entra na chamada aberta do bairro. A API devolve um corpo JSON (o app lê o corpo de todo 201).

```http
POST /chamadas
Content-Type: application/json

{
  "id": "m3k9x2a-7f4q1b2c",
  "ruaId": "8",
  "status": "agua_subindo",
  "pessoas": null,
  "observacao": "Água na calçada",
  "criadaEm": "2026-11-10T01:41:59.082Z"
}
```

Resposta: **HTTP 201**

```json
{
  "id": "m3k9x2a-7f4q1b2c",
  "ruaId": "8",
  "status": "agua_subindo",
  "pessoas": null,
  "observacao": "Água na calçada",
  "criadaEm": "2026-11-10T01:41:59.082Z",
  "recebidaEm": "2026-11-10T01:41:59.096Z"
}
```

### 14. O app reenvia a mesma chamada

Mesmo `id`: **200**, um registro só.

```http
POST /chamadas
Content-Type: application/json

{
  "id": "m3k9x2a-7f4q1b2c",
  "ruaId": "8",
  "status": "agua_subindo",
  "pessoas": null,
  "observacao": "Água na calçada",
  "criadaEm": "2026-11-10T01:41:59.082Z"
}
```

Resposta: **HTTP 200**

```json
{
  "id": "m3k9x2a-7f4q1b2c",
  "ruaId": "8",
  "status": "agua_subindo",
  "pessoas": null,
  "observacao": "Água na calçada",
  "criadaEm": "2026-11-10T01:41:59.082Z",
  "recebidaEm": "2026-11-10T01:41:59.096Z"
}
```

### 15. POST /leituras sem a chave do dispositivo

O ESP32 precisa mandar o header `X-Chave-Dispositivo`. Sem ele: **401**.

```http
POST /leituras
Content-Type: application/json

{
  "reguaId": "regua-exemplo",
  "nivelMetros": 1.48
}
```

Resposta: **HTTP 401**

```json
{
  "title": "Não autorizado",
  "status": 401,
  "detail": "Envie a chave do dispositivo no header X-Chave-Dispositivo.",
  "instance": "/leituras"
}
```

### 16. POST /leituras com a chave (o que a régua do Wokwi envia)

Mesmo corpo do `sketch.ino` da régua. Sucesso: **204**, sem corpo, como a régua espera. A chave desta execução foi configurada por variável de ambiente e está mascarada aqui.

```http
POST /leituras
X-Chave-Dispositivo: ********
Content-Type: application/json

{
  "reguaId": "regua-exemplo",
  "nivelMetros": 1.48
}
```

Resposta: **HTTP 204**, sem corpo

### 17. GET /regua

Última leitura da régua, em metros. O app mostra "acima do limite" porque `nivelMetros >= limiteMetros`.

```http
GET /regua
```

Resposta: **HTTP 200**

```json
{
  "reguaId": "regua-exemplo",
  "local": "Córrego do bairro, na ponte da Rua das Palafitas (simulado)",
  "nivelMetros": 1.48,
  "limiteMetros": 1.2,
  "medidaEm": "2026-11-10T01:41:59.140Z"
}
```

### 18. GET /ruas/1/plano

Plano da rua com os nomes de campo do app e os contatos de emergência.

```http
GET /ruas/1/plano
```

Resposta: **HTTP 200**

```json
{
  "ruaId": "1",
  "ruaNome": "Rua das Garças",
  "pontoEncontro": "Praça da caixa d'água (simulada)",
  "rotaSaida": "Subir pela Rua Ipê Amarelo, que é o ponto mais alto da microárea.",
  "abrigo": "Escola estadual do bairro (simulada)",
  "itensParaLevar": [
    "Documentos em saco plástico",
    "Remédios de uso contínuo",
    "Lanterna e pilhas",
    "Carregador de celular",
    "Água potável"
  ],
  "contatos": [
    {
      "nome": "Defesa Civil",
      "telefone": "199"
    },
    {
      "nome": "Bombeiros",
      "telefone": "193"
    }
  ]
}
```

### 19. GET /ruas/4/plano (rua sem plano)

**404**: o app mostra o estado vazio.

```http
GET /ruas/4/plano
```

Resposta: **HTTP 404**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Recurso não encontrado",
  "status": 404,
  "detail": "A rua 4 não tem plano cadastrado.",
  "instance": "/ruas/4/plano",
  "traceId": "00-9ad913925c506f8a949ee7fe65e5d028-010a59b071ee04cc-00"
}
```

## Parte 3: fim da chamada e indicador

### 20. Abrir outra chamada com uma já aberta

Só pode haver uma chamada aberta por bairro: **409**.

```http
POST /api/bairros/1/chamadas
Content-Type: application/json

{
  "tipo": "Treino"
}
```

Resposta: **HTTP 409**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflito",
  "status": 409,
  "detail": "O bairro já tem a chamada 2 aberta. Encerre-a antes de abrir outra.",
  "instance": "/api/bairros/1/chamadas",
  "traceId": "00-5c7ea550e05034328e0a71cb748549a0-11bf67dcddf67646-00"
}
```

### 21. Encerrar a chamada real

O encerramento guarda a foto do indicador (continua valendo depois que as respostas forem apagadas em 30 dias).

```http
POST /api/chamadas/2/encerramento
```

Resposta: **HTTP 200**

```json
{
  "id": 2,
  "bairroId": 1,
  "tipo": "Real",
  "status": "Encerrada",
  "pergunta": "Aviso INMET de Acumulado de Chuva (perigo potencial). Como está a sua rua agora?",
  "iniciadaEm": "2026-11-10T01:16:53.7935871Z",
  "encerradaEm": "2026-11-10T01:41:59.2655144Z",
  "encerramentoPrevistoEm": null,
  "aviso": {
    "fonte": "INMET",
    "identificador": "SIMULADO-2026-0001",
    "evento": "Acumulado de Chuva",
    "severidade": "PerigoPotencial"
  },
  "duracaoMinutos": null,
  "resultado": {
    "totalDeRuas": 8,
    "ruasComResposta": 6,
    "ruasEmSilencio": 2,
    "ruasConhecidasEm15Min": 5,
    "percentualConhecidoEm15Min": 62.5,
    "percentualComResposta": 75,
    "ruasQuePediramAjuda": 3,
    "minutosAtePrimeiraRespostaEmMedia": 10.3
  }
}
```

### 22. Abrir um treino (simulado)

Polimorfismo: a pergunta e a mensagem de WhatsApp deixam claro que é TREINO.

```http
POST /api/bairros/1/chamadas
Content-Type: application/json

{
  "tipo": "Treino",
  "duracaoMinutos": 20
}
```

Resposta: **HTTP 201** (Location: `http://localhost:5080/api/chamadas/3`)

```json
{
  "id": 3,
  "bairroId": 1,
  "tipo": "Treino",
  "status": "Aberta",
  "pergunta": "TREINO: isto é um simulado, não há alerta real. Como estaria a sua rua agora?",
  "iniciadaEm": "2026-11-10T01:41:59.2765688Z",
  "encerradaEm": null,
  "encerramentoPrevistoEm": "2026-11-10T02:01:59.2765688Z",
  "aviso": null,
  "duracaoMinutos": 20,
  "resultado": null
}
```

### 23. Indicador de impacto do bairro

Percentual de ruas com situação conhecida em 15 minutos, por chamada e com média por tipo (Real x Treino).

```http
GET /api/bairros/1/indicadores
```

Resposta: **HTTP 200**

```json
{
  "bairroId": 1,
  "bairro": "Jardim Pantanal (simulado)",
  "mediaPorTipo": [
    {
      "tipo": "Real",
      "chamadas": 1,
      "percentualMedioConhecidoEm15Min": 62.5,
      "minutosMediosAtePrimeiraResposta": 10.3
    },
    {
      "tipo": "Treino",
      "chamadas": 2,
      "percentualMedioConhecidoEm15Min": 37.5,
      "minutosMediosAtePrimeiraResposta": 8.7
    }
  ],
  "chamadas": [
    {
      "chamadaId": 3,
      "tipo": "Treino",
      "iniciadaEm": "2026-11-10T01:41:59.2765688Z",
      "status": "Aberta",
      "indicador": {
        "totalDeRuas": 8,
        "ruasComResposta": 0,
        "ruasEmSilencio": 8,
        "ruasConhecidasEm15Min": 0,
        "percentualConhecidoEm15Min": 0,
        "percentualComResposta": 0,
        "ruasQuePediramAjuda": 0,
        "minutosAtePrimeiraRespostaEmMedia": null
      }
    },
    {
      "chamadaId": 2,
      "tipo": "Real",
      "iniciadaEm": "2026-11-10T01:16:53.7935871Z",
      "status": "Encerrada",
      "indicador": {
        "totalDeRuas": 8,
        "ruasComResposta": 6,
        "ruasEmSilencio": 2,
        "ruasConhecidasEm15Min": 5,
        "percentualConhecidoEm15Min": 62.5,
        "percentualComResposta": 75,
        "ruasQuePediramAjuda": 3,
        "minutosAtePrimeiraRespostaEmMedia": 10.3
      }
    },
    {
      "chamadaId": 1,
      "tipo": "Treino",
      "iniciadaEm": "2026-11-03T01:41:53.7935871Z",
      "status": "Encerrada",
      "indicador": {
        "totalDeRuas": 8,
        "ruasComResposta": 7,
        "ruasEmSilencio": 1,
        "ruasConhecidasEm15Min": 6,
        "percentualConhecidoEm15Min": 75,
        "percentualComResposta": 87.5,
        "ruasQuePediramAjuda": 1,
        "minutosAtePrimeiraRespostaEmMedia": 8.7
      }
    }
  ]
}
```

### 24. Recurso inexistente

Erros saem padronizados (ProblemDetails, RFC 9457).

```http
GET /api/chamadas/999
```

Resposta: **HTTP 404**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Recurso não encontrado",
  "status": 404,
  "detail": "Chamada 999 não encontrada.",
  "instance": "/api/chamadas/999",
  "traceId": "00-508f153170f40c45f040413bff940443-108c8c2e6ac11f46-00"
}
```

## Parte 4: avisos do INMET (`GET /avisos`)

A API baixa os avisos ativos do INMET (Brasil inteiro) em segundo plano, guarda em cache e devolve só os do município pedido, com os campos do tipo `Aviso` do app. Os avisos abaixo são **reais**, do momento da execução.

### 25. GET /avisos?codigoIbge=5200050 (município com aviso agora)

Do mais grave ao mais leve; no empate, o que começa antes. Datas no horário de Brasília com fuso (-03:00).

```http
GET /avisos?codigoIbge=5200050
```

Resposta: **HTTP 200**

```json
[
  {
    "id": "56358",
    "titulo": "Tempestade",
    "severidade": "Perigo Potencial",
    "gravidade": 1,
    "inicio": "2026-11-09T09:27:00-03:00",
    "fim": "2026-11-09T23:59:00-03:00",
    "quando": "hoje",
    "riscos": [
      "Chuva entre 20 e 30 mm/h ou até 50 mm/dia, ventos intensos (40-60 km/h), e queda de granizo. Baixo risco de corte de energia elétrica, estragos em plantações, queda de galhos de árvores e de alagamentos."
    ],
    "instrucoes": [
      "Em caso de rajadas de vento: (não se abrigue debaixo de árvores, pois há leve risco de queda e descargas elétricas e não estacione veículos próximos a torres de transmissão e placas de propaganda).",
      "Evite usar aparelhos eletrônicos ligados à tomada.",
      "Obtenha mais informações junto à Defesa Civil (telefone 199) e ao Corpo de Bombeiros (telefone 193)."
    ]
  },
  {
    "id": "56361",
    "titulo": "Tempestade",
    "severidade": "Perigo Potencial",
    "gravidade": 1,
    "inicio": "2026-11-10T00:00:00-03:00",
    "fim": "2026-11-10T23:59:00-03:00",
    "quando": "futuro",
    "riscos": [
      "Chuva entre 20 e 30 mm/h ou até 50 mm/dia, ventos intensos (40-60 km/h), e queda de granizo. Baixo risco de corte de energia elétrica, estragos em plantações, queda de galhos de árvores e de alagamentos."
    ],
    "instrucoes": [
      "Em caso de rajadas de vento: (não se abrigue debaixo de árvores, pois há leve risco de queda e descargas elétricas e não estacione veículos próximos a torres de transmissão e placas de propaganda).",
      "Evite usar aparelhos eletrônicos ligados à tomada.",
      "Obtenha mais informações junto à Defesa Civil (telefone 199) e ao Corpo de Bombeiros (telefone 193)."
    ]
  },
  {
    "id": "56362",
    "titulo": "Tempestade",
    "severidade": "Perigo Potencial",
    "gravidade": 1,
    "inicio": "2026-11-11T00:00:00-03:00",
    "fim": "2026-11-11T23:59:00-03:00",
    "quando": "futuro",
    "riscos": [
      "Chuva entre 20 e 30 mm/h ou até 50 mm/dia, ventos intensos (40-60 km/h), e queda de granizo. Baixo risco de corte de energia elétrica, estragos em plantações, queda de galhos de árvores e de alagamentos."
    ],
    "instrucoes": [
      "Em caso de rajadas de vento: (não se abrigue debaixo de árvores, pois há leve risco de queda e descargas elétricas e não estacione veículos próximos a torres de transmissão e placas de propaganda).",
      "Evite usar aparelhos eletrônicos ligados à tomada.",
      "Obtenha mais informações junto à Defesa Civil (telefone 199) e ao Corpo de Bombeiros (telefone 193)."
    ]
  }
]
```

### 26. GET /avisos?codigoIbge=3550308 (São Paulo)

Sem aviso para a capital neste momento: lista vazia.

```http
GET /avisos?codigoIbge=3550308
```

Resposta: **HTTP 200**, sem corpo

```json
[]
```

### 27. Código IBGE inválido

O código precisa ter 7 dígitos: **400**.

```http
GET /avisos?codigoIbge=12345
```

Resposta: **HTTP 400**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "codigoIbge": [
      "Informe o código IBGE do município com 7 dígitos. Ex.: 3550308."
    ]
  },
  "traceId": "00-e48c3dcfe7e0c1cf9a15f9d124ac9b65-606ca8df2c1df161-00"
}
```

### Quanto o app deixa de baixar

| Resposta | Tamanho |
|---|---|
| INMET `avisos/ativos` (Brasil inteiro, com polígonos e ícones) | 148.353 bytes |
| Elo API `GET /avisos?codigoIbge=5200050` | 2.333 bytes |

O app recebe 98,4% menos dados. O contrato do app conta que, em dias com muitos avisos, a resposta do INMET passa de 580 KB.

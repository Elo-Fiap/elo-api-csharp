# Evidências: fluxo completo da API

Requisições feitas de verdade contra a API rodando localmente (`dotnet run --project src/Elo.Api`), em 09/11/2026 15:19 UTC, a partir do banco recém-criado com os dados simulados. Os ids e horários vêm dessa execução.

O mesmo roteiro está em [`src/Elo.Api/Elo.Api.http`](../../src/Elo.Api/Elo.Api.http) para repetir no VS Code, Visual Studio ou Rider.

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

A ordem vem do LINQ do `PriorizadorDeRuas`: primeiro quem pediu ajuda (sem ninguém a caminho na frente), depois quem está em silêncio (o sensor acima do limite na frente), depois água subindo e tudo bem. Resposta resumida aqui; o JSON completo tem também sensor, observação e o link do WhatsApp de cada rua.

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
    "3. Rua das Palafitas: EmSilencio | sensor 46 cm, acima do limite",
    "4. Rua dos Barqueiros: EmSilencio",
    "5. Rua do Córrego Seco: EmSilencio",
    "6. Rua da Várzea: AguaSubindo",
    "7. Rua Ipê Amarelo: TudoBem",
    "8. Travessa da Taboa: TudoBem"
  ]
}
```

### 4. Uma rua pede ajuda (um toque)

A Rua do Córrego Seco estava em silêncio e agora pede ajuda. Repare que CPF e telefone da observação são mascarados antes de salvar.

```http
POST /api/chamadas/2/respostas
Content-Type: application/json

{
  "idLocal": "ec15d98b-0f85-4d34-a3c8-989331d65077",
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
  "idLocal": "ec15d98b-0f85-4d34-a3c8-989331d65077",
  "chamadaId": 2,
  "ruaId": 6,
  "situacao": "PrecisoDeAjuda",
  "pessoas": 4,
  "observacao": "Família ilhada. Liguem [telefone removido], CPF [CPF removido]",
  "canal": "App",
  "respondidaEm": "2026-11-09T15:19:47.7249591Z",
  "recebidaEm": "2026-11-09T15:19:47.7249591Z"
}
```

### 5. O celular reenvia a mesma resposta (internet voltou)

Mesmo `idLocal`: a API devolve **200** com a resposta já salva, sem duplicar.

```http
POST /api/chamadas/2/respostas
Content-Type: application/json

{
  "idLocal": "ec15d98b-0f85-4d34-a3c8-989331d65077",
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
  "idLocal": "ec15d98b-0f85-4d34-a3c8-989331d65077",
  "chamadaId": 2,
  "ruaId": 6,
  "situacao": "PrecisoDeAjuda",
  "pessoas": 4,
  "observacao": "Família ilhada. Liguem [telefone removido], CPF [CPF removido]",
  "canal": "App",
  "respondidaEm": "2026-11-09T15:19:47.7249591Z",
  "recebidaEm": "2026-11-09T15:19:47.7249591Z"
}
```

### 6. Regra de negócio: pedir ajuda sem dizer quantas pessoas

O domínio recusa e a API devolve **422** em ProblemDetails.

```http
POST /api/chamadas/2/respostas
Content-Type: application/json

{
  "ruaId": 4,
  "situacao": "PrecisoDeAjuda"
}
```

Resposta: **HTTP 422**

```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "Ao pedir ajuda, informe quantas pessoas precisam (de 1 a 500).",
  "instance": "/api/chamadas/2/respostas",
  "traceId": "00-5125ba50dc3b0b936ddfa8ed0cdbab2a-94e8cd65f2e67531-00"
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
  "traceId": "00-69e52203eacfb0db7c723e25171247b3-f67a83cb228ae5b6-00"
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
  "registradaEm": "2026-11-09T15:19:47.7650816Z"
}
```

### 9. O ESP32 da Rua dos Barqueiros manda uma leitura

52,5 cm com limite de 50 cm: a leitura entra como alerta.

```http
POST /api/sensores/2/leituras
Content-Type: application/json

{
  "nivelCm": 52.5
}
```

Resposta: **HTTP 201** (Location: `http://localhost:5080/api/sensores/2/leituras`)

```json
{
  "id": 13,
  "sensorId": 2,
  "nivelCm": 52.5,
  "limiteCm": 50,
  "acimaDoLimite": true,
  "medidaEm": "2026-11-09T15:19:47.7816686Z",
  "recebidaEm": "2026-11-09T15:19:47.7816686Z"
}
```

### 10. Painel depois das mudanças

Córrego Seco subiu para os pedidos de ajuda; Palafitas desceu entre as ruas em silêncio (alguém a caminho); Barqueiros passou a ter sensor acima do limite.

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
    "minutosAtePrimeiraRespostaEmMedia": 10.6
  },
  "ruas (ordem do painel)": [
    "1. Rua do Aguapé: PrecisaDeAjuda",
    "2. Rua do Córrego Seco: PrecisaDeAjuda",
    "3. Rua das Garças: PrecisaDeAjuda (alguém a caminho)",
    "4. Rua dos Barqueiros: EmSilencio | sensor 52.5 cm, acima do limite",
    "5. Rua das Palafitas: EmSilencio (alguém a caminho) | sensor 46 cm, acima do limite",
    "6. Rua da Várzea: AguaSubindo",
    "7. Rua Ipê Amarelo: TudoBem",
    "8. Travessa da Taboa: TudoBem"
  ]
}
```

### 11. Registro da chamada (o que foi avisado e o que foi feito)

Linha do tempo montada com LINQ (Concat + OrderBy) juntando abertura, respostas, ações e alertas de sensor.

```http
GET /api/chamadas/2/registro
```

Resposta: **HTTP 200**

```json
[
  {
    "momento": "2026-11-09T14:53:16.2579208Z",
    "tipo": "ChamadaAberta",
    "ruaId": null,
    "rua": null,
    "descricao": "Chamada real aberta pelo aviso INMET: Acumulado de Chuva (perigo potencial)."
  },
  {
    "momento": "2026-11-09T14:57:16.2579208Z",
    "tipo": "Resposta",
    "ruaId": 3,
    "rua": "Travessa da Taboa",
    "descricao": "Tudo bem, pelo app."
  },
  {
    "momento": "2026-11-09T14:58:16.2579208Z",
    "tipo": "Resposta",
    "ruaId": 2,
    "rua": "Rua do Aguapé",
    "descricao": "Água subindo, pelo app."
  },
  {
    "momento": "2026-11-09T14:59:16.2579208Z",
    "tipo": "Resposta",
    "ruaId": 8,
    "rua": "Rua Ipê Amarelo",
    "descricao": "Tudo bem, pelo app."
  },
  {
    "momento": "2026-11-09T15:01:16.2579208Z",
    "tipo": "Resposta",
    "ruaId": 1,
    "rua": "Rua das Garças",
    "descricao": "Preciso de ajuda (3 pessoa(s)), pelo app."
  },
  {
    "momento": "2026-11-09T15:03:16.2579208Z",
    "tipo": "AcaoDoCoordenador",
    "ruaId": 1,
    "rua": "Rua das Garças",
    "descricao": "Coordenação marcou \"Vou até lá\"."
  },
  {
    "momento": "2026-11-09T15:05:16.2579208Z",
    "tipo": "Resposta",
    "ruaId": 5,
    "rua": "Rua da Várzea",
    "descricao": "Água subindo, por Sms."
  },
  {
    "momento": "2026-11-09T15:08:16.2579208Z",
    "tipo": "SensorAcimaDoLimite",
    "ruaId": 7,
    "rua": "Rua das Palafitas",
    "descricao": "Sensor ESP32-PANTANAL-01 passou do limite de 40 cm (42 cm). Máximo no período: 46 cm."
  },
  {
    "momento": "2026-11-09T15:09:16.2579208Z",
    "tipo": "AcaoDoCoordenador",
    "ruaId": 4,
    "rua": "Rua dos Barqueiros",
    "descricao": "Coordenação chamou a rua no WhatsApp."
  },
  {
    "momento": "2026-11-09T15:11:16.2579208Z",
    "tipo": "Resposta",
    "ruaId": 2,
    "rua": "Rua do Aguapé",
    "descricao": "Preciso de ajuda (5 pessoa(s)), pelo app."
  },
  {
    "momento": "2026-11-09T15:19:47.7249591Z",
    "tipo": "Resposta",
    "ruaId": 6,
    "rua": "Rua do Córrego Seco",
    "descricao": "Preciso de ajuda (4 pessoa(s)), pelo app."
  },
  {
    "momento": "2026-11-09T15:19:47.7650816Z",
    "tipo": "AcaoDoCoordenador",
    "ruaId": 7,
    "rua": "Rua das Palafitas",
    "descricao": "Coordenação marcou \"Vou até lá\"."
  },
  {
    "momento": "2026-11-09T15:19:47.7816686Z",
    "tipo": "SensorAcimaDoLimite",
    "ruaId": 4,
    "rua": "Rua dos Barqueiros",
    "descricao": "Sensor ESP32-PANTANAL-02 passou do limite de 50 cm (52,5 cm). Máximo no período: 52,5 cm."
  }
]
```

### 12. Abrir outra chamada com uma já aberta

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
  "traceId": "00-0facc70007b7b939719860ebffd9783a-6daf5928c68057d1-00"
}
```

### 13. Encerrar a chamada real

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
  "iniciadaEm": "2026-11-09T14:53:16.2579208Z",
  "encerradaEm": "2026-11-09T15:19:47.8972756Z",
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
    "minutosAtePrimeiraRespostaEmMedia": 10.6
  }
}
```

### 14. Abrir um treino (simulado)

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
  "iniciadaEm": "2026-11-09T15:19:47.9080171Z",
  "encerradaEm": null,
  "encerramentoPrevistoEm": "2026-11-09T15:39:47.9080171Z",
  "aviso": null,
  "duracaoMinutos": 20,
  "resultado": null
}
```

### 15. Indicador de impacto do bairro

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
      "minutosMediosAtePrimeiraResposta": 10.6
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
      "iniciadaEm": "2026-11-09T15:19:47.9080171Z",
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
      "iniciadaEm": "2026-11-09T14:53:16.2579208Z",
      "status": "Encerrada",
      "indicador": {
        "totalDeRuas": 8,
        "ruasComResposta": 6,
        "ruasEmSilencio": 2,
        "ruasConhecidasEm15Min": 5,
        "percentualConhecidoEm15Min": 62.5,
        "percentualComResposta": 75,
        "ruasQuePediramAjuda": 3,
        "minutosAtePrimeiraRespostaEmMedia": 10.6
      }
    },
    {
      "chamadaId": 1,
      "tipo": "Treino",
      "iniciadaEm": "2026-11-02T15:18:16.2579208Z",
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

### 16. Recurso inexistente

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
  "traceId": "00-ae09e0ee5d2f2318f6b9868acf85337d-72625c6c81c06f5f-00"
}
```

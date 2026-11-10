# Contrato da Elo API (para o app e o ESP32)

A proposta citava um `docs/contrato-api.md` no repositório do app, para quem fizesse o C# validar.
Esse arquivo não estava disponível, então este é o contrato **implementado** nesta API.
Quem cuida do app pode comparar com o que já usa e avisar o que precisa mudar.

A referência completa e sempre atualizada é o OpenAPI: `GET /openapi/v1.json`, com telas em `/swagger` e `/scalar`.

## Convenções

| Item | Regra |
|---|---|
| Formato | JSON, nomes em camelCase (`ruaId`, `respondidaEm`) |
| Enums | Texto: `"PrecisoDeAjuda"`, não `3` |
| Datas | ISO 8601. A API devolve sempre em UTC (`...Z`). Na entrada, aceita qualquer fuso (`2026-11-09T18:20:00-03:00`) |
| Erros | ProblemDetails (RFC 9457): `{ "title", "status", "detail" }`. Validação de campos traz `errors` |
| Status | 200 ok, 201 criado (com `Location`), 204 excluído, 400 corpo inválido, 404 não encontrado, 409 conflito, 422 regra de negócio |
| Dados | Sem cadastro de moradores. Da liderança, só apelido e WhatsApp |

## Fluxo do app da liderança (telas Agora, Preciso de ajuda e Recebido)

1. Descobrir a chamada aberta do bairro: `GET /api/bairros` → campo `chamadaAbertaId`
   (ou `GET /api/chamadas?bairroId=1&status=Aberta`).
2. Mostrar a pergunta e o aviso: `GET /api/chamadas/{id}` → `pergunta`, `aviso` (só em chamada real).
3. Régua d'água da rua: `GET /api/ruas/{ruaId}` → `sensores[].ultimaLeitura.nivelCm`.
4. Responder em um toque:

```http
POST /api/chamadas/{id}/respostas
Content-Type: application/json

{
  "idLocal": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
  "ruaId": 6,
  "situacao": "PrecisoDeAjuda",
  "pessoas": 4,
  "observacao": "Família ilhada no térreo",
  "respondidaEm": "2026-11-09T18:20:00-03:00"
}
```

| Campo | Obrigatório | Observação |
|---|---|---|
| `idLocal` | Recomendado | UUID gerado no celular. **É o que permite o reenvio offline sem duplicar** |
| `ruaId` | Sim | |
| `situacao` | Sim | `TudoBem`, `AguaSubindo` ou `PrecisoDeAjuda` |
| `pessoas` | Só em `PrecisoDeAjuda` | 1 a 500. Em outra situação, a API recusa (422) |
| `observacao` | Não | Até 280 caracteres. CPF, telefone e e-mail são trocados por marcadores antes de salvar |
| `canal` | Não | `App` (padrão), `Sms`, `WhatsApp`, `Ligacao`, `Presencial` |
| `respondidaEm` | Não | Hora do toque no celular. Vazio = hora de chegada |

Respostas da API:

- **201**: resposta nova (tela "Recebido").
- **200**: o mesmo `idLocal` já tinha chegado. O app pode marcar como enviada e apagar da fila local.
- **422**: regra de negócio (ex.: faltou `pessoas`, resposta fora do horário da chamada).

### Sem internet

O app guarda a resposta com `idLocal` e `respondidaEm`, e reenvia quando a rede volta.
A API aceita a resposta mesmo se a chamada já foi encerrada, desde que ela tenha sido **dada** dentro do horário da chamada.
Para o indicador de 15 minutos conta a hora em que a resposta **chegou** (`recebidaEm`).

## Fluxo do coordenador (tela Bairro)

| Ação | Requisição |
|---|---|
| Ver o painel da chamada aberta | `GET /api/bairros/{id}/painel` |
| Abrir chamada real | `POST /api/bairros/{id}/chamadas` com `{ "tipo": "Real", "aviso": { "fonte": "INMET", "identificador": "...", "evento": "Acumulado de Chuva", "severidade": "Perigo" } }` |
| Abrir treino | `POST /api/bairros/{id}/chamadas` com `{ "tipo": "Treino", "duracaoMinutos": 30 }` |
| Registrar resposta recebida por SMS/WhatsApp/ligação | `POST /api/chamadas/{id}/respostas` com `"canal": "Sms"` |
| Marcar "Vou até lá" | `POST /api/chamadas/{id}/acoes` com `{ "ruaId": 7, "tipo": "VouAteLa" }` |
| Encerrar | `POST /api/chamadas/{id}/encerramento` |
| Ver o registro | `GET /api/chamadas/{id}/registro` |
| Indicador de impacto | `GET /api/bairros/{id}/indicadores` |

Cada item de `ruas` no painel traz `posicao`, `status` (`PrecisaDeAjuda`, `EmSilencio`, `AguaSubindo`, `TudoBem`),
a última resposta, o sensor e um `linkWhatsApp` com a mensagem pronta (no treino, a mensagem avisa que é simulado).

## Plano da rua (tela Plano, abre sem internet)

`GET /api/ruas/{id}/plano` → `rota`, `pontoDeEncontro`, `abrigo`, `itensParaLevar[]`, `atualizadoEm`.
O app guarda uma cópia e compara `atualizadoEm` para saber se precisa baixar de novo.

## Registro (tela Registro)

`GET /api/ruas/{id}/registro` → respostas da rua agrupadas por mês (`"mes": "2026-11"`).
As respostas são apagadas pela API depois de 30 dias (privacidade); o indicador das chamadas encerradas continua guardado.

## ESP32 (Physical Computing)

```http
POST /api/sensores/{id}/leituras
Content-Type: application/json

{ "nivelCm": 42.5 }
```

- `nivelCm`: nível da água em cm (0 a 500), calculado no ESP32 a partir da distância do HC-SR04.
- `medidaEm` é opcional; sem ele, vale a hora de chegada (o ESP32 no Wokwi não precisa de relógio).
- A resposta (201) traz `acimaDoLimite`. O buzzer continua sendo decidido no próprio ESP32, mesmo sem internet.
- O Wokwi só alcança endereço público: a API precisa estar publicada (por exemplo, na VM Linux).

Exemplo em Arduino (ESP32):

```cpp
#include <WiFi.h>
#include <HTTPClient.h>

void enviarLeitura(float nivelCm) {
  HTTPClient http;
  http.begin("http://SEU-IP-PUBLICO:5080/api/sensores/1/leituras");
  http.addHeader("Content-Type", "application/json");
  int status = http.POST("{\"nivelCm\":" + String(nivelCm, 1) + "}");
  Serial.printf("API respondeu %d\n", status); // 201 = leitura registrada
  http.end();
}
```

# Contrato da Elo API com o app e a régua

O contrato oficial é o [`docs/contrato-api.md` do `elo-app`](https://github.com/Elo-Fiap/elo-app/blob/main/docs/contrato-api.md),
escrito para os colegas de C#, SOA e Physical Computing. Esta API implementa as rotas dele que são da "API do bairro",
**sem o prefixo `/api`**, do jeito que o app (`src/services/bairro.ts`) e a régua (`sketch.ino`) já chamam.

A API também tem rotas próprias em `/api/...` (abrir e encerrar chamadas, painel completo, indicadores, cadastros).
A referência completa e sempre atualizada é o OpenAPI: `GET /openapi/v1.json`, com telas em `/swagger` e `/scalar`.

## Conferência com o contrato do app

Conferido lendo o contrato e o código do app (`src/services/bairro.ts`, `src/services/http.ts`, `src/types/index.ts`) em 09/11/2026.
Cada item tem teste de integração em [`tests/Elo.Tests/Integracao/ContratoDoAppApiTests.cs`](../tests/Elo.Tests/Integracao/ContratoDoAppApiTests.cs).

| Item do contrato | Nesta API | Confere? |
|---|---|---|
| `GET /situacao` com `bairro`, `chamadaIniciadaEm` e `ruas[]` (`id`, `nome`, `status`, `respondidaEm`) | Igual | Sim |
| `status`: `tudo_bem`, `agua_subindo`, `preciso_ajuda`; silêncio = `null` | Igual | Sim |
| `chamadaIniciadaEm` nulo sem chamada em andamento | Igual (e todas as ruas em silêncio) | Sim |
| Resposta de chamada anterior não vale: a rua volta a `null` | Só contam as respostas da chamada aberta | Sim |
| `respondidaEm` é o `criadaEm` recebido, para o app comparar com o dele | Mesmo instante, no formato do `toISOString()` (`2026-11-05T15:00:00.000Z`) | Sim |
| Ordem: o app ordena; o LINQ do servidor pode ordenar também | Vem na ordem do painel (quem pediu ajuda, depois silêncio) | Sim |
| `POST /chamadas` com `id` criado no app, idempotente | `id` de até 64 caracteres; o mesmo `id` devolve 200 com o registro existente | Sim |
| Resposta `201` (ou `200` se já existia) | 201 e 200, **sempre com corpo JSON** (o `http.ts` do app lê o corpo de todo 2xx que não seja 204) | Sim |
| `pessoas` e `observacao` podem ser nulos | Aceitos nulos; `pessoas`, quando vem, de 1 a 500 | Sim |
| `observacao` com mais de 200 caracteres é recusada | 400 | Sim |
| A observação não vai para log | Não vai (pode ter dado pessoal digitado sem querer) | Sim |
| `GET /regua` com `reguaId`, `local`, `nivelMetros`, `limiteMetros`, `medidaEm` | Igual, em metros | Sim |
| O limite fica no servidor, por régua | `LimiteAlertaCm` de cada régua (a `regua-exemplo` vem com 1,20 m) | Sim |
| `GET /ruas/{id}/plano` com `ruaId`, `ruaNome`, `pontoEncontro`, `rotaSaida`, `abrigo`, `itensParaLevar`, `contatos` | Igual; contatos: Defesa Civil 199 e Bombeiros 193 | Sim |
| `404` quando a rua não tem plano | 404 (também para id que não existe) | Sim |
| `POST /leituras` com o header `X-Chave-Dispositivo` | Conferido por um filtro, antes de ler o corpo; sem chave ou com chave errada, 401 | Sim |
| Corpo `{ reguaId, nivelMetros, medidaEm?, bateriaPercentual? }` | Igual; `nivelMetros` nulo dá 400 | Sim |
| `medidaEm` omitido: vale a hora de recebimento | Igual | Sim |
| Resposta `204` | 204, sem corpo | Sim |
| A chave fica fora do repositório | Variável de ambiente `Dispositivos__Chave` ou `dotnet user-secrets` | Sim |
| Recomendação: `GET /avisos?codigoIbge=` filtrando o INMET no servidor | **Não feito**. O app segue buscando o INMET direto | Não |

## O que o contrato não dizia e foi decidido aqui

| Ponto | Decisão |
|---|---|
| Em qual chamada entra a resposta do app (o `POST /chamadas` não diz) | Na chamada do bairro da rua cuja janela inclui o `criadaEm`. Uma resposta guardada offline vale mesmo se chegar depois do encerramento |
| Resposta sem chamada aberta | **422**. O app trata como falha e tenta de novo depois |
| Quem abre a chamada | O coordenador, pela API completa: `POST /api/bairros/{id}/chamadas` (real, com o aviso oficial, ou treino) |
| Qual bairro as rotas do app atendem | O de `ContratoDoApp:BairroId` no `appsettings.json` ("o acesso do piloto vai por URL própria do bairro"). O `GET /situacao` aceita `?bairroId=` para testes |
| Formato do id da rua | O número da rua como texto (`"7"`). O app já converte com `String(r.id)` |
| Qual régua o `GET /regua` mostra | A de `ContratoDoApp:ReguaPadrao` (`regua-exemplo`, o id fixo no firmware), ou outra com `?reguaId=` |
| Maiúsculas no `reguaId` | Não diferencia: `REGUA-EXEMPLO` e `regua-exemplo` são a mesma régua |
| Chave não configurada no servidor | Em desenvolvimento, `POST /leituras` aceita sem chave (para testar no Swagger). Em qualquer outro ambiente, 401 |
| `bateriaPercentual` | Aceito, mas não guardado nesta versão |

## Exemplos

App, resposta da liderança:

```http
POST /chamadas
Content-Type: application/json

{ "id": "m3k9x2a-7f4q1b2c", "ruaId": "8", "status": "agua_subindo", "pessoas": null, "observacao": "Água na calçada", "criadaEm": "2026-11-09T18:20:00.000Z" }
```

Régua (ESP32), igual ao que o `sketch.ino` monta:

```http
POST /leituras
Content-Type: application/json
X-Chave-Dispositivo: <chave da régua, fora do repositório>

{"reguaId":"regua-exemplo","nivelMetros":1.42}
```

As respostas reais dessas chamadas estão em [`docs/evidencias/fluxo-completo.md`](evidencias/fluxo-completo.md), parte 2.

## Para ligar

- **App:** `EXPO_PUBLIC_API_URL=http://<ip-da-maquina>:5080` (sem `/api` e sem barra no final; o app já tira a barra).
- **Régua:** o Wokwi só alcança endereço público. Publique a API (por exemplo, na VM Linux de Sistemas Operacionais) com
  `Dispositivos__Chave` configurada e use a mesma chave no firmware. A régua recomenda HTTP simples, porque o HTTPS
  no ESP32 simulado levou de 6,5 a 7,7 segundos e falhou com o buzzer tocando.

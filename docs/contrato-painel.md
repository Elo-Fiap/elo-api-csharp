# Contrato da API para o painel de administração

Esta é a rota que alimenta o painel web (`elo-admin`). Ela devolve, numa resposta só, tudo o que as telas precisam.
O painel deve seguir este documento.

```
GET /api/admin/instantaneo?dias=30
```

| Parâmetro | Tipo | Padrão | Regra |
|---|---|---|---|
| `dias` | inteiro | `30` | De 1 a 30. Fora disso (ou texto), a API devolve **400** com `ProblemDetails` (`errors.dias`). |

Regras gerais:

- JSON em **camelCase**, como o resto da API. Sem autenticação; **CORS** liberado para qualquer origem.
- **Enums como texto**, com os nomes reais da API (tabela mais abaixo).
- **Datas em UTC, ISO 8601, com `Z`** (ex.: `2026-11-12T05:00:00Z`).
- **Medidas em metros** (a API guarda centímetros por dentro; a rota já converte).
- Coordenadas em graus. O **traçado** de uma rua é uma lista de pares **`[longitude, latitude]`** (a ordem do GeoJSON e do MapLibre).
- Campos opcionais vêm como `null` quando não existem (a chave sempre aparece).
- Esta rota **nunca devolve 503**: se o INMET estiver fora do ar, `avisos` vem vazio e o resto segue normal.
- A rota só lê: não muda nada no banco.

## Formato

```json
{
  "geradoEm": "2026-11-12T05:00:00Z",
  "bairros": [{
    "id": 1, "nome": "Jardim Pantanal (simulado)", "cidade": "São Paulo", "uf": "SP", "codigoIbge": "3550308",
    "latitude": -23.47939, "longitude": -46.40912, "dadosSimulados": true,
    "percentualSemApp": 31.0, "semAppMedidoEm": "2026-09-28T05:00:00Z",
    "ruas": [{ "id": 1, "nome": "Rua das Garças", "microarea": "Microárea 1", "nivelRisco": "Alto", "ativa": true,
               "latitude": -23.48, "longitude": -46.409, "tracado": [[-46.41, -23.48], [-46.409, -23.481]] }]
  }],
  "sensores": [{ "id": 1, "codigo": "regua-exemplo", "ruaId": 1, "bairroId": 1, "local": "Córrego...", "latitude": -23.47, "longitude": -46.41,
                 "limiteMetros": 1.2, "ativo": true, "leituras": [{ "nivelMetros": 1.42, "medidaEm": "2026-11-12T04:50:00Z" }] }],
  "chamadas": [{ "id": 2, "bairroId": 1, "tipo": "Real", "abertaEm": "2026-11-12T04:35:00Z", "encerradaEm": null, "avisoTitulo": "Chuvas intensas",
                 "respostas": [{ "id": 10, "ruaId": 3, "situacao": "PrecisoDeAjuda", "pessoas": 6, "respondidaEm": "2026-11-12T04:43:00Z", "recebidaEm": "2026-11-12T04:43:00Z" }],
                 "acoes": [{ "ruaId": 3, "tipo": "VouAteLa", "em": "2026-11-12T04:45:00Z" }] }],
  "avisos": [{ "id": "56273", "titulo": "Chuvas Intensas", "severidade": "Perigo", "gravidade": 2,
               "inicio": "2026-11-05T03:01:00Z", "fim": "2026-11-06T02:59:00Z", "ufs": ["SP"] }]
}
```

## Campos

### Raiz

| Campo | Tipo | O que é |
|---|---|---|
| `geradoEm` | data | Momento em que a API montou a resposta. É a "hora de agora" da API: use-a, e não o relógio do navegador, para contar "há quanto tempo". |
| `bairros` | lista | **Todos** os bairros, por `id`. |
| `sensores` | lista | **Todas** as réguas, por `id` (ativas e inativas). |
| `chamadas` | lista | Chamadas abertas agora **ou** abertas dentro da janela de `dias`, da mais recente para a mais antiga. |
| `avisos` | lista | Avisos ativos do INMET para o município dos bairros. |

### `bairros[]`

| Campo | Tipo | Observação |
|---|---|---|
| `id`, `nome`, `cidade` | inteiro, texto, texto | |
| `uf` | texto ou `null` | Sigla em maiúsculas (2 letras). |
| `codigoIbge` | texto ou `null` | 7 dígitos, **em texto** (não perde zero à esquerda). É a chave dos avisos do INMET. |
| `latitude`, `longitude` | número ou `null` | Centro do bairro. Vêm sempre juntos (ou os dois, ou nenhum). |
| `dadosSimulados` | booleano | `true` quando ruas e respostas são simuladas. |
| `percentualSemApp` | número 0 a 100 ou `null` | Resultado de um simulado feito **sem o app** (WhatsApp e telefone): % de ruas com situação conhecida em 15 minutos. É a base de comparação do indicador de impacto. |
| `semAppMedidoEm` | data ou `null` | Quando esse simulado foi medido. Só existe junto de `percentualSemApp`. |
| `ruas` | lista | **Todas** as ruas, ativas e inativas, por `id`. |

### `bairros[].ruas[]`

| Campo | Tipo | Observação |
|---|---|---|
| `id`, `nome`, `microarea` | inteiro, texto, texto | |
| `nivelRisco` | `Baixo` \| `Medio` \| `Alto` | Quanto a rua costuma alagar. |
| `ativa` | booleano | Rua desativada não entra no painel do coordenador nem no indicador. |
| `latitude`, `longitude` | número ou `null` | Ponto de referência da rua (onde o marcador aparece). |
| `tracado` | `[[lon, lat], ...]` ou `null` | Linha da rua no mapa, com 2 ou mais pontos. `null` quando a rua não tem traçado: desenhe só o marcador. |

### `sensores[]`

| Campo | Tipo | Observação |
|---|---|---|
| `id`, `codigo` | inteiro, texto | `codigo` é o `reguaId` gravado no ESP32 (minúsculas). |
| `ruaId`, `bairroId` | inteiro | A rua onde está e o bairro dessa rua. |
| `local` | texto | Descrição do ponto. |
| `latitude`, `longitude` | número ou `null` | Posição da régua no mapa. |
| `limiteMetros` | número | Leitura **igual ou acima** disso é alerta. |
| `ativo` | booleano | Régua inativa não recebe leituras novas. |
| `leituras` | lista | Só as **últimas 72 horas**, no máximo **500**, **da mais antiga para a mais nova**. Se houver mais de 500, ficam as 500 mais recentes. |

`leituras[]`: `nivelMetros` (número, em metros) e `medidaEm` (data).

### `chamadas[]`

| Campo | Tipo | Observação |
|---|---|---|
| `id`, `bairroId` | inteiro | |
| `tipo` | `Real` \| `Treino` | Real é motivada por aviso oficial; Treino é simulado com prazo. |
| `abertaEm` | data | |
| `encerradaEm` | data ou `null` | `null` = **aberta agora**. Um treino que passou do prazo conta como encerrado, na hora do prazo, mesmo que ninguém o tenha encerrado à mão. |
| `avisoTitulo` | texto ou `null` | Título do aviso oficial que motivou a chamada. Só a chamada `Real` tem. |
| `respostas` | lista | **Todas** as respostas da chamada, da mais antiga para a mais nova (por `respondidaEm`). |
| `acoes` | lista | **Todas** as ações do coordenador, da mais antiga para a mais nova. |

A janela: entra a chamada que **está aberta agora** (qualquer que seja a idade) ou que **foi aberta nos últimos `dias` dias**.

`respostas[]`:

| Campo | Tipo | Observação |
|---|---|---|
| `id` | inteiro | |
| `ruaId` | inteiro | |
| `situacao` | `TudoBem` \| `AguaSubindo` \| `PrecisoDeAjuda` | |
| `pessoas` | inteiro ou `null` | Quantas pessoas, se a liderança informou. |
| `respondidaEm` | data | Hora em que a liderança tocou no botão (relógio do celular, mesmo offline). |
| `recebidaEm` | data | Hora em que a resposta chegou na API. É a que conta para o indicador de 15 minutos. |

`acoes[]`: `ruaId`, `tipo` (`ContatoWhatsApp` \| `Ligacao` \| `VouAteLa`) e `em` (data).

A resposta mais recente de uma rua é a que vale; um `VouAteLa` só conta se veio depois da última resposta da rua (as mesmas regras do painel do coordenador, em `docs/contrato-api.md`).

### `avisos[]`

Avisos **ativos** (não encerrados) do INMET cujo município (código IBGE inteiro) é o de algum bairro. O mesmo aviso aparece uma vez só.

| Campo | Tipo | Observação |
|---|---|---|
| `id` | texto | Id do aviso no INMET. |
| `titulo` | texto | Ex.: `Chuvas Intensas`. |
| `severidade` | texto | Texto do INMET: `Perigo Potencial`, `Perigo` ou `Grande Perigo`. |
| `gravidade` | inteiro | `1`, `2` ou `3` (a mesma ordem da severidade). |
| `inicio`, `fim` | data ou `null` | Convertidos para UTC (o INMET manda com fuso `-03:00`). `null` se o INMET mandou uma data ilegível. |
| `ufs` | lista de texto | UFs (maiúsculas, em ordem alfabética) dos bairros a que o aviso vale. Se o mesmo aviso vale para bairros de UFs diferentes, vêm todas. |

Ordem: da maior gravidade para a menor e, no empate, o que começa antes.

## Nomes dos enums

| Onde | Valores |
|---|---|
| `nivelRisco` | `Baixo`, `Medio`, `Alto` |
| `chamadas[].tipo` | `Real`, `Treino` |
| `respostas[].situacao` | `TudoBem`, `AguaSubindo`, `PrecisoDeAjuda` |
| `acoes[].tipo` | `ContatoWhatsApp`, `Ligacao`, `VouAteLa` |

(As outras rotas do app e do ESP32 usam `tudo_bem`, `agua_subindo`, `preciso_ajuda`; esta rota usa os nomes da API.)

## Erros

| Status | Quando |
|---|---|
| `200` | Sempre que `dias` está certo, mesmo com o INMET fora do ar. |
| `400` | `dias` fora de 1 a 30 ou que não é número. Corpo `ProblemDetails` com `errors.dias`. |

## Dados de exemplo

Com a configuração `DadosDeExemplo:IncluirBairrosDoPainel` ligada (padrão) e o banco vazio, a API semeia 12 bairros:
o piloto (Jardim Pantanal, id 1) e 11 bairros simulados de outras cidades, com ruas, réguas (72 horas de leituras, de 10 em 10 minutos),
chamadas, respostas e ações. Só o piloto tem traçados de rua (`tracado`); nos outros, as ruas têm só o ponto. Os tempos da semente são
relativos ao momento em que o banco é criado: para "rejuvenescer" os dados, apague `src/Elo.Api/elo.db` e suba a API de novo.

Os traçados das ruas do piloto vêm do **OpenStreetMap** (© colaboradores do OpenStreetMap, licença ODbL,
<https://www.openstreetmap.org/copyright>), usados com os nomes fictícios da API.

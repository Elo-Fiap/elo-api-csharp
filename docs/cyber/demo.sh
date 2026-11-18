#!/usr/bin/env bash
# Roteiro do vídeo do módulo de Cybersecurity (Elo, CitizenTech).
#
# Mostra, ao vivo, os quatro pontos do módulo:
#   (a) o app manda "Preciso de ajuda" com CPF, RG, senha, doença e nome na observação;
#   (b) um JSON com chaves que a API nem conhece (exemplo do professor: documento e senha);
#   (c) dado LEGADO: gravado direto no SQLite (sem passar pela API) e lido de volta.
# Entre os passos o script pausa e mostra o painel Risco Cidadão (GET /api/seguranca/painel).
#
# Uso:
#   dotnet run --project src/Elo.Api          # em outro terminal
#   docs/cyber/demo.sh [URL_BASE]             # padrão: http://localhost:5080 (porta do launchSettings.json)
#
# Variáveis opcionais:
#   ELO_DB=/caminho/elo.db    banco SQLite da API (padrão: src/Elo.Api/elo.db), usado só no passo (c)
#
# Precisa de: curl. Opcionais: jq (painel resumido e colorido) e sqlite3 (passo c).
# Os dados abaixo são de mentira (CPF e RG inventados), só para a demonstração.

set -u

BASE="${1:-http://localhost:5080}"
BASE="${BASE%/}"
RAIZ="$(cd "$(dirname "$0")/../.." && pwd)"
BANCO="${ELO_DB:-$RAIZ/src/Elo.Api/elo.db}"
BAIRRO_ID=1       # o piloto (Jardim Pantanal), o mesmo bairro que as rotas do app atendem
RUA_ID=6          # uma rua do piloto
SUFIXO="$(date +%s)"

TEM_JQ=0
command -v jq >/dev/null 2>&1 && TEM_JQ=1

# ---------- Apresentação ----------
titulo() { printf '\n\033[1;36m== %s ==\033[0m\n' "$1"; }
nota()   { printf '\033[2m%s\033[0m\n' "$1"; }
pausa()  { echo; read -r -p "[Enter para continuar] " _; }

# Imprime o JSON bonito se tiver jq; senão imprime como veio.
mostrar_json() { if [ "$TEM_JQ" = 1 ]; then jq . 2>/dev/null || cat; else cat; fi; echo; }

# O painel resumido: farol, índice, frase ao cidadão, totais e os eventos (só metadados, nunca o dado).
painel() {
  titulo "Painel Risco Cidadão  (GET /api/seguranca/painel)"
  # Os eventos são escritos em segundo plano e a mensagem ao cidadão chega logo depois: espera um instante.
  sleep 1
  if [ "$TEM_JQ" = 1 ]; then
    curl -s "$BASE/api/seguranca/painel" | jq '{
      farol, indice, resumo, totais, porTipo,
      ultimosEventos: [.ultimosEventos[:4][] | {etapa, sink, tipos, onde, descricaoTecnica, mensagemAoCidadao, traduzidaPor}]
    }'
  else
    curl -s "$BASE/api/seguranca/painel"; echo
  fi
}

# ---------- Antes de começar ----------
if ! curl -fs "$BASE/health" >/dev/null; then
  echo "A API não respondeu em $BASE. Rode:  dotnet run --project src/Elo.Api" >&2
  exit 1
fi

titulo "Elo, módulo de Cybersecurity: demonstração"
nota "API: $BASE    painel visual: $BASE/risco-cidadao.html"
nota "Deixe a página do painel aberta ao lado: ela atualiza sozinha a cada 3 segundos."
painel
pausa

# ---------- Passo 0: garantir uma chamada aberta ----------
# O POST /chamadas do app precisa de uma chamada aberta no bairro (senão devolve 422).
if ! curl -fs "$BASE/api/bairros/$BAIRRO_ID/painel" >/dev/null; then
  titulo "Nenhuma chamada aberta: abrindo um treino de 20 minutos"
  curl -s -X POST "$BASE/api/bairros/$BAIRRO_ID/chamadas" \
    -H 'Content-Type: application/json' \
    -d '{"tipo":"Treino","duracaoMinutos":20}' | mostrar_json
fi

# ---------- (a) O app manda "Preciso de ajuda" com dado sensível ----------
titulo "(a) POST /chamadas: \"Preciso de ajuda\" com CPF, RG, senha, doença e nome"
OBSERVACAO='Sra. Maria Souza, CPF 123.456.789-00, RG 12.345.678-9, senha: abc12345. Ela é diabética e está sozinha.'
nota "Observação enviada: $OBSERVACAO"
CRIADA_EM="$(date -u +%Y-%m-%dT%H:%M:%S.000Z)"
curl -s -X POST "$BASE/chamadas" \
  -H 'Content-Type: application/json' \
  -d "{\"id\":\"demo-a-$SUFIXO\",\"ruaId\":\"$RUA_ID\",\"status\":\"preciso_ajuda\",\"pessoas\":1,\"observacao\":\"$OBSERVACAO\",\"criadaEm\":\"$CRIADA_EM\"}" | mostrar_json
nota "A resposta acima já volta sem o CPF, o RG, a senha, o nome e a doença."
nota "No terminal da API: a linha \"Pedido de ajuda na rua $RUA_ID...\" saiu com os dados escondidos."
painel
pausa

# ---------- (b) JSON com chaves extras (exemplo do professor) ----------
titulo "(b) JSON com chaves que a API não conhece: documento e senha"
nota 'Corpo: {"idLocal":...,"situacao":"TudoBem","documento":"123.456.789-00","senha":"abc12345"}'
curl -s -X POST "$BASE/api/chamadas/2/respostas" \
  -H 'Content-Type: application/json' \
  -d "{\"idLocal\":\"demo-b-$SUFIXO\",\"ruaId\":$RUA_ID,\"situacao\":\"TudoBem\",\"documento\":\"123.456.789-00\",\"senha\":\"abc12345\"}" | mostrar_json
nota "O interceptador de entrada reconheceu o CPF (mesmo com dígito verificador inválido) e a senha pelo nome das chaves e marcou os dois."
painel
pausa

# ---------- (c) Dado legado: gravado direto no SQLite ----------
titulo "(c) Dado legado: gravado direto no SQLite, sem passar pela API"
if ! command -v sqlite3 >/dev/null 2>&1 || [ ! -f "$BANCO" ]; then
  echo "Pulando: precisa do comando sqlite3 e do banco em $BANCO (use ELO_DB=/caminho/elo.db)." >&2
else
  CHAMADA_ID="$(curl -s "$BASE/api/bairros/$BAIRRO_ID/painel" | sed -n 's/.*"chamada":{"id":\([0-9]*\).*/\1/p')"
  CHAMADA_ID="${CHAMADA_ID:-2}"
  curl -s -X POST "$BASE/api/chamadas/$CHAMADA_ID/respostas" \
    -H 'Content-Type: application/json' \
    -d "{\"idLocal\":\"demo-c-$SUFIXO\",\"ruaId\":$RUA_ID,\"situacao\":\"AguaSubindo\"}" >/dev/null

  nota "sqlite3: UPDATE Respostas SET Observacao = 'CPF 123.456.789-00, vizinha diabética' ..."
  sqlite3 "$BANCO" "UPDATE Respostas SET Observacao = 'CPF 123.456.789-00, vizinha diabética' WHERE IdLocal = 'demo-c-$SUFIXO';"
  echo "No banco, o dado está lá, em texto puro:"
  sqlite3 "$BANCO" "SELECT IdLocal, Observacao FROM Respostas WHERE IdLocal = 'demo-c-$SUFIXO';"

  echo
  echo "Agora a API lê a mesma resposta (GET /api/chamadas/$CHAMADA_ID/respostas):"
  if [ "$TEM_JQ" = 1 ]; then
    curl -s "$BASE/api/chamadas/$CHAMADA_ID/respostas" | jq ".[] | select(.idLocal == \"demo-c-$SUFIXO\") | {idLocal, observacao}"
  else
    curl -s "$BASE/api/chamadas/$CHAMADA_ID/respostas"; echo
  fi
  nota "A guarda de saída (resposta HTTP) barrou o dado antes de ele sair. Esse é o evento vermelho de \"resposta da API\"."
  painel
fi

titulo "Fim. Abra $BASE/risco-cidadao.html para ver o farol, o índice e a mensagem ao cidadão."

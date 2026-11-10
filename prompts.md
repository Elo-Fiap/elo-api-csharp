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
- `src/Elo.Application/ContratoDoApp/ContratoDoAppService.cs` (como o app e a régua conversam com a API).

Se usarem IA para mudar algo depois, acrescentem o prompt aqui.

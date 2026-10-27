# Plano — Balão de permissão no mascote (Bloco 3)

**Data:** 27/10/2026 · a partir do brainstorm com o Victor (1 rodada) · ideia vinda do estudo do
clawd-on-desk (ver `PLANO-mascote.md`, §7)
**Status:** executado em 27/10 (sem `/plan`: o Victor pediu para executar os três blocos direto)

## §1 O que é

Quando o Claude Code pede permissão para usar uma ferramenta, um balão aparece sobre o bichinho
daquela sessão com o que o Claude quer fazer (comando, arquivo, endereço) e os botões
**Permitir**, **Sempre permitir**, **Negar** e **Abrir**. O pedido normal (no VS Code ou no
terminal) continua valendo ao mesmo tempo; a primeira resposta, em qualquer um dos dois, vale.
É a parte sensível do mascote: ele passa a decidir permissões do Claude.

## §2 Decisões

| # | Decisão | Motivo |
|---|---|---|
| 1 | Hook de **comando** no `PermissionRequest`: `Claudinho.exe --permissao`, que espera a resposta do balão e a escreve para o Claude | Com o mascote fechado ele sai em silêncio e vale só o pedido normal. Descartado: hook `http`, que registraria "erro de hook" a cada pedido com o mascote fechado |
| 2 | Botões: **Permitir**, **Sempre permitir**, **Negar** e **Abrir** (traz a janela da sessão) | Pedido do Victor ("Sempre permitir" incluído). "Abrir" era sugestão da rodada e não foi contestada |
| 3 | "Sempre permitir" aplica a **sugestão do próprio Claude** (`permission_suggestions`), a mesma do "não perguntar de novo" do pedido normal, onde ele sugerir gravar; o botão mostra a regra e o lugar. Sem sugestão, o botão não aparece | O mascote não inventa regra própria; usa a que o Claude já oferece |
| 4 | **Sem destaque** de comando perigoso e **sem confirmação dupla** | Decisão do Victor |
| 5 | **Sem atalhos de teclado** | Decisão do Victor |
| 6 | Todos os pedidos de ferramenta viram balão, **menos a aprovação de plano** (`ExitPlanMode`), que fica só no pedido normal | O balão não tem como mostrar um plano inteiro |
| 7 | Com **"Não perturbe"**, mascote **escondido** ou **fechado**: sem balão, o mascote não decide nada | O pedido normal segue sozinho |
| 8 | Ao **negar**, o Claude recebe uma mensagem dizendo que foi negado no mascote e **continua** a resposta | Quem quiser parar tudo usa o Esc |
| 9 | **Nenhum endereço do servidor aprova nada**: a decisão só sai de um clique no balão | Qualquer site aberto no navegador consegue mandar requisições para 127.0.0.1; um endereço de "permitir" seria uma porta aberta |
| 10 | O balão fecha quando: o Claude derruba a conexão do hook (você respondeu no pedido normal), chega a mesma ferramenta já executada (`PostToolUse`/`PostToolUseFailure` com o mesmo nome e entrada), a resposta acaba (`Stop`/`StopFailure`), vem prompt novo, a sessão termina ou o Claude fecha | Duas garantias independentes para nunca ficar um balão órfão |
| 11 | Um balão por vez; pedidos simultâneos esperam em fila | Pilha de balões ficou para depois |

## §7 Andamento

| Etapa | Resultado |
|---|---|
| Construção (`src/Permissao.cs`, `src/Balao.cs`; `Servidor.cs`, `Program.cs`, `JanelaMascote.cs`) | build ok; foto do balão conferida |
| Teste `testes/testar-permissao.ps1` (Automação de Interface do Windows, sem mexer no mouse) | 1ª rodada: 21 conferências verdes |
| Revisor (só leitura, sessão nova) | nada que bloqueie; confirmou que nenhum caminho aprova sem clique. 4 "deveria corrigir" e 4 sugestões, todas aplicadas (ver §8) |
| Teste depois das correções | **TUDO VERDE, 27 conferências**; `testar-eventos.sh` **TUDO VERDE** (sem regressão) |
| Hook ligado no `~/.claude/settings.json` | `PermissionRequest`: o aviso async de antes + `Claudinho.exe --permissao` (espera, timeout 600 s) |
| Falta (só dá com o Victor) | (1) num pedido real no VS Code, ver se o pedido normal aparece ao mesmo tempo que o balão e se responder num fecha o outro; (2) clique real de mouse nos botões (o teste usa a Automação de Interface, não o mouse) |

## §8 Decisões na execução

| O quê | Por quê | Custo se estiver errado |
|---|---|---|
| `AskUserQuestion` também sem balão (além do `ExitPlanMode`) | Achado do revisor: "Permitir" aprovaria a pergunta sem a resposta do Victor | — |
| Requisição com `Origin` ou com `Host` fora de 127.0.0.1/localhost recebe 403, em todas as rotas | Achado do revisor: um site aberto no navegador criava balão falso (não aprovava nada, mas confundia e ocupava a fila); `Host` cobre DNS rebinding | Um cliente legítimo que mande `Origin` seria recusado (os hooks não mandam) |
| Botões desligados nos primeiros 0,5 s | Achado do revisor: o balão aparece de surpresa sobre a barra de tarefas; um clique que já ia para lá cairia em "Permitir". Não é confirmação dupla (decisão 4 mantida) | Meio segundo a mais para quem já ia clicar |
| Comando cortado em 400 caracteres com aviso "Texto cortado", dica do mouse com até 3.000 | Achado do revisor: não dava para ver que havia mais além do corte | — |
| Cada pedido espera numa thread própria; fila limitada a 20 | Achado do revisor: o pedido prendia uma thread do ThreadPool por até 590 s, a mesma que atende os eventos da animação | Acima de 20 pedidos, os novos ficam só no pedido normal |
| Pedido cancelado quando a sessão dele **saiu** da lista (não quando "não está" nela) | Achado do revisor: um pedido de sessão ainda desconhecida era cancelado quando outra sessão caía | — |
| "Abrir" procura a sessão na hora do clique e avisa se não sabe a janela | Achado do revisor: com a sessão desconhecida, o botão não fazia nada | — |
| Mensagem do "Negar": "O usuário negou esta ação pelo Claudinho, o mascote do Claude na barra de tarefas." | Sem o nome do Victor, para o mascote servir em outro PC (decisão 8 mantida no sentido) | — |
| `/estado` mostra `balao`, `balao_armado` e `fila` | Teste sem depender de relógio: achar o botão pela automação pode levar mais que 0,5 s | — |
| Teste em PowerShell salvo em UTF-8 com BOM | Sem o BOM o PowerShell 5.1 lê na página antiga e o "—" vira aspa | — |

## Ideias para depois

- Pilha de balões quando duas sessões pedem ao mesmo tempo.
- Resumo da mudança (linhas tiradas e colocadas) para edição de arquivo.
- Atalhos de teclado (o Victor não quis na v1).
- Destaque de comando perigoso (o Victor não quis na v1).

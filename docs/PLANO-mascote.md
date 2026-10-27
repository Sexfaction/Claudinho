# Plano — Claudinho, o mascote do Claude na barra de tarefas

**Data:** 26/10/2026 · a partir do brainstorm com o Victor (3 rodadas)
**Status:** executado em 27/10 (sem `/plan`: o Victor pediu para executar direto após o brainstorm)

## §1 O que é

Um bichinho em pixel art (inspirado no Clawd, o mascote laranja do Claude) que fica em cima da
barra de tarefas do Windows e reage ao que cada sessão do Claude Code está fazendo: acorda quando
recebe um prompt, fica animado enquanto trabalha, pula quando precisa do Victor, comemora ao
terminar e dorme. Um bichinho por sessão. Os eventos vêm dos *hooks* do Claude Code (comandos que
ele roda sozinho a cada evento), então funciona em toda sessão: VS Code, terminal e app desktop.
Uso pessoal; não se distribui (a arte é do mascote da Anthropic).

## §2 Decisões

| # | Decisão | Motivo |
|---|---|---|
| 1 | Eventos vêm de **hooks** no `~/.claude/settings.json` global, que fazem um `curl` para `127.0.0.1`, sempre com sucesso e tempo máximo de 1 s | Caminho oficial e instantâneo. Descartado: ler os transcripts em `~/.claude/projects/` (atraso, formato instável). O hook nunca pode travar nem bloquear o Claude: o `PreToolUse` roda antes de toda ferramenta, e sair com código 2 bloqueia a ferramenta |
| 2 | Janela em **C# WinForms sobre o .NET Framework 4.8**, compilada com o .NET 8 SDK já instalado | O 4.8 já vem dentro de todo Windows 10/11: o `.exe` (~100 KB) roda em qualquer PC sem instalar nada, com ~15–25 MB de RAM. Descartados: Python+tkinter (precisa de Python no outro PC), .NET 8 (precisa do runtime instalado, ou de um `.exe` de 60+ MB), Electron (~150 MB de RAM), Tauri (exige instalar Rust). Pedido do Victor: o mais leve e compatível, quantidade de código não importa |
| 3 | Os eventos entram por um servidorzinho TCP próprio em `127.0.0.1` (HTTP mínimo), não pelo `HttpListener` | O `HttpListener` do Windows pode exigir permissão de administrador para abrir a porta; o TCP puro em 127.0.0.1 não exige. Nada sai para a internet |
| 4 | **Um bichinho por sessão**, até 4 lado a lado; some quando a sessão fecha ou depois de um tempo dormindo | Cada sessão tem o seu estado. O `SessionEnd` nem sempre chega (VS Code fechado de repente), por isso o tempo de corte |
| 5 | **Todas as sessões**, sem filtro por origem (VS Code, terminal, app) | Decisão do Victor: deixar funcionar em todas. O VS Code é o critério de teste da v1 |
| 6 | Estados da v1: **dormindo, acordando, trabalhando, precisa de você, terminou** (comemora ~2 s e dorme) | Mapa: `UserPromptSubmit` → acordando/trabalhando; `PreToolUse`/`PostToolUse` → trabalhando; `Notification` → precisa de você; `Stop` → terminou → dormindo; `SessionStart`/`SessionEnd` → aparece/some. Não existe evento de "digitando a resposta", então trabalhando cobre do prompt ao fim |
| 7 | Sprites desenhados **no próprio código** (grade de pixels ampliada 3–4×) | Ajustar a arte vira editar uma grade de caracteres; pixel art sem borda suavizada combina com a transparência por cor-chave do WinForms |
| 8 | Projeto em **`C:\claudinho`** (nasceu como `C:\mascote-claude`; renomeado em 27/10, ver §8), plano em `docs/`. **Sem git/GitHub na v1** | Fora do Google Drive: app rodando o tempo todo em pasta sincronizada já deu problema em outro projeto. Versionar exige pôr o repositório no `/commit` (commit fora dele é proibido); o Victor deixou para depois |
| 9 | **O `SessionStart` abre o mascote** se ele não estiver rodando | Nada na inicialização do Windows: o bichinho aparece quando há Claude trabalhando |
| 10 | Interação: **arrastar** para posicionar (a posição fica salva), **botão direito** com menu (dormir todos, esconder 1 h, sair); o clique não atravessa o bichinho; só o monitor principal | Precisa receber o arrasto. A janela se reafirma no topo de tempos em tempos (a barra de tarefas também é "sempre no topo") e se esconde em tela cheia |
| 11 | Sessão sai 30 min depois de dormir ou no `SessionEnd`; **sem sessão nenhuma, o programa fecha** (o próximo `SessionStart` reabre) | Zero consumo quando o Claude não está em uso. Descartado: bichinho dormindo para sempre na barra |
| 12 | Ao passar o mouse, **dica com o nome da pasta** do projeto da sessão | Identifica cada bichinho sem rótulo fixo poluindo a barra |
| 13 | ~~Hooks só valem em sessão aberta depois da mudança~~ — **errado, ver §8**: o Claude Code recarregou os hooks sozinho | Suposição do brainstorm, desmentida na execução |

## Ideias para depois

- Animação por tipo de ação: editando código (Edit/Write), terminal (Bash), lendo/procurando (Read/Grep), web, subagent. O hook já manda o nome da ferramenta, então encaixa sem refazer nada.
- Balão com a ação atual e há quanto tempo está trabalhando.
- Som discreto ao terminar.
- Suporte a vários monitores.
- Versionamento: `git init` em `C:\claudinho`, pôr o repositório na lista do `/commit` e, se o Victor quiser, repositório privado no GitHub.

## §7 Andamento

Execução inline, numa sessão só (sem §3–§6: o `/plan` foi pulado a pedido do Victor).

| Etapa | Resultado |
|---|---|
| Janela + arte + servidor (`src/`, `compilar.cmd`) | compila sem aviso; `bin\Claudinho.exe` de ~25 KB. Folha de estados (`--retrato`) conferida nos fundos escuro e claro; corpo alongado em 1 linha e cores do "Z" e dos pontos trocadas por tons médios depois da 1ª folha |
| Teste de ponta a ponta (`testes/testar-eventos.sh`) | **TUDO VERDE**, 16 conferências: abertura automática, os 5 estados, transições por tempo, aviso de espera ignorado, evento de 300 KB, 2 sessões, `SessionEnd`, fechamento sozinho em 65 s, hook com o mascote fechado sai com 0 |
| Consumo (`--demo`, 4 bichinhos animando) | 22,5 MB de memória própria, 0,9% de um núcleo |
| Hooks no `~/.claude/settings.json` | 7 eventos; os comandos gravados rodados pelo `cmd` e pelo Git Bash, ambos com saída 0 e estado conferido em `GET /estado`. Cópia de antes em `docs/settings.json.antes-dos-hooks.bak` |
| Sessão real | a própria sessão do app desktop que construiu o mascote passou a aparecer nele ("CodingOS — trabalhando") logo depois da mudança no `settings.json`, sem reiniciar. Isso derrubou o "fechou sozinho" do 2º teste (a sessão real o mantinha aberto); o teste agora pula essa conferência quando há sessão real e limpa a sessão de teste. 3ª rodada: **TUDO VERDE** |
| Ajustes de 27/10 (pedido do Victor) | (1) topo por evento: o mascote sumia e voltava ao trocar de janela (a barra de tarefas o cobria até a conferência de 1 s); agora volta no aviso de "foco mudou" do Windows, a conferência virou 2 s e só age se algo cobriu; opção "Sempre no topo" no menu. (2) Clicar traz a janela da sessão. Bateria: TUDO VERDE. Prova real do clique pelo `GET /estado`: sessão do VS Code do Victor → processo `Code` (15604), janela "… - CodingOS - Visual Studio Code"; hook disparado do app desktop → janela "Claude" |
| Falta (ajustes de 27/10) | ver com os olhos do Victor: sumiço ao trocar de janela, o clique trazendo a janela, a opção do menu |
| Estudo do clawd-on-desk (27/10) | repositório de referência (Electron, ~1.700 arquivos, AGPL-3.0, arte "todos os direitos reservados"): ideias aproveitadas, nada de código nem arte copiados. Três blocos aprovados pelo Victor: 1 (async, estados novos, processo vivo, som), 2 (medidor de cota), 3 (balão de permissão, via `/brainstorm`) |
| Blocos 1 e 2 (27/10) | `async: true` em todos os hooks (menos `SessionEnd`); 7 eventos novos; estados erro, sem cota, compactando e subagentes; trava contra eventos fora de ordem; sessão sai quando o processo do Claude Code some; bipes sintetizados com intervalo de 10 s; "Não perturbe" e "Som" no menu; preferências num arquivo só; barra de status repassa a cota e escreve a linha do terminal; medidor de 2 barras. Bateria ampliada: **TUDO VERDE, 36 conferências**. Folha de estados conferida nos fundos escuro e claro (vassoura passada para o lado direito depois da 1ª folha). Eventos reais desta sessão chegando com os hooks novos, sem reiniciar |

## §8 Decisões na execução

| O quê | Por quê | Custo se estiver errado |
|---|---|---|
| Compilar com o `csc.exe` que vem no Windows (C# 5), não com o .NET 8 SDK | O SDK precisaria baixar o pacote de referência do .NET 4.8 (o pacote de desenvolvimento não está instalado); o `csc` do Windows não baixa nada e compila em qualquer PC | Sintaxe antiga do C# (sem `$"..."`, `?.`); trocar depois é só um `.csproj` |
| `UserPromptSubmit` também abre o mascote, não só o `SessionStart` | O mascote fecha sozinho sem sessões; sem isso, um prompt numa sessão antiga não o traria de volta. Por isso o "Fechar" do menu diz "volta no próximo prompt" | — |
| O mascote é aberto fora do *job* do processo do hook (`CREATE_BREAKAWAY_FROM_JOB`) | O Windows mata os filhos quando o processo-pai do job fecha: fechar a sessão que o abriu levaria o mascote junto | Se o job não permitir, abre do jeito normal; o próximo prompt reabre |
| `curl --connect-timeout 0.2` nos hooks | No Windows, conectar numa porta fechada de 127.0.0.1 leva ~1 s; sem o limite, cada ferramenta esperaria 1 s com o mascote fechado. Medido: ~0,3 s fechado, ~0,1 s aberto | — |
| Ignorar a `Notification` de "esperando sua resposta" (`idle_prompt`) | Chega ~1 min depois do `Stop` e acordaria um bichinho que acabou de dormir; só pedido de permissão e pergunta viram "precisa de você" | — |
| `AskUserQuestion` e `ExitPlanMode` no `PreToolUse` também viram "precisa de você" | São perguntas ao Victor que não passam pela `Notification` de permissão | — |
| Trabalhando sem evento há **10 min** vira dormindo | O `Stop` não chega quando a resposta é interrompida com Esc | Um comando de mais de 10 min sem outra ferramenta faz o bichinho dormir antes da hora |
| `GET /estado`, `--demo` e `--retrato` | Evidência para o teste (estado real, não suposição) e para conferir a arte | — |
| Hooks pegam sem reiniciar a sessão (corrige a decisão 13) | Observado: o Claude Code recarregou o `settings.json` e esta sessão passou a mandar eventos | — |
| Topo por evento (`SetWinEventHook` de foco) + repique em 200 ms + conferência de 2 s que só age se outra janela estiver sobre o corpo do bichinho (`WindowFromPoint`) | Ficar fixo acima da barra exigiria `uiAccess` (programa assinado em Arquivos de Programas); reagir ao evento é o que os outros programas fazem | Algo que cubra o mascote sem trocar o foco fica por cima até 2 s |
| Clique: o `--hook` sobe pelos processos-pai até o primeiro com janela visível (pulando o Explorer) e manda no cabeçalho `X-Processo-Janela`; no clique, janela desse processo com a pasta no título, senão a usada por último; sem processo, qualquer janela (menos Explorer) com a pasta no título | O VS Code põe a pasta no título; vários VS Code são um processo só, por isso o título desempata | Sessão sem prompt desde que o mascote abriu acha a janela só pelo título (ex.: sessão do app com a pasta "CodingOS" aberta também no VS Code vai para o VS Code até o próximo prompt) |
| Clique × arrasto: menos de 4 px entre apertar e soltar é clique | O mesmo botão faz as duas coisas | — |
| Teste: "fechou sozinho" vira "pulado" quando chega sessão real durante a espera | Com o Victor usando o Claude ao mesmo tempo, o mascote tem motivo para não fechar | — |
| "Precisa de você" pelo `PermissionRequest` (hook async, sem decidir nada), mantendo a `Notification` | A `Notification` de permissão só dispara depois de ~6 s de espera (documentação oficial) | — |
| Trava de atraso: com o turno fechado (`Stop`/`StopFailure`/interrupção), eventos de ferramenta nos 3 s seguintes são ignorados | `async: true` faz os hooks chegarem fora de ordem; um `PostToolUse` depois do `Stop` deixaria o bichinho "trabalhando" por 10 min | Uma continuação legítima nos 3 s depois do `Stop` (hook de Stop que manda continuar) aparece com atraso |
| `SessionEnd` sem `async` | A documentação diz que hooks em segundo plano podem ser encerrados no fim da sessão | ~0,1 s no fechamento da sessão |
| Erro de ferramenta aparece pelo menos 1,5 s mesmo com o Claude seguindo | Sem isso o "X" piscaria e sumiria no próximo `PreToolUse` | — |
| "Sem cota" (`StopFailure` com `error: rate_limit`) fica até o próximo prompt (no máximo 5 h) | É a informação que importa: a sessão parou e só volta quando a janela renovar | — |
| "Precisa de você" não expira por tempo | O Claude está mesmo esperando, por quanto tempo for | Se o pedido for respondido sem gerar evento, fica até o próximo |
| Processo do Claude Code = primeiro ancestral chamado `claude.exe`; guarda a hora de início junto com o número | O app desktop também se chama Claude.exe, mas fica acima do CLI; o Windows reaproveita números de processo | Sessão vista só por `curl` (sem prompt desde que o mascote abriu) não tem o processo e sai só por `SessionEnd`/tempo |
| Som sintetizado no código (onda quadrada, 8 bits), ligado por padrão, no máximo um a cada 10 s; "Não perturbe" não é gravado | Sem arquivo de áudio para distribuir; o intervalo é o do clawd-on-desk | Se incomodar, o menu desliga e fica salvo |
| Preferências num `preferencias.txt` só, lendo os arquivos soltos da 1ª versão sem apagá-los | Três arquivos soltos (posição, topo, som) já estavam virando bagunça | — |
| Barra de status: o mascote registra toda vez que ela roda (`barra_rodou` no `/estado`) | A documentação não diz se a barra roda no VS Code e no app desktop; o registro separa "não roda" de "roda sem cota" | — |
| Teste: cada conferência inclui a pasta da sessão de teste | As sessões reais do Victor ficam no mascote ao mesmo tempo e davam falso positivo | — |
| Nome **Claudinho** no projeto inteiro (27/10): pasta `C:\claudinho`, `Claudinho.exe`, dados em `%LOCALAPPDATA%\Claudinho` (pasta antiga movida), repositório público `Sexfaction/Claudinho` | Pedido do Victor, feito antes do primeiro commit para não deixar histórico com dois nomes. Público por escolha dele; o README diz que é projeto de fã, sem ligação com a Anthropic | — |
| Servidor descarta o BOM no começo do JSON | O PowerShell põe esse caractere ao repassar texto; quebrava a leitura no teste pelo `cmd` | — |

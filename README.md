# Claudinho

Um bichinho em pixel art sobre a barra de tarefas do Windows que reage às sessões do Claude Code
(VS Code, terminal, app desktop): dorme, trabalha, avisa quando precisa de você, comemora quando
termina e deixa aprovar pedidos de permissão por um balão. Um bichinho por sessão.

> **Projeto de fã, não oficial.** O personagem Clawd pertence à [Anthropic](https://www.anthropic.com);
> a arte daqui é desenhada à mão em grades de pixels, inspirada nele, sem uso comercial. Este
> projeto não tem ligação com a Anthropic nem é endossado por ela.

| Estado | Quando | Animação | Som |
|---|---|---|---|
| dormindo | sessão parada, ou depois de terminar | olhos fechados, "Z" subindo | — |
| acordando | você mandou um prompt | pulinho (0,6 s) | — |
| trabalhando | entre o prompt e o fim da resposta | passinhos e três pontos; com subagentes, uma bolinha girando por subagente (até 3) | — |
| precisa de você | pedido de permissão (na hora, pelo `PermissionRequest`), pergunta (AskUserQuestion) ou plano para aprovar | pula acenando, "!" amarelo | ping duplo |
| terminou | a resposta acabou | braços para cima e check verde por 2 s | dois bipes subindo |
| erro | ferramenta falhou (volta a trabalhar em 2,5 s) ou a resposta morreu por erro (dorme em 5 s) | treme, "X" vermelho | — |
| sem cota | a resposta parou por limite da assinatura (`rate_limit`) | dormindo com bateria vazia piscando, até o próximo prompt | dois bipes descendo |
| compactando | o Claude está resumindo a conversa | varre com uma vassoura | — |

**Balão de permissão:** quando o Claude pede permissão para uma ferramenta, um balão aparece
sobre o bichinho da sessão com o que ele quer fazer e os botões **Permitir**, **Sempre
permitir** (aplica a regra que o próprio Claude sugere, a mesma do "não perguntar de novo";
o balão mostra qual regra e onde ela fica gravada; sem sugestão, o botão não aparece),
**Negar** (o Claude recebe o aviso e continua) e **Abrir** (traz a janela da sessão). O pedido
normal continua valendo ao mesmo tempo: a primeira resposta, em qualquer um dos dois, vale, e
o balão fecha sozinho. O quadro mostra o comando, o arquivo ou o endereço; para Write/Edit é só
o caminho do arquivo, sem o conteúdo. Texto comprido é cortado com aviso, e a dica do mouse mostra
tudo. Os botões só valem meio segundo depois de o balão aparecer, para um clique que ia para a
barra de tarefas não cair em "Permitir". Com "Não perturbe", mascote escondido ou fechado, na
aprovação de plano e em pergunta com opções (AskUserQuestion), não há balão. Nenhuma requisição
ao servidor aprova nada: só o clique no balão.

**Medidor de cota:** duas barrinhas à esquerda dos bichinhos, janela de 5 horas e semana,
verdes, amarelas a partir de 70% e vermelhas a partir de 90%. Passar o mouse mostra os números
e quando cada janela renova. Só aparece depois que o Claude Code mandou a cota (assinatura
Pro/Max, a partir da primeira resposta da sessão).

Passar o mouse sobre um bichinho mostra a pasta da sessão, o estado e o detalhe do erro.
**Clicar** traz para frente a janela onde a sessão roda (VS Code, terminal, app do Claude).
Arrastar muda o lugar (fica salvo). Botão direito: dormir todos, não perturbe (todos aparecem
dormindo e sem som, até desligar ou o mascote fechar), som (liga/desliga, fica salvo), sempre no
topo (liga/desliga, fica salvo), esconder por 1 hora, fechar (volta no próximo prompt).

**Por que "sempre no topo" não é fixo:** a barra de tarefas também é sempre no topo, e entre duas
janelas assim fica por cima a ativada por último. Um programa comum não tem como ficar acima dela
de vez (só programas com assinatura digital instalados em Arquivos de Programas). O mascote
escuta o aviso do Windows de "foco mudou" e volta para cima no mesmo instante.

## Como funciona

1. **Hooks** no `~/.claude/settings.json` (comandos que o Claude Code roda a cada evento) mandam
   o JSON do evento para `127.0.0.1:47821`, só dentro da máquina. Todos com `async: true`: o
   Claude dispara e segue, sem esperar (menos o `SessionEnd`, que roda uma vez e não pode ser
   cortado no fechamento da sessão).
   - `SessionStart` e `UserPromptSubmit` chamam `bin\Claudinho.exe --hook`, que entrega o evento
     e **abre o mascote** se ele estiver fechado. Ele também sobe pela árvore de processos e
     manda dois dados: o processo do **Claude Code** (quando ele fecha, a sessão sai do mascote
     em até 2 s) e o primeiro programa **com janela** (VS Code, terminal, app: é para lá que o
     clique leva). Até o primeiro prompt depois de o mascote abrir, a sessão não tem esses
     dados: o clique procura uma janela com o nome da pasta no título, e a saída depende do
     `SessionEnd` ou dos tempos limite.
   - Os demais usam `curl`: `PreToolUse`, `PermissionRequest`, `PostToolUse`,
     `PostToolUseFailure`, `SubagentStart`, `SubagentStop`, `PreCompact`, `PostCompact`,
     `Notification`, `Stop`, `StopFailure`, `SessionEnd`.
   - Todos saem sempre com 0: o mascote nunca atrapalha nem bloqueia o Claude.
   - Como rodam em segundo plano, os eventos podem chegar fora de ordem: uma ferramenta que
     chega até 3 s depois do `Stop` é tratada como atraso do mesmo turno e ignorada.
2. **Permissão:** o `PermissionRequest` tem também um hook de comando que **espera** (não é
   async, até 600 s): `bin\Claudinho.exe --permissao` entrega o pedido em `POST /permissao`, o
   servidor segura a conexão até o clique no balão e devolve a decisão no formato do hook.
   Sem decisão (mascote fechado, não perturbe, respondido no pedido normal, a ferramenta já
   rodou, a resposta acabou), o hook não escreve nada e o Claude segue com o pedido normal.
3. **Barra de status** (`statusLine` no mesmo arquivo): `bin\Claudinho.exe --statusline` recebe
   o JSON da barra, repassa a cota (`rate_limits`) ao mascote e escreve a linha do terminal:
   `[Opus] · contexto 12% · cota 5h 24% · semana 41%`. Nenhuma chamada extra à Anthropic.
4. **`bin\Claudinho.exe`** (C# WinForms, .NET Framework 4.8, que já vem no Windows) recebe os
   eventos, guarda o estado de cada sessão e desenha os bichinhos numa janela transparente,
   sempre no topo, que não rouba o foco e some em tela cheia.
5. Sem sessão nenhuma por 1 minuto, o programa fecha sozinho. Sessão dormindo há 30 min sai.
   Trabalhando sem evento há 10 min vira dormindo: o `Stop` não chega quando você interrompe
   com Esc (se uma ferramenta estava rodando, o `PostToolUseFailure` com `is_interrupt` avisa e
   ele dorme na hora).

| Arquivo | O que é |
|---|---|
| `src/Program.cs` | entrada; modos `--hook`, `--permissao`, `--statusline`, `--demo`, `--retrato` |
| `src/Servidor.cs` | TCP puro em 127.0.0.1: `POST /evento`, `POST /permissao` (segura a conexão até o balão), `GET /estado`; recusa requisição com `Origin` (navegador) ou `Host` que não é esta máquina |
| `src/Sessoes.cs` | regras: qual evento leva a qual estado, o que muda com o tempo, a cota |
| `src/Sprites.cs` | a arte, em grades de caracteres, e o medidor de cota |
| `src/JanelaMascote.cs` | a janela: animação, clique, arrastar, menu, dica, topo da barra |
| `src/Janelas.cs` | acha o Claude Code e o programa dono da janela; traz a janela para frente |
| `src/Permissao.cs` | um pedido de permissão esperando resposta; o JSON de decisão |
| `src/Balao.cs` | o balão com os botões |
| `src/Som.cs` | bipes sintetizados no próprio código, no máximo um a cada 10 s |
| `src/Preferencias.cs` | posição, som e sempre no topo, num arquivo só |
| `src/Win32.cs`, `src/Registro.cs` | chamadas ao Windows; log de erros |
| `testes/testar-eventos.sh` | teste de ponta a ponta com os mesmos comandos dos hooks |
| `testes/testar-permissao.ps1` | teste do balão: aperta os botões pela Automação de Interface do Windows, sem mexer no mouse (arquivo em UTF-8 com BOM, senão o PowerShell 5.1 lê errado) |

Dados do mascote (`preferencias.txt` e `erros.log`) ficam em `%LOCALAPPDATA%\Claudinho`.

## Comandos

```
compilar.cmd                                  compila para bin\Claudinho.exe (feche o mascote antes)
bin\Claudinho.exe --demo                        mostra 4 estados e o medidor sem precisar do Claude
bin\Claudinho.exe --retrato folha.png           grava todos os estados num PNG
bash /c/claudinho/testes/testar-eventos.sh   teste de ponta a ponta (com o mascote fechado; bipa)
& C:\claudinho\testes\testar-permissao.ps1   teste do balão, no PowerShell (com o mascote fechado)
curl http://127.0.0.1:47821/estado            estado atual das sessões e a cota
```

## Instalar

Precisa de Windows 10 ou 11 (já trazem o .NET Framework 4.8 e o `curl`) e do Claude Code.

1. Baixar para `C:\claudinho` (os hooks apontam para esse caminho):
   `git clone https://github.com/Sexfaction/Claudinho.git C:\claudinho`
2. Rodar `compilar.cmd`: usa o compilador de C# que vem no Windows e não baixa nada.
3. Juntar o conteúdo de [`settings-exemplo.json`](settings-exemplo.json) ao
   `~/.claude/settings.json`. Se já houver um `"hooks"`, juntar os eventos dentro dele (dois
   `"hooks"` no mesmo arquivo fazem um apagar o outro). Se já houver uma `"statusLine"`, ela é
   substituída. Uma vírgula a mais ou a menos invalida o arquivo inteiro.
4. Mandar um prompt: o bichinho aparece no canto esquerdo da barra de tarefas. O Claude Code
   costuma recarregar os hooks sozinho; se não aparecer, reiniciar a sessão.

**Atenção:** o balão de permissão decide pedidos do Claude Code. Ele só aprova com um clique,
mas, se preferir não usá-lo, tire do `settings.json` a linha com `--permissao`.

## Desinstalar

Tirar o bloco `"hooks"` e a `"statusLine"` do `~/.claude/settings.json` e apagar `C:\claudinho`
e `%LOCALAPPDATA%\Claudinho`.

## Limitações conhecidas

- O medidor de cota só recebe dados de sessões que rodam a barra de status, o que hoje é só o
  terminal: o app desktop e a extensão do VS Code não a rodam. O aviso de "sem cota" funciona
  em todo lugar.
- O clique leva à janela do programa (VS Code, terminal, app), não à aba ou ao painel exato.
- Só o monitor principal.

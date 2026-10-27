# Plano — Animações novas do Claudinho

**Data:** 27/10/2026 · conversa com o Victor (sem `/brainstorm`, a pedido dele), proposta aprovada
com todas as respostas recomendadas
**Status:** os 8 estados refeitos em 27/10; falta a aprovação do Compactando e o commit

## §1 O que é

Repensar todas as animações para o bichinho **fazer** o que a sessão está fazendo, em vez de
mostrar um símbolo sobre a cabeça (três pontos, check, X, bateria saem). Cada estado ganha um
movimento de base e variações sorteadas, para não ficar em loop parado.

## §2 Decisões

| # | Decisão | Motivo |
|---|---|---|
| 1 | **Ação, não símbolo.** Só flutua algo quando é objeto na mão dele (placa) ou onomatopeia (o "Z" do ronco, a fila de "Zzz" do sono) | Pedido do Victor; o "Zzz" voltou a pedido dele |
| 2 | **Base + intervalos sorteados.** A sorte vem do tempo no estado, com um "baralho" por rodada (toda variação sai uma vez, em ordem embaralhada, nunca a mesma duas vezes seguidas) | Determinístico: a mesma hora desenha a mesma pose, sem guardar quadro; testável pela folha |
| 3 | **Vida de fundo:** respiração, piscadas irregulares, olhar para os lados | Menos estático |
| 4 | **Boca** aparece só durante a ação (gole, ronco, bocejo) | O Clawd não tem boca; fixa, mudaria o personagem |
| 5 | **Subagentes = mini-Claudinhos** em cima da cabeça, um por subagente (até 3); chegam caindo, saem num pulo com "pof" | Ele delegando. Substitui os malabares |
| 6 | **Compactando = pilha de papel** que ele achata pulando em cima | Mais literal que a vassoura |
| 7 | **Ação por ferramenta** (folhear ao ler, binóculo na web, digitar ao editar) fica para uma segunda rodada | Primeiro acertar o digitando |
| 8 | Acordar 0,6 → 1,2 s; comemorar 2 → 3 s | Cabe a ação inteira; é só visual |
| 9 | **Lata branca de energético** (tampa cinza, sem logo nem nome) | O repositório é público e a marca é de terceiros |
| 10 | Aprovação vendo no `--demo` e na folha do `--retrato`, estado por estado | Compilar leva segundos; prévia em HTML seria refeita no C# |

## §3 Estados

**Trabalhando.** Teclado cinza na frente (esconde as pernas), lata branca em pé à direita. Mãos
alternam no teclado (a tecla sob a mão acende), olhos para baixo. Ritmo em rajadas de digitação e
pausas curtas (olha para cima, tamborila com uma mão). Variações: **gole** (pega a lata; na
primeira de cada lata, "psst" ao abrir; inclina na boca, olhos fecham, "ahh", depois digita no
dobro da velocidade por 3 s; a cada 4 goles amassa a lata e joga para trás, e aparece outra),
**estala os dedos** (braços esticados, tremidinho), **pensando** (mão na cabeça, olhos para cima),
**errou e apagou** (martela a tecla do canto direito), **olha para você** (para, olha, pisca).
Subagentes: os minis da decisão 5.

**Dormindo.** Sentado e "derretido", olhos fechados (traço horizontal), respiração lenta, fila
de 3 "Z" que nasce maior perto da cabeça e diminui ao subir (ajuste do Victor). Variações:
baba (gota → fio → pinga → poça que some), ronco (infla, treme ao soltar, "Z" serrilhado),
bolha de ranho (cresce com a respiração, estoura, susto, volta a dormir), cabeceada, coçada.

**Acordando** (1,2 s). Susto (olhos abrem, pulinho), espreguiça, puxa o teclado.

**Compactando.** Pilha de folhas ao lado; ele pula em cima e ela achata 1 pixel por pulo; vira
bloquinho, caem folhas novas, recomeça.

**Precisa de você.** Plaquinha amarela com "!" no cabo, balançando, olhando para você, pulinhos.
Sem resposta: ~20 s bate o pé; ~60 s cruza os braços e tamborila, levantando a placa às vezes. A
placa fica abaixo das 3 primeiras linhas (ali abre o balão de permissão).

**Terminou** (3 s). Sorteia uma: pulo com confete, dancinha, brinde com a lata, soco no ar. Saída:
pálpebras pesadas, empurra o teclado, senta e dorme.

**Erro.** Ferramenta falhou: faísca no teclado, susto para trás com olhos "x x", balança a cabeça,
volta a digitar (variação: mão na testa). Resposta morreu: fumaça do teclado, olhos "x x", corpo
afunda, dorme.

**Sem cota.** Largado, lata tombada ao lado; a cada ~20 s vira a lata, sacode, cai uma gota,
espia dentro, suspira.

## Ideias para depois

- Interação entre bichinhos lado a lado (um encosta no vizinho dormindo, um olha o outro comemorar).
- Ação por ferramenta (decisão 7).

## §7 Andamento

| Etapa | Resultado |
|---|---|
| Trabalhando (27/10) | `src/Corpo.cs` (corpo por partes, sorteio fixo, piscada) e `src/Trabalhando.cs` (teclado, lata, 5 variações, minis). Malabares e três pontos saíram. Conferido quadro a quadro no `--filme` (novo), fundos escuro e claro: base, pausa, as 5 variações, gole com "psst", lata amassada e jogada, minis com 1, 2 e 3. Compila sem aviso |
| Trabalhando aprovado pelo Victor no `--demo` (27/10) | — |
| Dormindo (27/10) | `src/Dormindo.cs`; o corpo ganhou respiração (`Esticar`) e inclinação (`Inclina`). Sentado, braços caídos, respira a cada 3 s; 5 variações (baba, ronco, bolha, cabeceada, coçada). O "Z" fixo saiu. Conferido no `--filme`, fundos escuro e claro |
| Ajustes do Victor no Dormindo (27/10) | (1) olho fechado não parecia fechado: virou traço horizontal de 3 pixels, em todos os estados (piscada, gole, apagar); (2) volta o "Zzz", agora uma fila de 3 que nasce maior perto da cabeça e diminui ao subir; o ronco passou para o lado esquerdo |
| Dormindo aprovado pelo Victor (27/10) | — |
| Acordando (27/10) | `src/Acordando.cs`; `TempoAcordando` 600 → 1200 ms (teste ajustado: espera 1,5 s em vez de 1 s). Da pose do Dormindo: susto (pulinho, braços abertos), espreguiça bocejando com tremidinho, teclado entra pela esquerda e lata pela direita, termina na pose do Trabalhando. Conferido no `--filme` |
| Acordando aprovado pelo Victor (27/10) | — |
| Terminou (27/10) | `src/Terminou.cs`; `TempoComemorando` 2000 → 3000 ms (teste ajustado: espera 3,5 s). Olhos felizes "^" e braço com punho novos no corpo. 2 s de comemoração sorteada pela hora em que terminou (confete, dancinha, brinde, soco no ar) e 1 s de ida para o sono (piscada lenta, olhos fecham, empurra teclado e lata, senta na pose do Dormindo). Check verde saiu. `--filme` ganhou o argumento `fase`, para ver cada comemoração |
| Ajuste no Terminou (27/10) | o Victor não conseguiu ver as 4 comemorações (sorteio solto repete). Agora saem de um baralho pela contagem de respostas da sessão (`Comemoracoes`): em cada 4 respostas, as 4 aparecem. No `--demo`, o 3º bichinho repete a comemoração a cada 3,5 s passando pelas 4. Bateria: TUDO VERDE (desta vez com a conferência "fecha sozinho", sem sessão real aberta) |
| Terminou aprovado pelo Victor (27/10) | — |
| Precisa de você (27/10) | `src/PrecisaDeVoce.cs`. Até 20 s: placa amarela com "!" erguida na mão direita, balançando, dois pulinhos a cada 2 s, olhando para você. 20–60 s: placa fincada à esquerda, bate o pé, olha de um lado para o outro. Depois de 60 s: braços cruzados (faixa mais escura na barriga) com um dedo tamborilando; a cada 8 s ergue a placa por 1,5 s. A placa fica abaixo da linha 3 (o balão). O "!" solto saiu. `--demo`: o 3º bichinho passa pelas três fases e recomeça a cada 75 s. Bateria de eventos: TUDO VERDE. Teste do balão: falhou 2 vezes no 1º pedido ("balão não apareceu"), depois TUDO VERDE 2 vezes com o mesmo executável; a versão do último commit também passou no meio. Causa não provada; a suspeita é uma janela em tela cheia na frente naquele momento (o mascote não abre balão em tela cheia, de propósito) |
| Ajuste do Victor na placa (27/10) | o "!" encostava no topo e na base e se perdia no fundo escuro: a placa ganhou uma linha amarela acima e abaixo (5×6, "!" de 2 + vão + ponto). Erguida, começa na linha 3 (pulinho: 2, só a margem amarela sob o balão). Bateria: TUDO VERDE |
| Precisa de você aprovado pelo Victor (27/10) | — |
| Erro (27/10) | `src/Erro.cs`; olhos "x x" 3×3 novos no corpo (centrados uma linha abaixo, senão encostam no topo). Ferramenta falhou (2,5 s): faísca no teclado, susto com olhos "x x", balança a cabeça, volta a digitar; ou, sorteado, mão na testa (braço e mão em tom mais escuro). Resposta morreu (5 s): faísca, susto, fumaça da ponta esquerda do teclado, afunda 2 pixels, empurra o teclado de olhos fechados e senta na pose do Dormindo. "X" vermelho saiu. `--filme` ganhou o argumento "depois do erro"; `--demo` repete os dois erros. Bateria: TUDO VERDE |
| Erro aprovado pelo Victor (27/10) | — |
| Sem cota (27/10) | `src/SemCota.cs`. Largado no chão (corpo 2 abaixo, sem pernas, tombado 1, olhos pesados, respiração de 4 s), lata vazia deitada à direita. A cada 20 s (4 s de ação): abre os olhos para a lata, vira de ponta-cabeça sobre a boca aberta, sacode, cai uma gota até a boca, espia com a lata deitada no olho direito, suspira (enche e murcha 1 a mais). A bateria piscando saiu. Bateria: TUDO VERDE |
| Sem cota aprovado pelo Victor (27/10) | — |
| Compactando (27/10) | `src/Compactando.cs`. Em pé numa pilha de 6 folhas (listras branco/cinza-azulado com contorno cinza: sem ele o branco some na barra clara), 5 pulos achatam a pilha até 1; caem 5 folhas pelos lados, alternando, e a pilha cresce de novo levantando ele; ciclo de 4 s. A vassoura saiu. Com isso o desenho antigo por grade saiu todo de `Sprites.cs` (fica a célula, as cores, o despacho por estado e o medidor). Bateria de eventos: TUDO VERDE; teste do balão: TUDO VERDE |
| Falta (Compactando) | o olho do Victor no `--demo`; depois, commit pelo `/commit` |

## §8 Decisões na execução

| O quê | Por quê | Custo se estiver errado |
|---|---|---|
| Os olhos nunca sobem uma linha: "pensando" e a pausa olham só para os lados | Olho na linha de cima encosta no topo da cabeça e parece um recorte | Pedir "olhar para cima" exige outro jeito de desenhar o olho |
| Lata 3×5 com faixa azul-gelo (era 2×4) | Branca e estreita, sumia na barra clara e parecia um poste | — |
| Minis 6×4 em pirâmide (o 3º sobe nos outros dois) | Três de 5×3 lado a lado viraram um borrão quadriculado | — |
| Lata amassada voa por cima da cabeça, para trás | Jogada para a direita, saía da tela em 1 quadro | — |
| Estalar os dedos = braços esticados para os lados | De frente, braço esticado para a frente não aparece | — |
| A variação entra no fim de cada bloco de 12 s (a 1ª entre ~8 e 11 s); o sorteio conta o tempo no estado | Mesma pose para a mesma hora, sem guardar nada | Ao voltar de um erro, a contagem dos goles zera e a lata recomeça fechada |
| Dormindo: nada muda em menos de 250 ms | Dormindo, a janela anima a 4 quadros por segundo (economia que já existia); não mexi nisso | Tremidas do ronco ficam mais "travadas" que as do trabalhando |
| Cabeceada inclina no máximo 2 pixels; bolha de ranho cresce 1 → 2×2 → redonda 4×4 | Inclinação 3 virava uma escada e o corpo parecia quebrado; bolha 3×3 de cantos vazios virava uma cruz | — |
| Fila de "Z" em tamanhos 4, 3 e 3 (não 5, 4 e 3) | Acima da cabeça há 12 linhas; com 5, 4 e 3 os três se encostavam numa corrente. Um "Z" de 3×3 é o menor possível; na escala da tela ele lê como "z" | Para 3 tamanhos diferentes, a célula teria de ficar mais alta (janela maior em todos os estados) |
| Terminou: "soco no ar" com punho 2×2 (braço `Punho`); na ida para o sono, uma piscada lenta só e os olhos ficam fechados | O braço fino de "para cima" não parecia soco; de olhos abertos empurrando o teclado não parecia sono | — |
| Erro: a fumaça sai da ponta esquerda do teclado, não do meio | Do meio ela passava na frente do corpo e parecia sujeira | — |
| Modo `--filme` (quadros seguidos, escala 6) | A folha do `--retrato` tem 4 momentos por estado; variação de 12 em 12 s não aparece nela | — |

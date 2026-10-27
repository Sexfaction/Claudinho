#!/usr/bin/env bash
# Teste de ponta a ponta: manda eventos iguais aos dos hooks do Claude Code, pelos mesmos
# comandos que estão no settings.json, e confere o estado do mascote em GET /estado.
# Rodar com o mascote FECHADO: o primeiro evento testa a abertura automática.
# Cada conferência inclui a pasta da sessão de teste: sessões reais do usuário podem estar
# no mascote ao mesmo tempo. O teste toca os bipes de "terminou" e "sem cota".
# Uso (Git Bash):  bash /c/claudinho/testes/testar-eventos.sh
set -u
EXE="C:/claudinho/bin/Claudinho.exe"
URL="http://127.0.0.1:47821"
falhas=0

if tasklist //FI "IMAGENAME eq Claudinho.exe" | grep -qi mascote; then
  echo "Feche o mascote antes (botão direito > Fechar)."; exit 1
fi

# Os comandos dos hooks, idênticos aos do settings.json (lá eles rodam com async: true).
hook_exe()  { echo "$1" | "$EXE" --hook; }
hook_curl() { ( echo "$1" | curl -s --connect-timeout 0.2 -m 1 --data-binary @- "$URL/evento" || exit 0 ); }

ev() { printf '{"hook_event_name":"%s","session_id":"%s","cwd":"%s"%s}' "$1" "$2" "$3" "${4:-}"; }

estado() { curl -s -m 2 "$URL/estado"; }

confere() {
  local atual; atual=$(estado)
  if [[ "$atual" == *"$2"* ]]; then echo "ok     $1"
  else echo "FALHA  $1 (esperado $2, veio $atual)"; falhas=$((falhas+1)); fi
}

nao_tem() {
  local atual; atual=$(estado)
  if [[ "$atual" != *"$2"* ]]; then echo "ok     $1"
  else echo "FALHA  $1 (não devia ter $2, veio $atual)"; falhas=$((falhas+1)); fi
}

A="sessao-alfa"; B="sessao-beta"; C="sessao-gama"
alfa() { echo "\"pasta\":\"alfa\",\"estado\":\"$1\""; }

# ---- O básico ----
hook_exe "$(ev SessionStart $A C:/projetos/alfa ',"source":"startup"')"
confere "SessionStart abre o mascote e cria a sessão dormindo" "$(alfa dormindo)"

hook_exe "$(ev UserPromptSubmit $A C:/projetos/alfa ',"prompt":"oi"')"
confere "UserPromptSubmit acorda" "$(alfa acordando)"
sleep 1
confere "acordando vira trabalhando sozinho" "$(alfa trabalhando)"

hook_curl "$(ev PreToolUse $A C:/projetos/alfa ',"tool_name":"Bash","tool_input":{"command":"ls"}')"
confere "PreToolUse mantém trabalhando" "$(alfa trabalhando)"

hook_curl "$(ev PermissionRequest $A C:/projetos/alfa ',"tool_name":"Bash","tool_input":{"command":"rm x"}')"
confere "PermissionRequest vira precisa de você na hora" "$(alfa 'precisa de você')"
hook_curl "$(ev PostToolUse $A C:/projetos/alfa ',"tool_name":"Bash","tool_response":{"stdout":"ok"}')"
confere "PostToolUse volta a trabalhando" "$(alfa trabalhando)"

hook_curl "$(ev Notification $A C:/projetos/alfa ',"notification_type":"permission_prompt","message":"Claude needs your permission to use Bash"')"
confere "notificação de permissão (a de 6 s) também vale" "$(alfa 'precisa de você')"
hook_curl "$(ev PostToolUse $A C:/projetos/alfa ',"tool_name":"Bash"')"

hook_curl "$(ev PreToolUse $A C:/projetos/alfa ',"tool_name":"AskUserQuestion"')"
confere "AskUserQuestion vira precisa de você" "$(alfa 'precisa de você')"
hook_curl "$(ev PostToolUse $A C:/projetos/alfa ',"tool_name":"AskUserQuestion"')"

grande=$(head -c 300000 /dev/zero | tr '\0' 'x')
hook_curl "$(ev PreToolUse $A C:/projetos/alfa ",\"tool_name\":\"Write\",\"tool_input\":{\"content\":\"$grande\"}")"
confere "evento de 300 KB (Write de arquivo grande) chega" "$(alfa trabalhando)"

# ---- Erro, subagentes, compactação ----
hook_curl "$(ev PostToolUseFailure $A C:/projetos/alfa ',"tool_name":"Bash","error":"Exit code 1","is_interrupt":false')"
confere "ferramenta que falha vira erro" "$(alfa erro)"
hook_curl "$(ev PreToolUse $A C:/projetos/alfa ',"tool_name":"Bash"')"
confere "...e o erro fica visível mesmo com o Claude seguindo" "$(alfa erro)"
sleep 3
confere "...e volta a trabalhando sozinho" "$(alfa trabalhando)"

hook_curl "$(ev SubagentStart $A C:/projetos/alfa ',"agent_id":"a1","agent_type":"Explore"')"
hook_curl "$(ev SubagentStart $A C:/projetos/alfa ',"agent_id":"a2","agent_type":"Explore"')"
confere "dois SubagentStart contam 2 subagentes" "$(alfa trabalhando),\"subagentes\":2"
hook_curl "$(ev SubagentStop $A C:/projetos/alfa ',"agent_id":"a1","agent_type":"Explore"')"
confere "SubagentStop desconta" "\"subagentes\":1"

hook_curl "$(ev PreCompact $A C:/projetos/alfa ',"trigger":"auto"')"
confere "PreCompact vira compactando" "$(alfa 'compactando a conversa')"
hook_curl "$(ev PostCompact $A C:/projetos/alfa ',"trigger":"auto"')"
confere "PostCompact automático volta a trabalhando" "$(alfa trabalhando)"

# ---- Fim do turno e eventos fora de ordem (hooks async) ----
hook_curl "$(ev Stop $A C:/projetos/alfa)"
confere "Stop comemora" "$(alfa terminou)"
confere "...e zera os subagentes" "$(alfa terminou),\"subagentes\":0"
hook_curl "$(ev PostToolUse $A C:/projetos/alfa ',"tool_name":"Bash"')"
confere "PostToolUse atrasado depois do Stop é ignorado" "$(alfa terminou)"
hook_curl "$(ev Notification $A C:/projetos/alfa ',"notification_type":"idle_prompt","message":"Claude is waiting for your input"')"
confere "aviso de espera não acorda" "$(alfa terminou)"
sleep 2.5
confere "terminou vira dormindo sozinho" "$(alfa dormindo)"

hook_exe "$(ev UserPromptSubmit $A C:/projetos/alfa ',"prompt":"de novo"')"
sleep 1
hook_curl "$(ev PostToolUseFailure $A C:/projetos/alfa ',"tool_name":"Bash","error":"aborted","is_interrupt":true')"
confere "Esc com ferramenta rodando (is_interrupt) dorme na hora" "$(alfa dormindo)"

hook_exe "$(ev UserPromptSubmit $A C:/projetos/alfa ',"prompt":"de novo"')"
hook_curl "$(ev StopFailure $A C:/projetos/alfa ',"error":"rate_limit","error_details":"429 Too Many Requests"')"
confere "StopFailure rate_limit vira sem cota" "$(alfa 'sem cota'),\"subagentes\":0,\"detalhe\":\"bateu no limite da assinatura"
hook_exe "$(ev UserPromptSubmit $A C:/projetos/alfa ',"prompt":"de novo"')"
confere "novo prompt tira do sem cota" "$(alfa acordando)"
hook_curl "$(ev StopFailure $A C:/projetos/alfa ',"error":"server_error"')"
confere "outro StopFailure vira erro" "$(alfa erro)"

# ---- Cota pela barra de status ----
saida=$(echo '{"session_id":"x","model":{"display_name":"Opus"},"context_window":{"used_percentage":12.4},"rate_limits":{"five_hour":{"used_percentage":23.5,"resets_at":4102444800},"seven_day":{"used_percentage":41.2,"resets_at":4102444800}}}' | "$EXE" --statusline | tr -d '\r')
if [ "$saida" = "[Opus] · contexto 12% · cota 5h 24% · semana 41%" ]; then echo "ok     barra de status escreve a linha"
else echo "FALHA  barra de status escreveu: $saida"; falhas=$((falhas+1)); fi
confere "barra de status entrega a cota ao mascote" '"cota":{"5h":23.5,"semana":41.2,'

# ---- Sessão some quando o Claude fecha ----
# O cabeçalho é o que o --hook manda: "número;início" do processo do Claude Code. Aqui o
# "Claude" de mentira é o próprio bash do teste (vivo); início 0 = não conferir a hora.
vivo=$(cat /proc/$$/winpid)
claude_falso() { echo "$(ev UserPromptSubmit "$1" "C:/projetos/$2")" | curl -s -m 2 --data-binary @- -H "X-Processo-Claude: $3" "$URL/evento"; }
claude_falso sessao-gama gama "$vivo;0"
claude_falso sessao-delta delta "$vivo;123"
claude_falso sessao-epsilon epsilon "999999;123"
sleep 2.5
confere "sessão cujo Claude está vivo continua" '"pasta":"gama"'
nao_tem "sessão cujo número foi reaproveitado por outro processo sai" '"pasta":"delta"'
nao_tem "sessão cujo Claude fechou sai" '"pasta":"epsilon"'
hook_curl "$(ev SessionEnd sessao-gama C:/projetos/gama)"

# ---- Duas sessões ----
hook_exe "$(ev SessionStart $B C:/projetos/beta ',"source":"startup"')"
hook_exe "$(ev UserPromptSubmit $B C:/projetos/beta ',"prompt":"oi"')"
confere "segunda sessão ganha o próprio bichinho" '"pasta":"beta","estado":"acordando"'

ini=$(date +%s%N); hook_curl "$(ev PreToolUse $B C:/projetos/beta ',"tool_name":"Read"')"; fim=$(date +%s%N)
echo "info   comando do hook de ferramenta: $(( (fim-ini)/1000000 )) ms (com async: true o Claude não espera)"

hook_curl "$(ev SessionEnd $A C:/projetos/alfa ',"reason":"exit"')"
hook_curl "$(ev SessionEnd $B C:/projetos/beta ',"reason":"exit"')"
nao_tem "SessionEnd remove as sessões" '"pasta":"alfa"'
nao_tem "SessionEnd remove as sessões (2)" '"pasta":"beta"'

# Se o teste roda de dentro de uma sessão do Claude Code com os hooks ligados, essa sessão real
# também aparece no mascote e o mantém aberto: aí o fechamento automático não tem como ser testado.
reais=$(estado)
if [[ "$reais" != *'"sessoes":[]'* ]]; then
  echo "pulado fechamento sozinho: há sessão real aberta"
else
  echo "info   esperando 65 s: sem sessão nenhuma, o mascote deve fechar sozinho..."
  sleep 65
  if tasklist //FI "IMAGENAME eq Claudinho.exe" | grep -qi mascote; then
    depois=$(estado)
    if [[ "$depois" != *'"sessoes":[]'* ]]; then echo "pulado fechamento sozinho: sessão real chegou durante a espera"
    else echo "FALHA  mascote continua aberto sem sessões"; falhas=$((falhas+1)); fi
  else echo "ok     mascote fechou sozinho"; fi
fi

ini=$(date +%s%N); hook_curl "$(ev PreToolUse $A C:/projetos/alfa ',"tool_name":"Read"')"; cod=$?; fim=$(date +%s%N)
if [ $cod -eq 0 ]; then echo "ok     hook sai com 0 ($(( (fim-ini)/1000000 )) ms)"
else echo "FALHA  hook saiu com $cod"; falhas=$((falhas+1)); fi
hook_curl "$(ev SessionEnd $A C:/projetos/alfa)" # não deixa a sessão de teste para trás

echo; [ $falhas -eq 0 ] && echo "TUDO VERDE" || echo "$falhas FALHA(S)"
exit $falhas

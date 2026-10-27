# Teste do balão de permissão: roda o mesmo comando do hook PermissionRequest
# (Claudinho.exe --permissao) com pedidos de mentira e aperta os botões do balão pela
# Automação de Interface do Windows (sem mexer no mouse). Confere o que o hook devolve
# ao Claude. Rodar com o mascote FECHADO. Uso: powershell -File testar-permissao.ps1
param([string]$Foto = "")
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$exe = "C:\claudinho\bin\Claudinho.exe"
$url = "http://127.0.0.1:47821"
$script:falhas = 0

if (Get-Process Claudinho -ErrorAction SilentlyContinue) { "Feche o mascote antes."; exit 1 }

function Confere($nome, $ok, $detalhe) {
    if ($ok) { "ok     $nome" } else { "FALHA  $nome ($detalhe)"; $script:falhas++ }
}

# Inicia o hook como o Claude faria: JSON na entrada, decisão na saída.
function Iniciar-Hook($json) {
    $psi = New-Object Diagnostics.ProcessStartInfo $exe, "--permissao"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $p = [Diagnostics.Process]::Start($psi)
    $p.StandardInput.Write($json)
    $p.StandardInput.Close()
    return $p
}

function Terminar-Hook($p, $segundos) {
    if (-not $p.WaitForExit($segundos * 1000)) { $p.Kill(); return "<travou>" }
    return $p.StandardOutput.ReadToEnd()
}

function Estado { (curl.exe -s -m 2 "$url/estado") | ConvertFrom-Json }

# Espera o balão e o meio segundo em que os botões ficam desligados (contra clique acidental).
function Esperar-Balao($ferramenta, [switch]$SemArmar) {
    for ($i = 0; $i -lt 40; $i++) {
        if ((Estado).balao -eq $ferramenta) {
            if (-not $SemArmar) { Start-Sleep -Milliseconds 700 }
            return $true
        }
        Start-Sleep -Milliseconds 100
    }
    return $false
}

function Balao {
    $raiz = [Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object Windows.Automation.PropertyCondition ([Windows.Automation.AutomationElement]::NameProperty), "Permissão — Claudinho"
    return $raiz.FindFirst([Windows.Automation.TreeScope]::Children, $cond)
}

function Apertar($botao) {
    $b = Balao
    if ($b -eq $null) { return $false }
    $cond = New-Object Windows.Automation.PropertyCondition ([Windows.Automation.AutomationElement]::NameProperty), $botao
    $alvo = $b.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
    if ($alvo -eq $null) { return $false }
    try { $alvo.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
    catch [Windows.Automation.ElementNotEnabledException] { return $false } # botão ainda desligado
    return $true
}

function Pedido($sessao, $ferramenta, $entrada, $sugestoes = "") {
    $s = if ($sugestoes) { ",""permission_suggestions"":$sugestoes" } else { "" }
    return "{""hook_event_name"":""PermissionRequest"",""session_id"":""$sessao"",""cwd"":""C:/projetos/teste-permissao"",""tool_name"":""$ferramenta"",""tool_input"":$entrada$s}"
}

function Evento($json) { $json | curl.exe -s -m 2 --data-binary "@-" "$url/evento" | Out-Null }

$sessao = "sessao-permissao"
$entradaBash = '{"command":"npm test","description":"roda os testes"}'
$sugestao = '[{"type":"addRules","rules":[{"toolName":"Bash","ruleContent":"npm test:*"}],"behavior":"allow","destination":"localSettings"}]'

# ---- Mascote fechado: sai em silêncio e rápido ----
$t = [Diagnostics.Stopwatch]::StartNew()
$p = Iniciar-Hook (Pedido $sessao "Bash" $entradaBash)
$saida = Terminar-Hook $p 5
Confere "mascote fechado: o hook sai sem decidir ($($t.ElapsedMilliseconds) ms)" ($saida -eq "") "saída: $saida"
Confere "mascote fechado: o hook não abre o mascote" (-not (Get-Process Claudinho -ErrorAction SilentlyContinue)) "abriu"

# ---- Abre o mascote com uma sessão ----
Start-Process $exe
Start-Sleep -Milliseconds 800
Evento "{""hook_event_name"":""UserPromptSubmit"",""session_id"":""$sessao"",""cwd"":""C:/projetos/teste-permissao""}"

# ---- Permitir ----
$p = Iniciar-Hook (Pedido $sessao "Bash" $entradaBash $sugestao)
Confere "o balão aparece" (Esperar-Balao "Bash" -SemArmar) "balão não apareceu"
# Pelo /estado, não pela automação: achar o botão na tela pode levar mais que o meio segundo.
Confere "nos primeiros 0,5 s os botões estão desligados (clique acidental)" (-not (Estado).balao_armado) "já ligados"
Start-Sleep -Milliseconds 700
Confere "...e depois ligam" ((Estado).balao_armado) "continuam desligados"
if ($Foto -and (Balao) -ne $null) {
    $r = (Balao).Current.BoundingRectangle
    $bmp = New-Object Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
    $bmp.Save($Foto)
    "info   foto do balão em $Foto"
}
Confere "botão Permitir apertado" (Apertar "Permitir") "não achei o botão"
$saida = Terminar-Hook $p 5
Confere "Permitir devolve allow" ($saida -eq '{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow"}}}') "saída: $saida"
Start-Sleep -Milliseconds 300
Confere "o balão fecha" ((Estado).balao -eq $null) "ainda aberto"

# ---- Sempre permitir: devolve a sugestão do Claude ----
$p = Iniciar-Hook (Pedido $sessao "Bash" $entradaBash $sugestao)
[void](Esperar-Balao "Bash")
Confere "botão Sempre permitir apertado" (Apertar "Sempre permitir") "não achei o botão"
$saida = Terminar-Hook $p 5
$esperado = '{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow","updatedPermissions":[{"type":"addRules","rules":[{"toolName":"Bash","ruleContent":"npm test:*"}],"behavior":"allow","destination":"localSettings"}]}}}'
Confere "Sempre permitir devolve allow + a regra sugerida" ($saida -eq $esperado) "saída: $saida"

# ---- Sem sugestão, sem botão Sempre permitir ----
$p = Iniciar-Hook (Pedido $sessao "Read" '{"file_path":"C:/x/segredo.txt"}')
[void](Esperar-Balao "Read")
Confere "sem sugestão do Claude, não há Sempre permitir" (-not (Apertar "Sempre permitir")) "o botão existe"

# ---- Negar ----
Confere "botão Negar apertado" (Apertar "Negar") "não achei o botão"
$saida = Terminar-Hook $p 5
Confere "Negar devolve deny com a mensagem" ($saida -eq '{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"deny","message":"O usuário negou esta ação pelo Claudinho, o mascote do Claude na barra de tarefas."}}}') "saída: $saida"

# ---- Respondido no pedido normal: o Claude encerra o hook, o balão fecha ----
$p = Iniciar-Hook (Pedido $sessao "Bash" $entradaBash)
[void](Esperar-Balao "Bash")
$p.Kill()
Start-Sleep -Milliseconds 800
Confere "hook encerrado pelo Claude: o balão fecha" ((Estado).balao -eq $null) "ainda aberto"

# ---- A ferramenta rodou (PostToolUse igual): o balão fecha sem decidir ----
$p = Iniciar-Hook (Pedido $sessao "Bash" $entradaBash)
[void](Esperar-Balao "Bash")
Evento "{""hook_event_name"":""PostToolUse"",""session_id"":""$sessao"",""tool_name"":""Bash"",""tool_input"":$entradaBash}"
$saida = Terminar-Hook $p 5
Confere "PostToolUse da mesma ferramenta: o hook sai sem decidir" ($saida -eq "") "saída: $saida"
Confere "...e o balão fecha" ((Estado).balao -eq $null) "ainda aberto"

# ---- Outra ferramenta rodando não fecha o balão ----
$p = Iniciar-Hook (Pedido $sessao "Bash" $entradaBash)
[void](Esperar-Balao "Bash")
Evento "{""hook_event_name"":""PostToolUse"",""session_id"":""$sessao"",""tool_name"":""Bash"",""tool_input"":{""command"":""ls""}}"
Start-Sleep -Milliseconds 300
Confere "PostToolUse de outro comando não fecha o balão" ((Estado).balao -eq "Bash") "fechou"
Evento "{""hook_event_name"":""Stop"",""session_id"":""$sessao""}"
$saida = Terminar-Hook $p 5
Confere "Stop fecha o balão sem decidir" ($saida -eq "") "saída: $saida"

# ---- Aprovação de plano e pergunta com opções não viram balão ----
$p = Iniciar-Hook (Pedido $sessao "ExitPlanMode" '{"plan":"..."}')
$saida = Terminar-Hook $p 5
Confere "ExitPlanMode sai na hora sem decidir" ($saida -eq "") "saída: $saida"
$p = Iniciar-Hook (Pedido $sessao "AskUserQuestion" '{"questions":[]}')
$saida = Terminar-Hook $p 5
Confere "AskUserQuestion sai na hora sem decidir" ($saida -eq "") "saída: $saida"

# ---- Requisição de navegador ou de outro endereço é recusada ----
$corpo = Pedido $sessao "Bash" $entradaBash
$cod = $corpo | curl.exe -s -o NUL -w "%{http_code}" -m 3 --data-binary "@-" -H "Origin: https://site-qualquer.com" "$url/permissao"
Confere "POST /permissao com Origin (site no navegador) recebe 403" ($cod -eq "403") "código $cod"
$cod = $corpo | curl.exe -s -o NUL -w "%{http_code}" -m 3 --data-binary "@-" -H "Host: atacante.com" "$url/evento"
Confere "Host que não é esta máquina (DNS rebinding) recebe 403" ($cod -eq "403") "código $cod"
Confere "...e nenhum balão apareceu" ((Estado).balao -eq $null) "apareceu"

# ---- Dois pedidos: um balão por vez, na ordem ----
Evento "{""hook_event_name"":""UserPromptSubmit"",""session_id"":""$sessao"",""cwd"":""C:/projetos/teste-permissao""}"
$p1 = Iniciar-Hook (Pedido $sessao "Bash" $entradaBash)
[void](Esperar-Balao "Bash")
$p2 = Iniciar-Hook (Pedido $sessao "Write" '{"file_path":"C:/x/novo.txt","content":"oi"}')
Start-Sleep -Milliseconds 500
$e = Estado
Confere "o segundo pedido espera na fila" ($e.balao -eq "Bash" -and $e.fila -eq 2) "balão $($e.balao), fila $($e.fila)"
[void](Apertar "Permitir")
[void](Terminar-Hook $p1 5)
Confere "decidido o primeiro, o segundo aparece" (Esperar-Balao "Write") "não apareceu"
[void](Apertar "Negar")
$saida = Terminar-Hook $p2 5
Confere "o segundo recebe a própria decisão" ($saida -like '*"behavior":"deny"*') "saída: $saida"

# ---- Mascote fechando com pedido aberto: sem decisão ----
$p = Iniciar-Hook (Pedido $sessao "Bash" $entradaBash)
[void](Esperar-Balao "Bash")
Stop-Process -Name Claudinho
$saida = Terminar-Hook $p 5
Confere "mascote fechado no meio: o hook sai sem decidir" ($saida -eq "") "saída: $saida"

""
if ($script:falhas -eq 0) { "TUDO VERDE" } else { "$($script:falhas) FALHA(S)" }
exit $script:falhas

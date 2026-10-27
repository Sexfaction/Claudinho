using System;
using System.Collections.Generic;
using System.IO;

namespace Claudinho
{
    enum Estado { Dormindo, Acordando, Trabalhando, PrecisaDeVoce, Terminou, Erro, SemCota, Compactando }

    class Sessao
    {
        public string Id;
        public string Pasta;
        public Estado Estado;
        public long Desde;          // ms em que entrou no estado atual
        public long UltimoEvento;   // ms do último evento recebido desta sessão
        public int Fase;            // defasagem da animação, para os bichinhos não andarem em sincronia
        public bool Fixa;           // sessão do --demo: não muda sozinha nem expira
        public int ProcessoJanela;  // programa com a janela da sessão (0 = ainda não sabido)
        public int ProcessoClaude;  // o processo do Claude Code; quando ele some, a sessão sai
        public long InicioClaude;   // hora de início desse processo (o Windows reaproveita números)
        public int Subagentes;      // subagentes rodando agora
        public long TurnoFechadoEm = -1; // ms do Stop (-1 = turno aberto)
        public Estado DepoisDoErro; // para onde o Erro volta quando acaba
        public string Detalhe;      // texto extra para a dica (o erro, a cota)
    }

    // Cota da assinatura, que vem da barra de status do Claude Code. Vale para a conta toda,
    // não por sessão. Null = o Claude ainda não mandou (ou não é assinatura Pro/Max).
    class Cota
    {
        public double? Uso5h, UsoSemana;
        public long Renova5h, RenovaSemana; // segundos desde 1970 (UTC)
        public DateTime? BarraRodou;        // última vez que a barra de status mandou algo (diagnóstico)
    }

    // Regras de estado: qual evento do hook leva a qual animação, e o que muda sozinho com o tempo.
    class Sessoes
    {
        public const int MaximoVisiveis = 4;
        const long TempoAcordando = 600;
        const long TempoComemorando = 2000;
        const long TempoErroNoMeio = 2500;    // falha de ferramenta: mostra e volta a trabalhar
        const long TempoErroNoFim = 5000;     // a resposta morreu por erro: mostra e dorme
        const long ErroMinimo = 1500;         // o erro aparece pelo menos isto, mesmo com o Claude seguindo
        // Os hooks rodam em segundo plano (async) e podem chegar fora de ordem: uma ferramenta
        // que chega até 3 s depois do Stop é atraso do mesmo turno, não trabalho novo.
        const long JanelaAtraso = 3000;
        // O Stop não chega quando a resposta é interrompida (Esc); sem evento por este tempo, dorme.
        const long TrabalhandoSemEvento = 10 * 60 * 1000;
        const long DormindoAteSair = 30 * 60 * 1000;
        const long SemCotaAteDormir = 5L * 60 * 60 * 1000; // a janela de 5 h já renovou
        const long VazioAteFechar = 60 * 1000;

        public readonly List<Sessao> Lista = new List<Sessao>();
        public readonly Cota Cota = new Cota();
        public Action<Sessao, Estado> AoMudar; // para o som
        long ultimaAtividade;
        int proximaFase;

        public Sessoes(long agora)
        {
            ultimaAtividade = agora;
        }

        public void Aplicar(Dictionary<string, object> evento, long agora)
        {
            string nome = Texto(evento, "hook_event_name");
            if (nome == "_Cota")
            {
                AtualizarCota(evento);
                return;
            }
            string id = Texto(evento, "session_id");
            if (nome == null || id == null) return;
            ultimaAtividade = agora;

            Sessao s = Lista.Find(x => x.Id == id);
            if (nome == "SessionEnd")
            {
                if (s != null) Lista.Remove(s);
                return;
            }
            if (s == null)
            {
                s = new Sessao();
                s.Id = id;
                s.Pasta = NomeDaPasta(Texto(evento, "cwd"));
                s.Estado = Estado.Dormindo;
                s.Desde = agora;
                s.Fase = (proximaFase++ * 377) % 1000;
                Lista.Add(s);
            }
            s.UltimoEvento = agora;
            object valor;
            if (evento.TryGetValue("_processo_janela", out valor) && valor is int) s.ProcessoJanela = (int)valor;
            if (evento.TryGetValue("_processo_claude", out valor) && valor is int) s.ProcessoClaude = (int)valor;
            if (evento.TryGetValue("_inicio_claude", out valor) && valor is long) s.InicioClaude = (long)valor;

            bool atrasado = s.TurnoFechadoEm >= 0 && agora - s.TurnoFechadoEm < JanelaAtraso;
            bool erroRecente = s.Estado == Estado.Erro && agora - s.Desde < ErroMinimo;

            switch (nome)
            {
                case "UserPromptSubmit":
                    s.TurnoFechadoEm = -1;
                    s.Subagentes = 0;
                    s.Detalhe = null;
                    Mudar(s, Estado.Acordando, agora);
                    break;

                case "PreToolUse":
                    if (atrasado) break;
                    string ferramenta = Texto(evento, "tool_name");
                    if (ferramenta == "AskUserQuestion" || ferramenta == "ExitPlanMode")
                        Mudar(s, Estado.PrecisaDeVoce, agora);
                    else
                        Trabalhar(s, agora, erroRecente);
                    break;

                case "PostToolUse":
                    if (!atrasado) Trabalhar(s, agora, erroRecente);
                    break;

                case "PostToolUseFailure":
                    if (atrasado) break;
                    if (Verdadeiro(evento, "is_interrupt"))
                    {
                        // Esc com uma ferramenta rodando: o Claude parou e não vai mandar Stop.
                        s.TurnoFechadoEm = agora;
                        s.Subagentes = 0;
                        Mudar(s, Estado.Dormindo, agora);
                    }
                    else
                    {
                        s.Detalhe = "uma ferramenta falhou (" + Texto(evento, "tool_name") + ")";
                        s.DepoisDoErro = Estado.Trabalhando;
                        Mudar(s, Estado.Erro, agora);
                    }
                    break;

                case "PermissionRequest":
                    if (!atrasado) Mudar(s, Estado.PrecisaDeVoce, agora);
                    break;

                case "Notification":
                    if (!atrasado && PedePermissao(evento)) Mudar(s, Estado.PrecisaDeVoce, agora);
                    break;

                case "SubagentStart":
                    if (atrasado) break;
                    s.Subagentes++;
                    if (s.Estado != Estado.PrecisaDeVoce) Trabalhar(s, agora, erroRecente);
                    break;

                case "SubagentStop":
                    s.Subagentes = Math.Max(0, s.Subagentes - 1);
                    break;

                case "PreCompact":
                    Mudar(s, Estado.Compactando, agora);
                    break;

                case "PostCompact":
                    // Compactação automática acontece no meio do trabalho; a manual (/compact), com o Claude parado.
                    Mudar(s, Texto(evento, "trigger") == "auto" ? Estado.Trabalhando : Estado.Dormindo, agora);
                    break;

                case "Stop":
                    s.TurnoFechadoEm = agora;
                    s.Subagentes = 0;
                    Mudar(s, Estado.Terminou, agora);
                    break;

                case "StopFailure":
                    s.TurnoFechadoEm = agora;
                    s.Subagentes = 0;
                    if (Texto(evento, "error") == "rate_limit")
                    {
                        s.Detalhe = "bateu no limite da assinatura";
                        Mudar(s, Estado.SemCota, agora);
                    }
                    else
                    {
                        s.Detalhe = "a resposta parou com erro (" + Texto(evento, "error") + ")";
                        s.DepoisDoErro = Estado.Dormindo;
                        Mudar(s, Estado.Erro, agora);
                    }
                    break;
                // SessionStart: só faz o bichinho aparecer (dormindo). Num SessionStart de
                // compactação a sessão já existe e o estado fica como está.
            }
        }

        void Trabalhar(Sessao s, long agora, bool erroRecente)
        {
            if (s.Estado == Estado.Acordando) return; // o pulinho termina sozinho e vira trabalhando
            if (erroRecente)
            {
                s.DepoisDoErro = Estado.Trabalhando;
                return;
            }
            Mudar(s, Estado.Trabalhando, agora);
        }

        void Mudar(Sessao s, Estado novo, long agora)
        {
            if (s.Estado == novo) return;
            s.Estado = novo;
            s.Desde = agora;
            if (AoMudar != null) AoMudar(s, novo);
        }

        // Só pedido de permissão (ou pergunta) acorda o bichinho. O aviso "esperando sua
        // resposta", que chega ~1 min depois do Stop, é ignorado: ali ele já está dormindo.
        static bool PedePermissao(Dictionary<string, object> evento)
        {
            string tipo = Texto(evento, "notification_type");
            if (tipo != null) return tipo == "permission_prompt" || tipo == "elicitation_dialog";
            string mensagem = Texto(evento, "message");
            return mensagem != null && mensagem.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void AtualizarCota(Dictionary<string, object> evento)
        {
            Cota.BarraRodou = DateTime.Now;
            object bruto;
            var limites = evento.TryGetValue("rate_limits", out bruto) ? bruto as Dictionary<string, object> : null;
            if (limites == null) return;
            LerJanela(limites, "five_hour", ref Cota.Uso5h, ref Cota.Renova5h);
            LerJanela(limites, "seven_day", ref Cota.UsoSemana, ref Cota.RenovaSemana);
        }

        static void LerJanela(Dictionary<string, object> limites, string nome, ref double? uso, ref long renova)
        {
            object bruto;
            var janela = limites.TryGetValue(nome, out bruto) ? bruto as Dictionary<string, object> : null;
            if (janela == null) return;
            double? pct = Numero(janela, "used_percentage");
            double? reset = Numero(janela, "resets_at");
            if (pct == null) return;
            uso = pct;
            renova = reset.HasValue ? (long)reset.Value : 0;
        }

        // Transições por tempo. Devolve false quando o programa deve fechar (nenhuma sessão há 1 min).
        public bool Avancar(long agora)
        {
            for (int i = Lista.Count - 1; i >= 0; i--)
            {
                Sessao s = Lista[i];
                if (s.Fixa) continue;
                long noEstado = agora - s.Desde;
                bool parado = agora - s.UltimoEvento > TrabalhandoSemEvento;
                switch (s.Estado)
                {
                    case Estado.Acordando:
                        if (noEstado > TempoAcordando) Mudar(s, Estado.Trabalhando, agora);
                        break;
                    case Estado.Terminou:
                        if (noEstado > TempoComemorando) Mudar(s, Estado.Dormindo, agora);
                        break;
                    case Estado.Erro:
                        if (noEstado > (s.DepoisDoErro == Estado.Dormindo ? TempoErroNoFim : TempoErroNoMeio))
                            Mudar(s, s.DepoisDoErro, agora);
                        break;
                    case Estado.Trabalhando:
                    case Estado.Compactando:
                        if (parado) Mudar(s, Estado.Dormindo, agora);
                        break;
                    case Estado.SemCota:
                        if (noEstado > SemCotaAteDormir) Mudar(s, Estado.Dormindo, agora);
                        break;
                    case Estado.Dormindo:
                        if (noEstado > DormindoAteSair) Lista.RemoveAt(i);
                        break;
                    // PrecisaDeVoce não expira: o Claude está mesmo esperando, por quanto tempo for.
                }
            }
            if (Lista.Count > 0) ultimaAtividade = agora;
            return agora - ultimaAtividade < VazioAteFechar;
        }

        // Tira as sessões cujo Claude Code já fechou (VS Code fechado, terminal fechado, travou).
        public void RemoverEncerradas(Predicate<Sessao> encerrada)
        {
            Lista.RemoveAll(s => !s.Fixa && encerrada(s));
        }

        public void DormirTodas(long agora)
        {
            foreach (Sessao s in Lista) Mudar(s, Estado.Dormindo, agora);
        }

        public bool AlgumaAcordada()
        {
            foreach (Sessao s in Lista) if (s.Estado != Estado.Dormindo) return true;
            return false;
        }

        public int Visiveis
        {
            get { return Math.Min(Lista.Count, MaximoVisiveis); }
        }

        public Sessao NovaFixa(string pasta, Estado estado, int subagentes, long agora)
        {
            var s = new Sessao();
            s.Id = "demo-" + Lista.Count;
            s.Pasta = pasta;
            s.Estado = estado;
            s.Desde = agora;
            s.UltimoEvento = agora;
            s.Fase = (proximaFase++ * 377) % 1000;
            s.Fixa = true;
            s.Subagentes = subagentes;
            Lista.Add(s);
            return s;
        }

        static string NomeDaPasta(string cwd)
        {
            if (string.IsNullOrEmpty(cwd)) return "sessão";
            string nome = Path.GetFileName(cwd.TrimEnd('\\', '/'));
            return string.IsNullOrEmpty(nome) ? cwd : nome;
        }

        static string Texto(Dictionary<string, object> evento, string chave)
        {
            object valor;
            return evento.TryGetValue(chave, out valor) ? valor as string : null;
        }

        static bool Verdadeiro(Dictionary<string, object> evento, string chave)
        {
            object valor;
            return evento.TryGetValue(chave, out valor) && valor is bool && (bool)valor;
        }

        // O leitor de JSON entrega número como int, long ou decimal, conforme o tamanho.
        static double? Numero(Dictionary<string, object> dados, string chave)
        {
            object valor;
            if (!dados.TryGetValue(chave, out valor) || valor == null) return null;
            try { return Convert.ToDouble(valor, System.Globalization.CultureInfo.InvariantCulture); }
            catch (FormatException) { return null; }
            catch (InvalidCastException) { return null; }
        }
    }
}

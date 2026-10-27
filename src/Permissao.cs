using System;
using System.Collections.Generic;
using System.Threading;
using System.Web.Script.Serialization;

namespace Claudinho
{
    // Um pedido de permissão do Claude esperando resposta. O servidor fica segurando a conexão
    // do hook (Claudinho.exe --permissao) até o balão decidir ou o pedido ser cancelado.
    // Resposta null = "sem decisão": o Claude segue só com o pedido normal.
    class PedidoPermissao : IDisposable
    {
        public readonly string Sessao;
        public readonly string Ferramenta;
        public readonly Dictionary<string, object> Entrada;
        public readonly string EntradaSerializada; // para reconhecer a mesma ferramenta já executada
        public readonly Dictionary<string, object> Sugestao; // a do "Sempre permitir" (null = sem botão)
        public readonly DateTime Chegou = DateTime.Now;
        public readonly ManualResetEvent Pronto = new ManualResetEvent(false);

        readonly object trava = new object();
        string resposta;
        bool encerrado;

        public PedidoPermissao(Dictionary<string, object> evento)
        {
            object valor;
            Sessao = evento.TryGetValue("session_id", out valor) ? valor as string : null;
            Ferramenta = (evento.TryGetValue("tool_name", out valor) ? valor as string : null) ?? "?";
            Entrada = (evento.TryGetValue("tool_input", out valor) ? valor as Dictionary<string, object> : null)
                ?? new Dictionary<string, object>();
            EntradaSerializada = Json().Serialize(Entrada);
            Sugestao = EscolherSugestao(evento);
        }

        public bool Encerrado
        {
            get { lock (trava) return encerrado; }
        }

        public string Resposta
        {
            get { lock (trava) return resposta; }
        }

        // Só o balão chama (um clique). Devolve false se o pedido já tinha acabado.
        public bool Decidir(string json)
        {
            lock (trava)
            {
                if (encerrado) return false;
                resposta = json;
                encerrado = true;
            }
            Pronto.Set();
            return true;
        }

        // Sem decisão: respondido no pedido normal, ferramenta já rodou, Claude fechou, não perturbe...
        public void Cancelar()
        {
            lock (trava)
            {
                if (encerrado) return;
                encerrado = true;
            }
            Pronto.Set();
        }

        // O servidor chama quando acabou de responder; depois disso Decidir/Cancelar não mexem
        // mais no evento (o pedido já está encerrado).
        public void Dispose()
        {
            Cancelar();
            Pronto.Close();
        }

        // ---- O que volta para o Claude (formato do hook PermissionRequest) ----

        public string Permitir()
        {
            return Decisao("allow", null, null);
        }

        public string PermitirSempre()
        {
            return Decisao("allow", Sugestao, null);
        }

        public string Negar()
        {
            return Decisao("deny", null, "O usuário negou esta ação pelo Claudinho, o mascote do Claude na barra de tarefas.");
        }

        static string Decisao(string comportamento, Dictionary<string, object> sugestao, string mensagem)
        {
            var decisao = new Dictionary<string, object>();
            decisao["behavior"] = comportamento;
            if (sugestao != null) decisao["updatedPermissions"] = new object[] { sugestao };
            if (mensagem != null) decisao["message"] = mensagem;
            var saida = new Dictionary<string, object>();
            saida["hookEventName"] = "PermissionRequest";
            saida["decision"] = decisao;
            var tudo = new Dictionary<string, object>();
            tudo["hookSpecificOutput"] = saida;
            return Json().Serialize(tudo);
        }

        // A primeira sugestão do Claude que libera algo (regra de permitir, modo, pasta). As de
        // negar ou perguntar não servem para um botão "Sempre permitir".
        static Dictionary<string, object> EscolherSugestao(Dictionary<string, object> evento)
        {
            object valor;
            var lista = evento.TryGetValue("permission_suggestions", out valor) ? valor as object[] : null;
            if (lista == null) return null;
            foreach (object item in lista)
            {
                var s = item as Dictionary<string, object>;
                if (s == null) continue;
                string tipo = Texto(s, "type");
                if ((tipo == "addRules" && Texto(s, "behavior") == "allow") || tipo == "setMode" || tipo == "addDirectories")
                    return s;
            }
            return null;
        }

        // "Bash(npm test:*) neste projeto, só nesta máquina" — o que o botão vai gravar.
        public string DescreverSugestao()
        {
            if (Sugestao == null) return null;
            string tipo = Texto(Sugestao, "type");
            string oque;
            object valor;
            if (tipo == "addRules")
            {
                var regras = new List<string>();
                var lista = Sugestao.TryGetValue("rules", out valor) ? valor as object[] : null;
                if (lista != null)
                    foreach (object r in lista)
                    {
                        var regra = r as Dictionary<string, object>;
                        if (regra == null) continue;
                        string conteudo = Texto(regra, "ruleContent");
                        regras.Add(Texto(regra, "toolName") + (string.IsNullOrEmpty(conteudo) ? "" : "(" + conteudo + ")"));
                    }
                oque = string.Join(", ", regras.ToArray());
            }
            else if (tipo == "setMode")
            {
                string modo = Texto(Sugestao, "mode");
                oque = modo == "acceptEdits" ? "aceitar edições de arquivo sem perguntar" : "modo " + modo;
            }
            else
            {
                var pastas = Sugestao.TryGetValue("directories", out valor) ? valor as object[] : null;
                oque = "acesso a " + (pastas == null ? "?" : string.Join(", ", Array.ConvertAll(pastas, p => p as string)));
            }
            return oque + " " + Destino(Texto(Sugestao, "destination"));
        }

        static string Destino(string destino)
        {
            switch (destino)
            {
                case "session": return "só nesta sessão";
                case "localSettings": return "neste projeto, só nesta máquina";
                case "projectSettings": return "neste projeto (vai para o repositório)";
                case "userSettings": return "em todos os projetos";
                default: return destino == null ? "" : "(" + destino + ")";
            }
        }

        // O que mostrar no balão: o comando, o arquivo, o endereço... ou a entrada inteira.
        // Para Write/Edit é só o caminho do arquivo, sem o conteúdo. Cortado em `maximo`, com
        // aviso de quanto ficou de fora: nada é aprovado sem dar para ver que havia mais.
        public string Resumo(int maximo)
        {
            string texto = Texto(Entrada, "command") ?? Texto(Entrada, "file_path") ?? Texto(Entrada, "notebook_path")
                ?? Texto(Entrada, "url") ?? Texto(Entrada, "query") ?? Texto(Entrada, "pattern");
            if (texto == null) texto = EntradaSerializada;
            if (texto.Length <= maximo) return texto;
            return texto.Substring(0, maximo) + "… (+" + (texto.Length - maximo) + " caracteres)";
        }

        public bool ResumoCabe(int maximo)
        {
            return Resumo(int.MaxValue).Length <= maximo;
        }

        static string Texto(Dictionary<string, object> dados, string chave)
        {
            object valor;
            return dados.TryGetValue(chave, out valor) ? valor as string : null;
        }

        static JavaScriptSerializer Json()
        {
            var j = new JavaScriptSerializer();
            j.MaxJsonLength = int.MaxValue;
            return j;
        }
    }
}

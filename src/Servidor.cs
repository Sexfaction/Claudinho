using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Claudinho
{
    // Recebe os eventos dos hooks: um POST com o JSON do hook, só em 127.0.0.1.
    // TCP puro em vez de HttpListener, que pode exigir administrador para abrir a porta.
    class Servidor
    {
        const int TamanhoMaximo = 32 * 1024 * 1024;
        static readonly byte[] Resposta = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n");
        static readonly byte[] Continuar = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");

        static readonly string[] HostsDaMaquina = { "127.0.0.1", "127.0.0.1:47821", "localhost", "localhost:47821" };

        // O hook de permissão espera até 600 s; o servidor desiste um pouco antes, sem decidir nada.
        static readonly TimeSpan EsperaMaximaPermissao = TimeSpan.FromSeconds(590);

        readonly int porta;
        readonly Action<Dictionary<string, object>> aoReceber;
        readonly Func<string> estadoAtual;
        readonly Action<PedidoPermissao> aoPedirPermissao;
        TcpListener ouvinte;

        // estadoAtual responde ao GET /estado (JSON das sessões), usado pelo teste.
        // aoPedirPermissao mostra o balão; a decisão volta pelo próprio pedido.
        public Servidor(int porta, Action<Dictionary<string, object>> aoReceber, Func<string> estadoAtual,
            Action<PedidoPermissao> aoPedirPermissao)
        {
            this.porta = porta;
            this.aoReceber = aoReceber;
            this.estadoAtual = estadoAtual;
            this.aoPedirPermissao = aoPedirPermissao;
        }

        public bool Iniciar()
        {
            try
            {
                ouvinte = new TcpListener(IPAddress.Loopback, porta);
                ouvinte.Start();
            }
            catch (SocketException ex)
            {
                Registro.Erro("porta " + porta + " ocupada", ex);
                return false;
            }
            var laco = new Thread(Laco);
            laco.IsBackground = true;
            laco.Start();
            return true;
        }

        public void Parar()
        {
            if (ouvinte != null) ouvinte.Stop();
        }

        void Laco()
        {
            while (true)
            {
                TcpClient cliente;
                try { cliente = ouvinte.AcceptTcpClient(); }
                catch (SocketException) { return; }        // Parar() fecha o ouvinte
                catch (ObjectDisposedException) { return; }
                ThreadPool.QueueUserWorkItem(Atender, cliente);
            }
        }

        void Atender(object estado)
        {
            var cliente = (TcpClient)estado;
            bool entregue = false; // a conexão passou para a thread de um pedido de permissão
            try
            {
                cliente.ReceiveTimeout = 2000;
                cliente.SendTimeout = 2000;
                var fluxo = cliente.GetStream();
                string caminho;
                int processoJanela;
                string processoClaude;
                bool deFora;
                byte[] corpo = LerRequisicao(fluxo, out caminho, out processoJanela, out processoClaude, out deFora);
                if (deFora)
                {
                    // Veio de um navegador (cabeçalho Origin) ou de um endereço que não é esta
                    // máquina (ataque de DNS rebinding): os hooks nunca mandam assim.
                    Responder(fluxo, "403 Forbidden", "");
                    return;
                }
                if (caminho == "GET /estado")
                {
                    Responder(fluxo, "200 OK", estadoAtual());
                    return;
                }
                if (caminho == "POST /permissao")
                {
                    // Pode esperar até 590 s: thread própria, para não prender as do ThreadPool,
                    // que atendem os eventos da animação.
                    var pedidoJson = Ler(corpo);
                    var espera = new Thread(delegate()
                    {
                        using (cliente) AtenderPermissao(cliente, fluxo, pedidoJson);
                    });
                    espera.IsBackground = true;
                    espera.Start();
                    entregue = true;
                    return;
                }

                // Responde antes de interpretar: o hook (e o Claude) não esperam pela animação.
                fluxo.Write(Resposta, 0, Resposta.Length);
                var evento = Ler(corpo);
                if (evento == null) return;
                if (processoJanela > 0) evento["_processo_janela"] = processoJanela;
                // "número;início" do processo do Claude Code (ver Program.ModoHook).
                string[] claude = (processoClaude ?? "").Split(';');
                int pid;
                long inicio;
                if (claude.Length == 2 && int.TryParse(claude[0], out pid) && pid > 0 && long.TryParse(claude[1], out inicio))
                {
                    evento["_processo_claude"] = pid;
                    evento["_inicio_claude"] = inicio;
                }
                aoReceber(evento);
            }
            catch (Exception ex)
            {
                Registro.Erro("requisição", ex);
            }
            finally
            {
                if (!entregue) cliente.Close();
            }
        }

        // Segura a conexão do hook até o balão decidir. Se o Claude derrubar a conexão (você
        // respondeu no pedido normal, ou ele fechou), o pedido é cancelado e o balão fecha.
        // Nenhuma requisição decide nada: a decisão só sai de um clique no balão.
        void AtenderPermissao(TcpClient cliente, NetworkStream fluxo, Dictionary<string, object> evento)
        {
            try
            {
                if (evento == null)
                {
                    Responder(fluxo, "200 OK", "");
                    return;
                }
                using (var pedido = new PedidoPermissao(evento))
                {
                    aoPedirPermissao(pedido);
                    DateTime limite = DateTime.Now + EsperaMaximaPermissao;
                    while (!pedido.Pronto.WaitOne(250))
                    {
                        if (ConexaoCaiu(cliente) || DateTime.Now > limite)
                        {
                            pedido.Cancelar();
                            break;
                        }
                    }
                    if (!ConexaoCaiu(cliente)) Responder(fluxo, "200 OK", pedido.Resposta ?? "");
                }
            }
            catch (IOException) { }      // o hook foi embora no meio da resposta: nada a fazer
            catch (SocketException) { }
            catch (Exception ex)
            {
                Registro.Erro("permissão", ex);
            }
        }

        // O outro lado fechou: há "algo para ler" mas nada chegou.
        static bool ConexaoCaiu(TcpClient cliente)
        {
            try
            {
                return cliente.Client.Poll(0, SelectMode.SelectRead) && cliente.Client.Available == 0;
            }
            catch (SocketException) { return true; }
            catch (ObjectDisposedException) { return true; }
        }

        static void Responder(NetworkStream fluxo, string status, string corpo)
        {
            byte[] dados = Encoding.UTF8.GetBytes(corpo);
            byte[] cabecalho = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: application/json\r\nContent-Length: "
                + dados.Length + "\r\nConnection: close\r\n\r\n");
            fluxo.Write(cabecalho, 0, cabecalho.Length);
            fluxo.Write(dados, 0, dados.Length);
        }

        static Dictionary<string, object> Ler(byte[] corpo)
        {
            if (corpo == null || corpo.Length == 0) return null;
            var leitor = new JavaScriptSerializer();
            leitor.MaxJsonLength = int.MaxValue;
            // Tolera o BOM que alguns shells (PowerShell) põem na frente do texto.
            string texto = Encoding.UTF8.GetString(corpo).TrimStart('﻿');
            return leitor.DeserializeObject(texto) as Dictionary<string, object>;
        }

        // caminho = "MÉTODO /caminho" (ex.: "POST /evento"), tirado da primeira linha.
        // deFora = veio com Origin (navegador) ou com Host que não é esta máquina.
        static byte[] LerRequisicao(NetworkStream fluxo, out string caminho, out int processoJanela, out string processoClaude,
            out bool deFora)
        {
            caminho = null;
            processoJanela = 0;
            processoClaude = null;
            deFora = false;
            var lido = new MemoryStream();
            var bloco = new byte[8192];
            int fimCabecalho = -1;
            while (fimCabecalho < 0)
            {
                int n = fluxo.Read(bloco, 0, bloco.Length);
                if (n <= 0) return null;
                lido.Write(bloco, 0, n);
                fimCabecalho = AcharFimCabecalho(lido.GetBuffer(), (int)lido.Length);
                if (fimCabecalho < 0 && lido.Length > 64 * 1024) return null;
            }

            string cabecalho = Encoding.ASCII.GetString(lido.GetBuffer(), 0, fimCabecalho);
            string[] primeira = cabecalho.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ');
            if (primeira.Length >= 2) caminho = primeira[0] + " " + primeira[1];
            int tamanho = 0;
            bool pedeContinuar = false;
            foreach (string linha in cabecalho.Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                int doisPontos = linha.IndexOf(':');
                if (doisPontos < 0) continue;
                string nome = linha.Substring(0, doisPontos).Trim().ToLowerInvariant();
                string valor = linha.Substring(doisPontos + 1).Trim().ToLowerInvariant();
                if (nome == "content-length") int.TryParse(valor, out tamanho);
                else if (nome == "expect" && valor == "100-continue") pedeContinuar = true;
                else if (nome == "x-processo-janela") int.TryParse(valor, out processoJanela);
                else if (nome == "x-processo-claude") processoClaude = valor;
                else if (nome == "origin") deFora = true; // navegador: os hooks nunca mandam
                else if (nome == "host" && Array.IndexOf(HostsDaMaquina, valor) < 0) deFora = true;
            }
            if (deFora || caminho == "GET /estado") return null;
            if (tamanho <= 0 || tamanho > TamanhoMaximo) return null;
            // O curl pede licença antes de mandar corpos grandes.
            if (pedeContinuar) fluxo.Write(Continuar, 0, Continuar.Length);

            int inicioCorpo = fimCabecalho + 4;
            var corpo = new MemoryStream(tamanho);
            corpo.Write(lido.GetBuffer(), inicioCorpo, (int)lido.Length - inicioCorpo);
            while (corpo.Length < tamanho)
            {
                int n = fluxo.Read(bloco, 0, bloco.Length);
                if (n <= 0) break;
                corpo.Write(bloco, 0, n);
            }
            return corpo.ToArray();
        }

        static int AcharFimCabecalho(byte[] dados, int tamanho)
        {
            for (int i = 0; i + 3 < tamanho; i++)
                if (dados[i] == '\r' && dados[i + 1] == '\n' && dados[i + 2] == '\r' && dados[i + 3] == '\n') return i;
            return -1;
        }
    }
}

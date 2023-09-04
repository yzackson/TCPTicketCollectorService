using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Configuration;
using System.Timers;
using System.Runtime.InteropServices;
using log4net;

namespace TCPTicketCollectorService
{
    public partial class TCPTicketCollectorService : ServiceBase
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        readonly TcpListener _TCPServer;
        readonly Timer _timer = new Timer(1000);

        readonly ILog logger;
        readonly ILog ticket;


        bool _resetTicket = true;

        public TCPTicketCollectorService()
        {
            InitializeComponent();

            _TCPServer = new TcpListener(IPAddress.Parse(ConfigurationManager.AppSettings["IPAddress"]), Int32.Parse(ConfigurationManager.AppSettings["Port"]));
            _TCPServer.Start();

            logger = LogManager.GetLogger("RollingLogFileAppender");
            logger.Debug($"Iniciando serviço.");
            logger.Debug($"Endereço para conexão:{ ConfigurationManager.AppSettings["IPAddress"] + ":" + ConfigurationManager.AppSettings["Port"]}. Aguardando conexão...");
            ticket = LogManager.GetLogger("TicketLog");
            logger.Debug("Ticket logger instanciado");
            _timer.Elapsed += ReadFromClient;
            _timer.Start();
            logger.Debug("Timer de leitura de conexão TCP inciado");
        }

        public void ReadFromClient(object sender, ElapsedEventArgs args)
        {
            _timer.Stop();
            logger.Info("Nenhuma central conectada.");

            
            if(DateTime.Now.TimeOfDay.Hours == 0 && DateTime.Now.TimeOfDay.Minutes == 0)
            {
                if (_resetTicket)
                {
                    TicketCreated();

                    _resetTicket = false;
                }
            } else
            {
                _resetTicket = true;
            }
            
            

            if (_TCPServer.Pending())
            {
                var client = _TCPServer.AcceptTcpClient();

                logger.Debug($"Conectado a central {client.Client.RemoteEndPoint}\nIniciando leitura de dados. Aguardando 5 segundos à espera de dados");
                System.Threading.Thread.Sleep(5000);
                try
                {
                    NetworkStream stream = client.GetStream();

                    
                    // Buffer for reading data
                    Byte[] bytes = new Byte[256];
                    int i;

                    
                    // First check if there is data available
                    while (client.Available > 0)
                    {
                        logger.Debug($"Tem dados disponíveis");

                        // Loop to receive all the data sent by the client.
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            // Translate data bytes to a ASCII string.
                            string data = Encoding.ASCII.GetString(bytes, 0, i);
                            logger.Debug($"Recebendo informação da central e escrevendo no arquivo");
                            try
                            {
                                ticket.Info(data.Replace(",", ";"));
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"Não foi possível escrever no arquivo.\n{ex}");
                            }

                            // Await one second to receive more data, if receive the loop will proceed otherwise will be 
                            System.Threading.Thread.Sleep(1000);
                            if (client.Available == 0)
                                break;
                        }
                    }
                    logger.Error($"Sem dados a serem lidos.");
                    client.Close();
                    logger.Debug($"Client closed.");
                }
                catch (SocketException e)
                {
                    //eventLog1.WriteEntry($"Erro na leitura de dados.\n\n{e}", EventLogEntryType.Error);
                    logger.Error($"Erro na leitura de dados.\n{e}");
                }
            }
            _timer.Start();
        }

        public void TicketCreated()
        {
            logger.Debug($"Novo arquivo de Ticket gerado.");
            ticket.Info("Horario inicial da chamada;Tempo de conexao;Tempo de toque;Chamador;Direcao;Numero chamado;Numero discado;Codigo de conta;E interno;ID da chamada;Continuacao;Dispositivo da parte1;Nome da parte1;Dispositivo da parte2;Nome da parte2;Tempo em espera;Tempo de estacionamento;Autorizacao valida;Codigo de autorizacao;Usuario cobrado;Cobranca de chamada;Moeda;Valor na ultima mudanca de usuario;Unidades de chamada;Unidades na ultima mudanca de usuario;Custo por unidade;Marcacao;Causa do destino externo;ID do destino externo;Numero do destino externo;Endereco IP do servidor do chamador;ID exclusiva da chamada para o ramal do chamador;Endereco IP do servidor do receptor da chamada;ID exclusiva da chamada para o ramal chamado;Horario do registro SMDR;Diretriz de consentimento do chamador;Verificacao do numero chamador;Outros\n");
        }









        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(ServiceHandle, ref serviceStatus);

            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(ServiceHandle, ref serviceStatus);
        }

        protected override void OnStop()
        {// Update the service state to Stop Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            //eventLog1.WriteEntry("Parando serviço.");
            logger.Info("Parando serviço");

            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);


            _TCPServer.Stop();
        }


        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        }




    }
}


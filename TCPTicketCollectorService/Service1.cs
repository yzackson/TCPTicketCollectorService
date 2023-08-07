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

namespace TCPTicketCollectorService
{
    public partial class TCPTicketCollectorService : ServiceBase
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        string path;
        string filePath;
        string fileName;
        TcpClient client;
        TcpListener server;

        public TCPTicketCollectorService()
        {
            InitializeComponent();

            eventLog1 = new EventLog();
            if (!EventLog.SourceExists("TicketCollectorService"))
            {
                EventLog.CreateEventSource(
                    "TicketCollectorService", "NewLog");
            }
            eventLog1.Source = "TicketCollectorService";
            eventLog1.Log = "NewLog";

            try
            {
                // Set the TcpListener.
                Int32 port = Int32.Parse(ConfigurationManager.AppSettings["Port"]);
                IPAddress localAddr = IPAddress.Parse(ConfigurationManager.AppSettings["IPAddress"]);
                // TcpListener server = new TcpListener(port);
                server = new TcpListener(localAddr, port);
            }
            catch
            {
                eventLog1.WriteEntry("Erro na instanciação do TcpListener", EventLogEntryType.Error);
            } finally
            {
                try
                {
                    // Start listening for client requests.
                    server.Start();

                    eventLog1.WriteEntry("Servidor iniciado.", EventLogEntryType.Information);
                    eventLog1.WriteEntry($"Servidor iniciado.\n\nEndereço local de conexão:{ConfigurationManager.AppSettings["IPAddress"] +":"+ ConfigurationManager.AppSettings["Port"]}\nPasta de destino: {ConfigurationManager.AppSettings["OutputFolder"]}\nIntervalo de leitura: {ConfigurationManager.AppSettings["CheckInterval"]} seg", EventLogEntryType.Information);
                    eventLog1.WriteEntry("Aguardando conexão... ", EventLogEntryType.Information);

                    server.AcceptTcpClientAsync().ContinueWith(result => {
                        client = result.Result;
                        eventLog1.WriteEntry("Conectado ao PABX", EventLogEntryType.Information);
                        // TODO: Insert monitoring activities here.
                        eventLog1.WriteEntry("Monitorando o sistema.", EventLogEntryType.Information);
                    });
                }
                catch (Exception ex)
                {
                    eventLog1.WriteEntry($"Não foi possível iniciar o servidor. {ex}", EventLogEntryType.Error);
                }
            }
        }

        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(ServiceHandle, ref serviceStatus);

            //eventLog1.WriteEntry("Iniciando serviço.");

            filePath = ConfigurationManager.AppSettings["OutputFolder"];
            CheckFile();

            Timer timer = new Timer();
            timer.Interval = double.Parse(ConfigurationManager.AppSettings["CheckInterval"]); // 30 seconds
            timer.Elapsed += new ElapsedEventHandler(OnTimer);
            timer.Start();


            


            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(ServiceHandle, ref serviceStatus);
        }

        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            if(client == null || !client.Connected) {
                eventLog1.WriteEntry("Nenhuma central conectada.", EventLogEntryType.Warning);
                return;
            }

            

            try
            {
                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();

                // Buffer for reading data
                Byte[] bytes = new Byte[256];
                int i;

                CheckFile();

                // Loop to receive all the data sent by the client.
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Translate data bytes to a ASCII string.
                    string data = Encoding.ASCII.GetString(bytes, 0, i);
                    eventLog1.WriteEntry($"Recebendo informação da central e escrevendo no arquivo {fileName}", EventLogEntryType.Information);

                    // Write array of strings to a file using WriteAllLines.
                    // If the file does not exists, it will create a new file.
                    // This method automatically opens the file, writes to it, and closes file
                    //eventLog1.WriteEntry($"Escrevendo no arquivo.", EventLogEntryType.Information);
                    if (!File.Exists(path))
                    {
                        var file = File.AppendText(path);
                        file.Write("Horario inicial da chamada;Tempo de conexao;Tempo de toque;Chamador;Direcao;Numero chamado;Numero discado;Codigo de conta;E interno;ID da chamada;Continuacao;Dispositivo da parte1;Nome da parte1;Dispositivo da parte2;Nome da parte2;Tempo em espera;Tempo de estacionamento;Autorizacao valida;Codigo de autorizacao;Usuario cobrado;Cobranca de chamada;Moeda;Valor na ultima mudanca de usuario;Unidades de chamada;Unidades na ultima mudanca de usuario;Custo por unidade;Marcacao;Causa do destino externo;ID do destino externo;Numero do destino externo;Endereco IP do servidor do chamador;ID exclusiva da chamada para o ramal do chamador;Endereco IP do servidor do receptor da chamada;ID exclusiva da chamada para o ramal chamado;Horario do registro SMDR;Diretriz de consentimento do chamador;Verificacao do numero chamador;Outros\n");
                        file.Close();
                    }
                    Write2File(data.Replace(",", ";"));

                    /*byte[] msg = Encoding.ASCII.GetBytes(data);
                    // Send back a response.
                    stream.Write(msg, 0, msg.Length);
                    Console.WriteLine("Sent: {0}", data);*/
                }
            }
            catch (SocketException e)
            {
                eventLog1.WriteEntry($"Erro na leitura de dados.\n\n{e}", EventLogEntryType.Error);
            }
            finally
            {
                server.Stop();
            }
        }

        protected override void OnStop()
        {// Update the service state to Stop Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            eventLog1.WriteEntry("Parando serviço.");

            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
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
        };


        public void Write2File(string data)
        {
            try
            {
                var file = File.AppendText(path);
                file.Write(data);
                file.Close();
            } catch (Exception ex) {
                eventLog1.WriteEntry($"Não foi possível escrever o log.\n\n{ex}", EventLogEntryType.Error);
            }
        }

        public void CheckFile()
        {
            string day = DateTime.Now.Day < 10 ? "0" + DateTime.Now.Day.ToString() : DateTime.Now.Day.ToString();
            string month = DateTime.Now.Month < 10 ? "0" + DateTime.Now.Month.ToString() : DateTime.Now.Month.ToString();
            string year = DateTime.Now.Year.ToString();

            fileName = $"Ticket_{day}{month}{year}.csv";
            path = Path.Combine(filePath, fileName);
        }
    }
}


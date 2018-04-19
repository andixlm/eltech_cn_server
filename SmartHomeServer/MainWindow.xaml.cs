using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SmartHomeServer
{
    public partial class MainWindow : Window
    {
        private static readonly int BUFFER_SIZE = 8192;

        private static readonly string IPADDRESS_LOG_LABEL = "IP Address: ";

        private static readonly string LOCALHOST_IPADDRESS = "127.0.0.1";

        private static readonly string PORT_LOG_LABEL = "Port: ";
        private static readonly int MINIMAL_PORT_VALUE = 1024;
        private static readonly int MAXIMAL_PORT_VALUE = 49151;

        private static readonly string CONNECTION_LOG_LABEL = "Connection: ";
        private static readonly string CONNECTION_UP = "up";
        private static readonly string CONNECTION_WAIT = "wait";
        private static readonly string CONNECTION_DOWN = "down";
        private static readonly string CONNECTION_ERR = "err";

        private static readonly string UPDATE_INTERVAL_LOG_LABEL = "Update interval: ";

        private static readonly string NETWORK_LOG_LABEL = "Network: ";

        private static readonly string NETWORK_DEVICE_ARG = "Device: ";
        private static readonly string NETWORK_DEVICE_THERMOMETER = "Thermometer";
        private static readonly string NETWORK_TEMPERATURE_ARG = "Temparatute: ";
        private static readonly string NETWORK_UPDATE_INTERVAL_ARG = "Update interval: ";

        private static readonly int MAXIMAL_CLIENTS_NUM_VALUE = 3;
        private static readonly int MAXIMAL_THREADS_NUM_VALUE = 3;

        private static readonly Random sRandom = new Random();

        private TcpListener _NetworkListener;

        private Thread[] _ListenerThreads;
        private TcpClient[] _Sockets;

        private int _ThermometerIdx;

        private int _Port;

        public MainWindow()
        {
            InitializeComponent();

            Init();
            Configure();
        }

        private void Init()
        {
            _NetworkListener = default(TcpListener);

            _ListenerThreads = new Thread[MAXIMAL_THREADS_NUM_VALUE];
            _Sockets = new TcpClient[MAXIMAL_CLIENTS_NUM_VALUE];
        }

        private void Configure()
        {
            _Port = sRandom.Next(MINIMAL_PORT_VALUE, MAXIMAL_PORT_VALUE);
            PortTextBox.Text = _Port.ToString();

            StartServerButton.IsEnabled = true;
            StartServerButton.Click += (sender, e) =>
            {
                StartServer();
            };

            StopServerButton.IsEnabled = false;
            StopServerButton.Click += (sender, e) =>
            {
                StopServer();
            };

            for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
            {
                _ListenerThreads[idx] = new Thread(new ThreadStart(delegate ()
                {
                    try
                    {
                        _Sockets[idx] = _NetworkListener.AcceptTcpClient();
                        HandleNewClient(_Sockets[idx], idx);
                    }
                    catch (Exception exc)
                    {
                        Dispatcher.Invoke(delegate ()
                        {
                            LogTextBlock.AppendText(NETWORK_LOG_LABEL + "Unable to establish connection with client: " + exc.Message + "\n");
                            LogTextBlock.ScrollToEnd();
                        });
                    }
                }));
            }
        }

        private void StartServer()
        {
            try
            {
                _Port = int.Parse(PortTextBox.Text);
                if (_Port < MINIMAL_PORT_VALUE || _Port > MAXIMAL_PORT_VALUE)
                {
                    throw new Exception(string.Format("Incorrect port value. [{0}; {1}] ports are allowed.",
                        MINIMAL_PORT_VALUE, MAXIMAL_PORT_VALUE));
                }

                _NetworkListener = new TcpListener(IPAddress.Parse(LOCALHOST_IPADDRESS), _Port);
                StartServerButton.IsEnabled = !StartServerButton.IsEnabled;
                StopServerButton.IsEnabled = !StopServerButton.IsEnabled;

                for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
                {
                    _ListenerThreads[idx].Start();
                }

                ServerStatusLabel.Content = CONNECTION_UP;

                LogTextBlock.AppendText(NETWORK_LOG_LABEL + "Server successfully started." + "\n");
                LogTextBlock.ScrollToEnd();
            }
            catch (Exception exc)
            {
                LogTextBlock.AppendText(NETWORK_LOG_LABEL + "Unable to start server: " + exc.Message + "\n");
                LogTextBlock.ScrollToEnd();
                return;
            }
        }

        private void StopServer()
        {
            _NetworkListener.Stop();
        }

        private void Send(TcpClient socket, byte[] bytes)
        {
            NetworkStream stream = socket.GetStream();
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private void HandleNewClient(TcpClient socket, int socketIdx)
        {
            byte[] bytes = new byte[BUFFER_SIZE];

            NetworkStream stream = socket.GetStream();
            stream.Read(bytes, 0, socket.ReceiveBufferSize);

            /// TODO: Parse, cache received data and process later.
            string data = Encoding.Unicode.GetString(bytes);
            data = data.Substring(0, data.IndexOf(';') + 1);

            if (string.IsNullOrEmpty(data))
            {
                Dispatcher.Invoke(delegate ()
                {
                    LogTextBlock.AppendText(NETWORK_LOG_LABEL + "Empty data received." + "\n");
                    LogTextBlock.ScrollToEnd();
                });
                return;
            }

            int idx;
            if ((idx = data.IndexOf(NETWORK_DEVICE_ARG)) >= 0)
            {
                string device = data.Substring(idx + NETWORK_DEVICE_ARG.Length, data.IndexOf("$)"));
                if (string.Equals(device, NETWORK_DEVICE_THERMOMETER))
                {
                    _ThermometerIdx = idx;
                    HandleThermometer();
                }
                else /// TODO: Handle other devices.
                {
                    Dispatcher.Invoke(delegate ()
                    {
                        LogTextBlock.AppendText(NETWORK_LOG_LABEL + "Unknown device tried to connect." + "\n");
                        LogTextBlock.ScrollToEnd();
                    });
                }
            }
            else
            {
                Dispatcher.Invoke(delegate ()
                {
                    LogTextBlock.AppendText(NETWORK_LOG_LABEL + "Unknown data received." + "\n");
                    LogTextBlock.ScrollToEnd();
                });
            }
        }

        private void HandleThermometer()
        {
            Dispatcher.Invoke(delegate ()
            {
                ThermometerConnectionValueLabel.Content = CONNECTION_UP;
                /// TODO: Receive update interval.
            });
        }

        private void ProcessData(string data)
        {

        }
    }
}

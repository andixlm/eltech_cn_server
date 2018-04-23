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

        private static readonly char DELIMITER = ';';

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
        private static readonly string NETWORK_DEVICE_THERMOMETER_LOG_LABEL = "Thermometer: ";

        private static readonly string NETWORK_DEVICE_ARG = "Device: ";
        private static readonly string NETWORK_DEVICE_THERMOMETER = "Thermometer";
        private static readonly string NETWORK_TEMPERATURE_ARG = "Temperatute: ";
        private static readonly string NETWORK_UPDATE_INTERVAL_ARG = "Update interval: ";
        private static readonly string NETWORK_METHOD_TO_INVOKE_ARG = "Method: ";

        private static readonly string NETWORK_METHOD_TO_UPDATE_TEMP = "UPDATE_TEMP";

        private static readonly int MAXIMAL_CLIENTS_NUM_VALUE = 3;
        private static readonly int MAXIMAL_THREADS_NUM_VALUE = 3;

        private static readonly Random sRandom = new Random();

        private bool _ShouldScrollToEnd;

        private TcpListener _NetworkListener;

        private Thread[] _ListenerThreads;
        private Thread[] _WorkerThreads;

        private TcpClient[] _Sockets;

        private Mutex _ListenerMutex;
        private Mutex _ReceiveMutex;
        private Mutex _SendMutex;

        private List<string> _Cache;
        private List<string> _ThermometerCache;

        private int _SocketsIdx;
        private int _ThermometerIdx;

        private int _ThermometerUpdateInterval;

        private int _Port;

        public MainWindow()
        {
            InitializeComponent();

            Init();
            Configure();
        }

        private void Init()
        {
            _ShouldScrollToEnd = true;

            _NetworkListener = default(TcpListener);

            _ListenerThreads = new Thread[MAXIMAL_THREADS_NUM_VALUE];
            _WorkerThreads = new Thread[MAXIMAL_THREADS_NUM_VALUE];

            _SocketsIdx = 0;
            _Sockets = new TcpClient[MAXIMAL_CLIENTS_NUM_VALUE];

            _ListenerMutex = new Mutex();
            _ReceiveMutex = new Mutex();
            _SendMutex = new Mutex();

            _Cache = new List<string>();
            _ThermometerCache = new List<string>();

            _ThermometerUpdateInterval = 1;
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

            AdjustThermometerBlock(false);

            for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
            {
                _ListenerThreads[idx] = new Thread(new ThreadStart(delegate ()
                {
                    try
                    {
                        _ListenerMutex.WaitOne();
                        TcpClient socket = _NetworkListener.AcceptTcpClient();

                        _Sockets[_SocketsIdx] = socket;
                        HandleNewClient(ref _Sockets[_SocketsIdx], _SocketsIdx);

                        _SocketsIdx++;
                        /// TODO: Check if more than three connections.
                        _ListenerMutex.ReleaseMutex();
                    }
                    catch (Exception exc)
                    {
                        Log(NETWORK_LOG_LABEL + "Unable to establish connection with client: " + exc.Message + "\n");
                    }
                }));
            }

            UpdateIntervalSetButton.Click += (sender, e) =>
            {
                try
                {
                    _ThermometerUpdateInterval = int.Parse(UpdateIntervalTextBlock.Text);
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        UPDATE_INTERVAL_LOG_LABEL + string.Format("Set to {0}\n", _ThermometerUpdateInterval));

                    if (_Sockets[_ThermometerIdx] != null && _Sockets[_ThermometerIdx].Connected)
                    {
                        SendThermometerUpdateInterval(ref _Sockets[_ThermometerIdx], _ThermometerUpdateInterval);
                    }
                }
                catch (FormatException exc)
                {
                    Log(UPDATE_INTERVAL_LOG_LABEL + exc.Message + "\n");
                }
            };
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
                _NetworkListener.Start();
                StartServerButton.IsEnabled = !StartServerButton.IsEnabled;
                StopServerButton.IsEnabled = !StopServerButton.IsEnabled;

                for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
                {
                    _ListenerThreads[idx].Start();
                }

                ServerStatusLabel.Content = CONNECTION_UP;

                Log(NETWORK_LOG_LABEL + "Server successfully started." + "\n");
            }
            catch (Exception exc)
            {
                Log(NETWORK_LOG_LABEL + "Unable to start server: " + exc.Message + "\n");
                return;
            }
        }

        private void StopServer()
        {
            for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
            {
                _ListenerThreads[idx].Abort();
            }

            _NetworkListener.Stop();
        }

        private void Send(ref TcpClient socket, ref byte[] bytes)
        {
            _SendMutex.WaitOne();

            try
            {
                NetworkStream stream = socket.GetStream();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            catch (System.IO.IOException exc)
            {
                Log(NETWORK_LOG_LABEL +
                    (exc.InnerException != null ? exc.InnerException.Message : exc.Message) + "\n");
            }

            _SendMutex.ReleaseMutex();
        }

        private void Receive(ref TcpClient socket, ref byte[] bytes)
        {
            _ReceiveMutex.WaitOne();

            try
            {
                NetworkStream stream = socket.GetStream();
                stream.Read(bytes, 0, socket.ReceiveBufferSize);
            }
            catch (System.IO.IOException exc)
            {
                Log(NETWORK_LOG_LABEL +
                    (exc.InnerException != null ? exc.InnerException.Message : exc.Message) + "\n");
            }

            _ReceiveMutex.ReleaseMutex();
        }

        private void HandleNewClient(ref TcpClient socket, int socketIdx)
        {
            byte[] bytes = new byte[BUFFER_SIZE];
            Receive(ref socket, ref bytes);

            string data = Encoding.Unicode.GetString(bytes);
            if (string.IsNullOrEmpty(data) && data.Equals(""))
            {
                Log(NETWORK_LOG_LABEL + "Empty data received." + "\n");
                return;
            }

            string first = CacheData(data, ref _Cache);

            int idx;
            if ((idx = first.IndexOf(NETWORK_DEVICE_ARG)) >= 0)
            {
                int startIdx = idx + NETWORK_DEVICE_ARG.Length, endIdx = first.IndexOf(DELIMITER);
                string device = first.Substring(startIdx, endIdx - startIdx);
                if (string.Equals(device, NETWORK_DEVICE_THERMOMETER))
                {
                    _ThermometerIdx = idx;
                    MoveData(ref _Cache, ref _ThermometerCache);
                    HandleThermometer();
                }
                else /// TODO: Handle other devices.
                {
                    Log(NETWORK_LOG_LABEL + "Unknown device tried to connect." + "\n");
                }
            }
            else
            {
                Log(NETWORK_LOG_LABEL + "Unknown data received." + "\n");
            }
        }

        private void HandleThermometer()
        {
            AdjustThermometerBlock(true);
            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER + " connected" + "\n");

            _WorkerThreads[_ThermometerIdx] = new Thread(new ThreadStart(delegate ()
            {
                while (_Sockets[_ThermometerIdx].Connected)
                {
                    ProcessThermometerData(ref _ThermometerCache);

                    byte[] bytes = new byte[BUFFER_SIZE];
                    Receive(ref _Sockets[_ThermometerIdx], ref bytes);

                    string data = Encoding.Unicode.GetString(bytes);
                    if (!string.IsNullOrEmpty(data) && !data.Equals(""))
                        ProcessThermometerData(CacheData(data, ref _ThermometerCache));
                }
            }));
            _WorkerThreads[_ThermometerIdx].Start();
        }

        string CacheData(string data, ref List<string> cache)
        {
            int delimiterIdx = data.IndexOf(DELIMITER);
            string first = data.Substring(0, delimiterIdx + 1);

            data = data.Substring(delimiterIdx + 1, data.Length - delimiterIdx - 1);
            for (delimiterIdx = data.IndexOf(DELIMITER); delimiterIdx >= 0; delimiterIdx = data.IndexOf(DELIMITER))
            {
                cache.Add(data.Substring(0, delimiterIdx + 1));
                data = data.Substring(delimiterIdx + 1, data.Length - delimiterIdx - 1);
            }

            return first;
        }

        private void ProcessThermometerData(string data)
        {
            int idx;
            if ((idx = data.IndexOf(NETWORK_UPDATE_INTERVAL_ARG)) >= 0)
            {
                try
                {
                    int startIdx = idx + NETWORK_UPDATE_INTERVAL_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    int updateInterval = int.Parse(data.Substring(startIdx, endIdx - startIdx));

                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        string.Format("Received update interval: {0}", updateInterval) + "\n");

                    Dispatcher.Invoke(delegate ()
                    {
                        UpdateIntervalTextBlock.Text = updateInterval.ToString();
                    });
                }
                catch (FormatException)
                {
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        "Received incorrect update interval" + "\n");
                }
            }
            else if ((idx = data.IndexOf(NETWORK_TEMPERATURE_ARG)) >= 0)
            {
                try
                {
                    int startIdx = idx + NETWORK_TEMPERATURE_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    double temperature = double.Parse(data.Substring(startIdx, endIdx - startIdx));

                    Dispatcher.Invoke(delegate ()
                    {
                        TemperatureValueLabel.Content = temperature.ToString("F2");
                    });

                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        string.Format("Received temperature: {0}", temperature.ToString("F2")) + "\n");
                }
                catch (FormatException)
                {
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        "Received incorrect temperature" + "\n");
                }
            }
            else
            {
                Log(string.Format(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                    "Unknown data received: \"{0}\"" + "\n", data));
            }
        }

        private void ProcessThermometerData(ref List<string> dataSet)
        {
            foreach (string data in dataSet)
            {
                if (string.IsNullOrEmpty(data) && !data.Equals("")) ProcessThermometerData(data);
            }

            dataSet.Clear();
        }

        private void SendThermometerUpdateInterval(ref TcpClient socket, double updateInterval)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(string.Format(NETWORK_UPDATE_INTERVAL_ARG + "{0}" + DELIMITER, updateInterval));

            Send(ref socket, ref bytes);

            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Sent update interval" + "\n");
        }

        private void SendThermometerMethodToInvoke(ref TcpClient socket, string method)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(NETWORK_METHOD_TO_INVOKE_ARG + method + DELIMITER);

            Send(ref socket, ref bytes);

            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Temperature update's been requested." + "\n");
        }

        private void AdjustThermometerBlock(bool isConnected)
        {
            Dispatcher.Invoke(delegate ()
            {
                ThermometerConnectionValueLabel.Content = isConnected ? CONNECTION_UP
                                                                      : CONNECTION_DOWN;

                UpdateIntervalSetButton.IsEnabled = isConnected;
                TemperatureUpdateButton.IsEnabled = isConnected;
            });
        }

        private void MoveData(ref List<string> from, ref List<string> to)
        {
            foreach (string piece in from)
            {
                to.Add(piece);
            }

            from.Clear();
        }

        private void Log(string info)
        {
            Dispatcher.Invoke(delegate ()
            {
                LogTextBlock.AppendText(info);
                if (_ShouldScrollToEnd) LogTextBlock.ScrollToEnd();
            });
        }
    }
}

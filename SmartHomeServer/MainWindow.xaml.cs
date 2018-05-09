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
        private static readonly string NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL = "Light Switcher: ";
        private static readonly string NETWORK_DEVICE_THERMOMETER_LOG_LABEL = "Thermometer: ";
        private static readonly string NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL = "Motion Detector: ";

        private static readonly string NETWORK_DEVICE_ARG = "Device: ";
        private static readonly string NETWORK_DEVICE_LIGHT_SWITCHER = "LightSwitcher";
        private static readonly string NETWORK_DEVICE_THERMOMETER = "Thermometer";
        private static readonly string NETWORK_DEVICE_MOTION_DETECTOR = "MotionDetector";

        private static readonly string NETWORK_LIGHTS_ARG = "Lights: ";
        private static readonly string NETWORK_TEMPERATURE_ARG = "Temperatute: ";
        private static readonly string NETWORK_UPDATE_INTERVAL_ARG = "Update interval: ";
        private static readonly string NETWORK_TIME_ARG = "Time: ";

        private static readonly string NETWORK_METHOD_TO_INVOKE_ARG = "Method: ";
        private static readonly string NETWORK_STATUS_ARG = "Status: ";

        private static readonly string NETWORK_LIGHT_SWITCHER_METHOD_TO_SWITCH = "SWITCH";
        private static readonly string NETWORK_THERMOMETER_METHOD_TO_UPDATE_TEMP = "UPDATE_TEMP";
        private static readonly string NETWORK_METHOD_TO_DISCONNECT = "DISCONNECT";
        private static readonly string NETWORK_METHOD_TO_REQUEST_STATUS = "REQUEST_STATUS";

        private static readonly int MAXIMAL_CLIENTS_NUM_VALUE = 3;
        private static readonly int MAXIMAL_THREADS_NUM_VALUE = 3;

        private static readonly int DEVICE_STATUS_UP = 42;
        private static readonly int DEVICE_STATUS_CHECK_TIMEOUT = 3000;

        private static readonly Random sRandom = new Random();

        private static readonly DateTime sEpochTime = new DateTime(1970, 1, 1);

        private bool _VerboseLogging;
        private bool _ShouldScrollToEnd;

        private TcpListener _NetworkListener;

        private Thread[] _ListenerThreads;
        private Thread[] _WorkerThreads;
        private Thread[] _StatusThreads;

        private TcpClient[] _Sockets;

        private Mutex _ListenerMutex;
        private Mutex _DataMutex;

        private List<string> _Cache;
        private List<string> _LightSwitcherCache;
        private List<string> _ThermometerCache;
        private List<string> _MotionDetectorCache;

        private bool _LightSwitcherStatus;

        private int _ThermometerUpdateInterval;

        private DateTime _MotionDetectorTime;

        private const int _LightSwitcherIdx = 0;
        private const int _ThermometerIdx = 1;
        private const int _MotionDetectorIdx = 2;

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
            _WorkerThreads = new Thread[MAXIMAL_THREADS_NUM_VALUE];
            _StatusThreads = new Thread[MAXIMAL_THREADS_NUM_VALUE];

            _Sockets = new TcpClient[MAXIMAL_CLIENTS_NUM_VALUE];

            _ListenerMutex = new Mutex();
            _DataMutex = new Mutex();

            _Cache = new List<string>();
            _LightSwitcherCache = new List<string>();
            _ThermometerCache = new List<string>();
            _MotionDetectorCache = new List<string>();
        }

        private void Configure()
        {
            _LightSwitcherStatus = false;
            _ThermometerUpdateInterval = 1; ;
            _MotionDetectorTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            _VerboseLogging = false;
            VerobseLoggingCheckBox.IsChecked = _VerboseLogging;
            VerobseLoggingCheckBox.Checked += (sender, e) =>
            {
                _VerboseLogging = true;
            };
            VerobseLoggingCheckBox.Unchecked += (sender, e) =>
            {
                _VerboseLogging = false;
            };

            _ShouldScrollToEnd = true;
            ScrollToEndCheckBox.IsChecked = _ShouldScrollToEnd;
            ScrollToEndCheckBox.Checked += (sender, e) =>
            {
                _ShouldScrollToEnd = true;
            };
            ScrollToEndCheckBox.Unchecked += (sender, e) =>
            {
                _ShouldScrollToEnd = false;
            };

            Closed += (sender, e) =>
            {
                if (!StartServerButton.IsEnabled)
                {
                    new Thread(new ThreadStart(delegate ()
                    {
                        StopServer();
                    })).Start();
                }
            };

            _Port = sRandom.Next(MINIMAL_PORT_VALUE, MAXIMAL_PORT_VALUE);
            PortTextBox.Text = _Port.ToString();

            StartServerButton.IsEnabled = true;
            StartServerButton.Click += (sender, e) =>
            {
                new Thread(new ThreadStart(delegate ()
                {
                    StartServer();
                })).Start();
            };

            StopServerButton.IsEnabled = false;
            StopServerButton.Click += (sender, e) =>
            {
                new Thread(new ThreadStart(delegate ()
                {
                    StopServer();
                })).Start();
            };

            AdjustLightSwitcherBlock(false);
            AdjustThermometerBlock(false);
            AdjustMotionDetectorBlock(false);

            LightSwitcherSwitchButton.Click += (sender, e) =>
            {
                if (_Sockets[_LightSwitcherIdx] != null && _Sockets[_LightSwitcherIdx].Connected)
                {
                    SendMethodToInvoke(ref _Sockets[_LightSwitcherIdx], NETWORK_LIGHT_SWITCHER_METHOD_TO_SWITCH);
                }
            };

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
                    Log(UPDATE_INTERVAL_LOG_LABEL + exc.Message + '\n');
                }
            };

            TemperatureUpdateButton.Click += (sender, e) =>
            {
                if (_Sockets[_ThermometerIdx] != null && _Sockets[_ThermometerIdx].Connected)
                {
                    SendMethodToInvoke(ref _Sockets[_ThermometerIdx], NETWORK_THERMOMETER_METHOD_TO_UPDATE_TEMP);
                }
            };
        }

        private Thread ConfigureListenerThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    _ListenerMutex.WaitOne();
                    _NetworkListener.Start();
                    TcpClient socket = _NetworkListener.AcceptTcpClient();
                    _NetworkListener.Stop();
                    _ListenerMutex.ReleaseMutex();

                    HandleNewClient(ref socket);
                }
                catch (ThreadAbortException)
                {
                    try
                    {
                        _ListenerMutex.ReleaseMutex();
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + "Network listener was closed" + '\n');
                        }
                    }
                    catch (ApplicationException)
                    {
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Mutex's been tried to be released not by the owner thread." + '\n');
                        }
                    }
                }
                catch (SocketException)
                {
                    try
                    {
                        _ListenerMutex.ReleaseMutex();
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + "Network listener was closed" + '\n');
                        }
                    }
                    catch (ApplicationException)
                    {
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Mutex's been tried to be released not by the owner thread." + '\n');
                        }
                    }
                }
                catch (Exception exc)
                {
                    try
                    {
                        _ListenerMutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Mutex's been tried to be released not by the owner thread." + '\n');
                        }
                    }

                    Log(NETWORK_LOG_LABEL + "Unable to establish connection with client: " + exc.Message + '\n');
                }
            }));
        }

        private void ConfigureListenerThreads()
        {
            for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
            {
                _ListenerThreads[idx] = ConfigureListenerThread();
            }
        }

        private Thread ConfigureLightSwitcherWorkerThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_LightSwitcherIdx] != null && _Sockets[_LightSwitcherIdx].Connected)
                    {
                        ProcessLightSwitcherData(ref _LightSwitcherCache);

                        byte[] bytes = new byte[BUFFER_SIZE];
                        Receive(ref _Sockets[_LightSwitcherIdx], ref bytes);

                        string data = Encoding.Unicode.GetString(bytes);
                        ProcessLightSwitcherData(CacheData(data, ref _LightSwitcherCache));
                    }
                }
                catch (ThreadAbortException)
                {
                    if (_VerboseLogging)
                    {
                        Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL +
                            "Light switcher worker thread was terminated." + '\n');
                    }
                }
            }));
        }

        private Thread ConfigureLightSwitcherStatusThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_LightSwitcherIdx] != null && _Sockets[_LightSwitcherIdx].Connected)
                    {
                        SendMethodToInvoke(ref _Sockets[_LightSwitcherIdx], NETWORK_METHOD_TO_REQUEST_STATUS);
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL + "Status was requested." + '\n');
                        }

                        Thread.Sleep(DEVICE_STATUS_CHECK_TIMEOUT);
                    }
                }
                catch (ThreadAbortException)
                {
                    if (_VerboseLogging)
                    {
                        Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL +
                            "Light switcher status thread was terminated." + '\n');
                    }
                }
            }));
        }

        private Thread ConfigureThermometerWorkerThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_ThermometerIdx] != null && _Sockets[_ThermometerIdx].Connected)
                    {
                        ProcessThermometerData(ref _ThermometerCache);

                        byte[] bytes = new byte[BUFFER_SIZE];
                        Receive(ref _Sockets[_ThermometerIdx], ref bytes);

                        string data = Encoding.Unicode.GetString(bytes);
                        ProcessThermometerData(CacheData(data, ref _ThermometerCache));
                    }
                }
                catch (ThreadAbortException)
                {
                    if (_VerboseLogging)
                    {
                        Log(NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Thermometer worker thread was terminated." + '\n');
                    }
                }
            }));
        }

        private Thread ConfigureThermometerStatusThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_ThermometerIdx] != null && _Sockets[_ThermometerIdx].Connected)
                    {
                        SendMethodToInvoke(ref _Sockets[_ThermometerIdx], NETWORK_METHOD_TO_REQUEST_STATUS);
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Status was requested." + '\n');
                        }

                        Thread.Sleep(DEVICE_STATUS_CHECK_TIMEOUT);
                    }
                }
                catch (ThreadAbortException)
                {
                    if (_VerboseLogging)
                    {
                        Log(NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Status thread was terminated." + '\n');
                    }
                }
            }));
        }

        private Thread ConfigureMotionDetectorWorkerThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_MotionDetectorIdx] != null && _Sockets[_MotionDetectorIdx].Connected)
                    {
                        ProcessMotionDetectorData(ref _MotionDetectorCache);

                        byte[] bytes = new byte[BUFFER_SIZE];
                        Receive(ref _Sockets[_MotionDetectorIdx], ref bytes);

                        string data = Encoding.Unicode.GetString(bytes);
                        ProcessMotionDetectorData(CacheData(data, ref _MotionDetectorCache));
                    }
                }
                catch (ThreadAbortException)
                {
                    if (_VerboseLogging)
                    {
                        Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL +
                            "Motion detector worker thread was terminated." + '\n');
                    }
                }
            }));
        }

        private Thread ConfigureMotionDetectorStatusThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_MotionDetectorIdx] != null && _Sockets[_MotionDetectorIdx].Connected)
                    {
                        SendMethodToInvoke(ref _Sockets[_MotionDetectorIdx], NETWORK_METHOD_TO_REQUEST_STATUS);
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL + "Status was requested." + '\n');
                        }

                        Thread.Sleep(DEVICE_STATUS_CHECK_TIMEOUT);
                    }
                }
                catch (ThreadAbortException)
                {
                    if (_VerboseLogging)
                    {
                        Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL +
                            "Motion detector status thread was terminated." + '\n');
                    }
                }
            }));
        }

        private void StartServer()
        {
            try
            {
                Dispatcher.Invoke(delegate ()
                {
                    _Port = int.Parse(PortTextBox.Text);
                });
                if (_Port < MINIMAL_PORT_VALUE || _Port > MAXIMAL_PORT_VALUE)
                {
                    throw new Exception(string.Format("Incorrect port value. [{0}; {1}] ports are allowed.",
                        MINIMAL_PORT_VALUE, MAXIMAL_PORT_VALUE));
                }

                _NetworkListener = new TcpListener(IPAddress.Parse(LOCALHOST_IPADDRESS), _Port);

                ConfigureListenerThreads();
                for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
                {
                    _ListenerThreads[idx].Start();
                }

                Dispatcher.Invoke(delegate ()
                {
                    ServerStatusLabel.Content = CONNECTION_UP;
                });
                SwitchButtonsOnConnectionStatusChanged(true);
                Log(NETWORK_LOG_LABEL + "Server successfully started." + '\n');
            }
            catch (Exception exc)
            {
                Dispatcher.Invoke(delegate ()
                {
                    ServerStatusLabel.Content = CONNECTION_ERR;
                });
                Log(NETWORK_LOG_LABEL + "Unable to start server: " + exc.Message + '\n');
            }
        }

        private void StopServer()
        {
            _NetworkListener.Stop();

            for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
            {
                if (_WorkerThreads[idx] != null && _WorkerThreads[idx].IsAlive)
                {
                    _WorkerThreads[idx].Abort();
                }
                if (_ListenerThreads[idx] != null && _ListenerThreads[idx].IsAlive)
                {
                    _ListenerThreads[idx].Abort();
                }
                if (_Sockets[idx] != null)
                {
                    _Sockets[idx].Close();
                }
            }

            Dispatcher.Invoke(delegate ()
            {
                ServerStatusLabel.Content = CONNECTION_DOWN;
            });
            SwitchButtonsOnConnectionStatusChanged(false);
            Log(NETWORK_LOG_LABEL + "Server successfully stopped." + '\n');
        }

        private void Send(ref TcpClient socket, ref byte[] bytes)
        {
            try
            {
                NetworkStream stream = socket.GetStream();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            catch (System.IO.IOException exc)
            {
                CloseSocket(ref socket);

                if (_VerboseLogging)
                {
                    Log(NETWORK_LOG_LABEL +
                        (exc.InnerException != null ? exc.InnerException.Message : exc.Message) + '\n');
                }
            }
        }

        private void Receive(ref TcpClient socket, ref byte[] bytes)
        {
            try
            {
                NetworkStream stream = socket.GetStream();
                stream.Read(bytes, 0, socket.ReceiveBufferSize);
            }
            catch (System.IO.IOException exc)
            {
                CloseSocket(ref socket);

                if (_VerboseLogging)
                {
                    Log(NETWORK_LOG_LABEL +
                        (exc.InnerException != null ? exc.InnerException.Message : exc.Message) + '\n');
                }
            }
        }

        private void HandleNewClient(ref TcpClient socket)
        {
            byte[] bytes = new byte[BUFFER_SIZE];
            Receive(ref socket, ref bytes);

            string data = Encoding.Unicode.GetString(bytes);
            if (string.IsNullOrEmpty(data) || data.Equals(""))
            {
                Log(NETWORK_LOG_LABEL + "Empty data received." + '\n');
                return;
            }

            string first = CacheData(data, ref _Cache);

            int idx;
            if ((idx = first.IndexOf(NETWORK_DEVICE_ARG)) >= 0)
            {
                int startIdx = idx + NETWORK_DEVICE_ARG.Length, endIdx = first.IndexOf(DELIMITER);
                string device = first.Substring(startIdx, endIdx - startIdx);
                if (string.Equals(device, NETWORK_DEVICE_LIGHT_SWITCHER))
                {
                    _Sockets[_LightSwitcherIdx] = socket;
                    MoveData(ref _Cache, ref _LightSwitcherCache);
                    HandleLightSwitcher();
                }
                else if (string.Equals(device, NETWORK_DEVICE_THERMOMETER))
                {
                    _Sockets[_ThermometerIdx] = socket;
                    MoveData(ref _Cache, ref _ThermometerCache);
                    HandleThermometer();
                }
                else if (string.Equals(device, NETWORK_DEVICE_MOTION_DETECTOR))
                {
                    _Sockets[_MotionDetectorIdx] = socket;
                    MoveData(ref _Cache, ref _MotionDetectorCache);
                    HandleMotionDetector();
                }
                else
                {
                    Log(NETWORK_LOG_LABEL + "Unknown device tried to connect." + '\n');
                }
            }
            else
            {
                Log(NETWORK_LOG_LABEL + "Unknown data received." + '\n');
            }
        }

        private void HandleLightSwitcher()
        {
            AdjustLightSwitcherBlock(true);
            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL + "Connected" + '\n');

            _WorkerThreads[_LightSwitcherIdx] = ConfigureLightSwitcherWorkerThread();
            _WorkerThreads[_LightSwitcherIdx].Start();

            _StatusThreads[_LightSwitcherIdx] = ConfigureLightSwitcherStatusThread();
            _StatusThreads[_LightSwitcherIdx].Start();
        }

        private void HandleThermometer()
        {
            AdjustThermometerBlock(true);
            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Connected" + '\n');

            _WorkerThreads[_ThermometerIdx] = ConfigureThermometerWorkerThread();
            _WorkerThreads[_ThermometerIdx].Start();

            _StatusThreads[_ThermometerIdx] = ConfigureThermometerStatusThread();
            _StatusThreads[_ThermometerIdx].Start();
        }

        private void HandleMotionDetector()
        {
            AdjustMotionDetectorBlock(true);
            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL + "Connected" + '\n');

            _WorkerThreads[_MotionDetectorIdx] = ConfigureMotionDetectorWorkerThread();
            _WorkerThreads[_MotionDetectorIdx].Start();

            _StatusThreads[_MotionDetectorIdx] = ConfigureMotionDetectorStatusThread();
            _StatusThreads[_MotionDetectorIdx].Start();
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

        private void ProcessLightSwitcherData(string data)
        {
            if (string.IsNullOrEmpty(data) || data.Equals(""))
            {
                return;
            }

            int idx;
            if ((idx = data.IndexOf(NETWORK_LIGHTS_ARG)) >= 0)
            {
                try
                {
                    int startIdx = idx + NETWORK_LIGHTS_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    bool lights = _LightSwitcherStatus = bool.Parse(data.Substring(startIdx, endIdx - startIdx));

                    Dispatcher.Invoke(delegate ()
                    {
                        LightSwitcherStatusValueLabel.Content = lights ? "on" : "off";
                    });
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL +
                        string.Format("Received lights status: {0}", lights ? "on" : "off") + '\n');
                }
                catch (FormatException)
                {
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL +
                        "Received incorrect lights status" + '\n');
                }
            }
            else if ((idx = data.IndexOf(NETWORK_STATUS_ARG)) >= 0)
            {
                try
                {
                    int startIdx = idx + NETWORK_STATUS_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    int status = int.Parse(data.Substring(startIdx, endIdx - startIdx));

                    if (status == DEVICE_STATUS_UP)
                    {
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL + "Device is up." + '\n');
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(delegate ()
                        {
                            CloseLightSwitcherConnection();
                        });

                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL +
                                "Device sent bad status, connection's closed." + '\n');
                        }
                    }
                }
                catch (FormatException)
                {
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL +
                        "Received incorrect device status" + '\n');
                }
            }
            else if ((idx = data.IndexOf(NETWORK_METHOD_TO_INVOKE_ARG)) >= 0)
            {
                int startIdx = idx + NETWORK_METHOD_TO_INVOKE_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                string method = data.Substring(startIdx, endIdx - startIdx);

                if (!string.IsNullOrEmpty(method) && method.Equals(NETWORK_LIGHT_SWITCHER_METHOD_TO_SWITCH))
                {
                    _LightSwitcherStatus = !_LightSwitcherStatus;
                    Dispatcher.Invoke(delegate ()
                    {
                        LightSwitcherStatusValueLabel.Content = _LightSwitcherStatus ? "on" : "off";
                    });

                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL +
                        "Switched." + '\n');
                }
                else if (!string.IsNullOrEmpty(method) && method.Equals(NETWORK_METHOD_TO_DISCONNECT))
                {
                    Dispatcher.Invoke(delegate ()
                    {
                        CloseLightSwitcherConnection();
                    });
                }
            }
            else
            {
                Log(string.Format(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL +
                    "Unknown data received: \"{0}\"" + '\n', data));
            }
        }

        private void ProcessLightSwitcherData(ref List<string> dataSet)
        {
            foreach (string data in dataSet)
            {
                ProcessLightSwitcherData(data);
            }

            dataSet.Clear();
        }

        private void ProcessThermometerData(string data)
        {
            if (string.IsNullOrEmpty(data) || data.Equals(""))
            {
                return;
            }

            int idx;
            if ((idx = data.IndexOf(NETWORK_UPDATE_INTERVAL_ARG)) >= 0)
            {
                try
                {
                    int startIdx = idx + NETWORK_UPDATE_INTERVAL_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    int updateInterval = int.Parse(data.Substring(startIdx, endIdx - startIdx));

                    Dispatcher.Invoke(delegate ()
                    {
                        UpdateIntervalTextBlock.Text = updateInterval.ToString();
                    });
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        string.Format("Received update interval: {0}", updateInterval) + '\n');
                }
                catch (FormatException)
                {
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        "Received incorrect update interval" + '\n');
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
                        string.Format("Received temperature: {0}", temperature.ToString("F2")) + '\n');
                }
                catch (FormatException)
                {
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        "Received incorrect temperature" + '\n');
                }
            }
            else if ((idx = data.IndexOf(NETWORK_STATUS_ARG)) >= 0)
            {
                try
                {
                    int startIdx = idx + NETWORK_STATUS_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    int status = int.Parse(data.Substring(startIdx, endIdx - startIdx));

                    if (status == DEVICE_STATUS_UP)
                    {
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Device is up." + '\n');
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(delegate ()
                        {
                            CloseThermometerConnection();
                        });

                        if (_VerboseLogging)
                        {
                            Log(NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Device sent bad status, connection's closed." + '\n');
                        }
                    }
                }
                catch (FormatException)
                {
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        "Received incorrect device status" + '\n');
                }
            }
            else if ((idx = data.IndexOf(NETWORK_METHOD_TO_INVOKE_ARG)) >= 0)
            {
                int startIdx = idx + NETWORK_METHOD_TO_INVOKE_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                string method = data.Substring(startIdx, endIdx - startIdx);

                if (!string.IsNullOrEmpty(method) && method.Equals(NETWORK_METHOD_TO_DISCONNECT))
                {
                    Dispatcher.Invoke(delegate ()
                    {
                        CloseThermometerConnection();
                    });
                }
            }
            else
            {
                Log(string.Format(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                    "Unknown data received: \"{0}\"" + '\n', data));
            }
        }

        private void ProcessThermometerData(ref List<string> dataSet)
        {
            foreach (string data in dataSet)
            {
                ProcessThermometerData(data);
            }

            dataSet.Clear();
        }

        private void ProcessMotionDetectorData(string data)
        {
            if (string.IsNullOrEmpty(data) || data.Equals(""))
            {
                return;
            }

            int idx;
            if ((idx = data.IndexOf(NETWORK_TIME_ARG)) >= 0)
            {
                try
                {
                    int startIdx = idx + NETWORK_TIME_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    long time = long.Parse(data.Substring(startIdx, endIdx - startIdx));
                    _MotionDetectorTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    _MotionDetectorTime.AddSeconds(time);

                    Dispatcher.Invoke(delegate ()
                    {
                        MotionDetectorLastTimeValueLabel.Content = _MotionDetectorTime.ToString();
                    });
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL +
                        string.Format("Received time: {0}", time) + '\n');
                }
                catch (FormatException)
                {
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL +
                        "Received incorrect time" + '\n');
                }
            }
            else if ((idx = data.IndexOf(NETWORK_STATUS_ARG)) >= 0)
            {
                try
                {
                    int startIdx = idx + NETWORK_STATUS_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    int status = int.Parse(data.Substring(startIdx, endIdx - startIdx));

                    if (status == DEVICE_STATUS_UP)
                    {
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL + "Device is up." + '\n');
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(delegate ()
                        {
                            CloseMotionDetectorConnection();
                        });

                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL +
                                "Device sent bad status, connection's closed." + '\n');
                        }
                    }
                }
                catch (FormatException)
                {
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL +
                        "Received incorrect device status" + '\n');
                }
            }
            else if ((idx = data.IndexOf(NETWORK_METHOD_TO_INVOKE_ARG)) >= 0)
            {
                int startIdx = idx + NETWORK_METHOD_TO_INVOKE_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                string method = data.Substring(startIdx, endIdx - startIdx);

                if (!string.IsNullOrEmpty(method) && method.Equals(NETWORK_METHOD_TO_DISCONNECT))
                {
                    Dispatcher.Invoke(delegate ()
                    {
                        CloseMotionDetectorConnection();
                    });
                }
            }
            else
            {
                Log(string.Format(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL +
                    "Unknown data received: \"{0}\"" + '\n', data));
            }
        }

        private void ProcessMotionDetectorData(ref List<string> dataSet)
        {
            foreach (string data in dataSet)
            {
                ProcessMotionDetectorData(data);
            }

            dataSet.Clear();
        }

        private void SendThermometerUpdateInterval(ref TcpClient socket, double updateInterval)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(string.Format(NETWORK_UPDATE_INTERVAL_ARG + "{0}" + DELIMITER, updateInterval));
            Send(ref socket, ref bytes);

            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Sent update interval" + '\n');
        }

        private void SendMethodToInvoke(ref TcpClient socket, string method)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(NETWORK_METHOD_TO_INVOKE_ARG + method + DELIMITER);
            Send(ref socket, ref bytes);

            if (_VerboseLogging)
            {
                Log(NETWORK_LOG_LABEL + "Method to invoke: " + method + '.' + '\n');
            }
        }

        /// TODO: Implement common method to close connection based on index.
        private void CloseLightSwitcherConnection()
        {
            AdjustLightSwitcherBlock(false);

            if (_WorkerThreads[_LightSwitcherIdx] != null && _WorkerThreads[_LightSwitcherIdx].IsAlive)
            {
                _WorkerThreads[_LightSwitcherIdx].Abort();
            }
            if (_ListenerThreads[_LightSwitcherIdx] != null && _ListenerThreads[_LightSwitcherIdx].IsAlive)
            {
                _ListenerThreads[_LightSwitcherIdx].Abort();
            }
            if (_Sockets[_LightSwitcherIdx] != null)
            {
                _Sockets[_LightSwitcherIdx].Close();
            }

            /// Recreate and run listener for the reason server hasn't been stopped.
            _ListenerThreads[_LightSwitcherIdx] = ConfigureListenerThread();
            _ListenerThreads[_LightSwitcherIdx].Start();

            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL + "Disconnected." + '\n');
        }

        /// TODO: Implement common method to close connection based on index.
        private void CloseThermometerConnection()
        {
            AdjustThermometerBlock(false);

            if (_WorkerThreads[_ThermometerIdx] != null && _WorkerThreads[_ThermometerIdx].IsAlive)
            {
                _WorkerThreads[_ThermometerIdx].Abort();
            }
            if (_ListenerThreads[_ThermometerIdx] != null && _ListenerThreads[_ThermometerIdx].IsAlive)
            {
                _ListenerThreads[_ThermometerIdx].Abort();
            }
            if (_Sockets[_ThermometerIdx] != null)
            {
                _Sockets[_ThermometerIdx].Close();
            }

            /// Recreate and run listener for the reason server hasn't been stopped.
            _ListenerThreads[_ThermometerIdx] = ConfigureListenerThread();
            _ListenerThreads[_ThermometerIdx].Start();

            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Disconnected." + '\n');
        }

        /// TODO: Implement common method to close connection based on index.
        private void CloseMotionDetectorConnection()
        {
            AdjustMotionDetectorBlock(false);

            if (_WorkerThreads[_MotionDetectorIdx] != null && _WorkerThreads[_MotionDetectorIdx].IsAlive)
            {
                _WorkerThreads[_MotionDetectorIdx].Abort();
            }
            if (_ListenerThreads[_MotionDetectorIdx] != null && _ListenerThreads[_MotionDetectorIdx].IsAlive)
            {
                _ListenerThreads[_MotionDetectorIdx].Abort();
            }
            if (_Sockets[_MotionDetectorIdx] != null)
            {
                _Sockets[_MotionDetectorIdx].Close();
            }

            /// Recreate and run listener for the reason server hasn't been stopped.
            _ListenerThreads[_MotionDetectorIdx] = ConfigureListenerThread();
            _ListenerThreads[_MotionDetectorIdx].Start();

            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL + "Disconnected." + '\n');
        }

        private void AdjustLightSwitcherBlock(bool isConnected)
        {
            Dispatcher.Invoke(delegate ()
            {
                LightSwitcherConnectionValueLabel.Content = isConnected ? CONNECTION_UP
                                                                        : CONNECTION_DOWN;
                LightSwitcherSwitchButton.IsEnabled = isConnected;
            });
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

        private void AdjustMotionDetectorBlock(bool isConnected)
        {
            Dispatcher.Invoke(delegate ()
            {
                MotionDetectorConnectionValueLabel.Content = isConnected ? CONNECTION_UP
                                                                         : CONNECTION_DOWN;
            });
        }

        private void SwitchButtonsOnConnectionStatusChanged(bool isConnected)
        {
            Dispatcher.Invoke(delegate ()
            {
                PortTextBox.IsEnabled = !isConnected;

                StartServerButton.IsEnabled = !isConnected;
                StopServerButton.IsEnabled = isConnected;
            });
        }

        private void CloseSocket(ref TcpClient socket)
        {
            for (int idx = 0; idx < MAXIMAL_CLIENTS_NUM_VALUE; ++idx)
            {
                if (_Sockets[idx] == socket)
                {
                    switch (idx)
                    {
                        case _LightSwitcherIdx:
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL + "Light switcher doesn't respond." + '\n');
                            Dispatcher.Invoke(delegate ()
                            {
                                CloseLightSwitcherConnection();
                                LightSwitcherConnectionValueLabel.Content = CONNECTION_ERR;
                            });
                            break;

                        case _ThermometerIdx:
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Thermometer doesn't respond." + '\n');
                            Dispatcher.Invoke(delegate ()
                            {
                                CloseThermometerConnection();
                                ThermometerConnectionValueLabel.Content = CONNECTION_ERR;
                            });
                            break;

                        case _MotionDetectorIdx:
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL + "Motion detector doesn't respond." + '\n');
                            Dispatcher.Invoke(delegate ()
                            {
                                CloseMotionDetectorConnection();
                                MotionDetectorConnectionValueLabel.Content = CONNECTION_ERR;
                            });
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        private void MoveData(ref List<string> from, ref List<string> to)
        {
            _DataMutex.WaitOne();

            foreach (string piece in from)
            {
                to.Add(piece);
            }

            from.Clear();

            _DataMutex.ReleaseMutex();
        }

        private void Log(string info)
        {
            try
            {
                Dispatcher.Invoke(delegate ()
                {
                    LogTextBlock.AppendText(info);
                    if (_ShouldScrollToEnd)
                    {
                        LogTextBlock.ScrollToEnd();
                    }
                });
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}

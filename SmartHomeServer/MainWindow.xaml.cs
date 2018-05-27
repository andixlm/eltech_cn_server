using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
        // Размер буфера для получаемых данных.
        private static readonly int BUFFER_SIZE = 8192;
        // Разделитель между единицами передаваемых данных.
        private static readonly char DELIMITER = ';';
        // Надпись элемента интерфейса.
        private static readonly string IPADDRESS_LOG_LABEL = "IP Address: ";
        // Адрес, замыкающийся на себя.
        private static readonly string LOCALHOST_IPADDRESS = "127.0.0.1";
        // Надпись элемента интерфейса.
        private static readonly string PORT_LOG_LABEL = "Port: ";
        // Минимальное и максимальное значения используемого порта.
        private static readonly int MINIMAL_PORT_VALUE = 1024;
        private static readonly int MAXIMAL_PORT_VALUE = 49151;
        // Надпись элемента интерфейса.
        private static readonly string CONNECTION_LOG_LABEL = "Connection: ";
        // Состояния подключения.
        private static readonly string CONNECTION_UP = "up";
        private static readonly string CONNECTION_WAIT = "wait";
        private static readonly string CONNECTION_DOWN = "down";
        private static readonly string CONNECTION_ERR = "err";
        // Надпись элемента интерфейса.
        private static readonly string UPDATE_INTERVAL_LOG_LABEL = "Update interval: ";
        // Надпись элементам интерфейса.
        private static readonly string NETWORK_LOG_LABEL = "Network: ";
        // Имена различных устройств для метки журнала.
        private static readonly string NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL = "Light Switcher: ";
        private static readonly string NETWORK_DEVICE_THERMOMETER_LOG_LABEL = "Thermometer: ";
        private static readonly string NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL = "Motion Detector: ";
        // Аргумент данных, сообщающий о типе устройства.
        private static readonly string NETWORK_DEVICE_ARG = "Device: ";
        // Типы устройств.
        private static readonly string NETWORK_DEVICE_LIGHT_SWITCHER = "LightSwitcher";
        private static readonly string NETWORK_DEVICE_THERMOMETER = "Thermometer";
        private static readonly string NETWORK_DEVICE_MOTION_DETECTOR = "MotionDetector";
        // Аргумент данных, сообщающий о статусе переключателя света.
        private static readonly string NETWORK_LIGHTS_ARG = "Lights: ";
        // Аргумент данных, сообщающий о температуре.
        private static readonly string NETWORK_TEMPERATURE_ARG = "Temperatute: ";
        // Аргумент данных, сообщающий о периоде обновления температуры.
        private static readonly string NETWORK_UPDATE_INTERVAL_ARG = "Update interval: ";

        // Аргумент данных, сообщающий о времени обнаружения движения.
        private static readonly string NETWORK_TIME_ARG = "Time: ";
        // Аргумент данных, сообщающий о методе, который необходимо выполнить.
        private static readonly string NETWORK_METHOD_TO_INVOKE_ARG = "Method: ";
        // Аргумент данных, сообщающий о состоянии работы устройства.
        private static readonly string NETWORK_STATUS_ARG = "Status: ";
        // Метод для исполнения на переключателе для переключения статуса света.
        private static readonly string NETWORK_LIGHT_SWITCHER_METHOD_TO_SWITCH = "SWITCH";
        // Метод для исполнения на термометре для обновления температуры.
        private static readonly string NETWORK_THERMOMETER_METHOD_TO_UPDATE_TEMP = "UPDATE_TEMP";
        // Метод для исполнения для отключения устройства от сервера.
        private static readonly string NETWORK_METHOD_TO_DISCONNECT = "DISCONNECT";
        // Метод для исполнения для отправки состояния работы устройства.
        private static readonly string NETWORK_METHOD_TO_REQUEST_STATUS = "REQUEST_STATUS";
        // Максимальное количество клиентов и рабочих потоков, обслуживающих их.
        private static readonly int MAXIMAL_CLIENTS_NUM_VALUE = 3;
        private static readonly int MAXIMAL_THREADS_NUM_VALUE = 3;
        // Значение состояния нормально функционирующего устройства.
        private static readonly int DEVICE_STATUS_UP = 42;
        // Период запроса состояния работы устройства.
        private static readonly int DEVICE_STATUS_CHECK_TIMEOUT = 3000;
        // Генератор случайных чисел.
        private static readonly Random sRandom = new Random();
        // 1.1.1970.
        private static readonly DateTime sEpochTime = new DateTime(1970, 1, 1);
        // Подробное логгирование.
        private bool _VerboseLogging;
        // Прокрутка журнала при логгировании.
        private bool _ShouldScrollToEnd;
        // Сервер, принимающий подключения.
        private TcpListener _NetworkListener;
        // Потоки, принимающие подключения.
        private Thread[] _ListenerThreads;
        // Потоки, обслуживающие подключённые устройства.
        private Thread[] _WorkerThreads;
        // Потоки, опрашивающие состояния устройств.
        private Thread[] _StatusThreads;
        // Хранилище сокетов устройств.
        private TcpClient[] _Sockets;
        // Мьютексы для синхронизации работы потоков.
        private Mutex _ListenerMutex;
        private Mutex _DataMutex;
        // Кэши данных, принятых от устройств для отложенной обработки.
        private List<string> _Cache;
        private List<string> _LightSwitcherCache;
        private List<string> _ThermometerCache;
        private List<string> _MotionDetectorCache;
        // Состояния переключателя света (свет включен/выключен).
        private bool _LightSwitcherStatus;
        // Период обновления температуры.
        private int _ThermometerUpdateInterval;
        // Время последнего обнаружения движения.
        private DateTime _MotionDetectorTime;
        // Идентификаторы различных устройств в хранилище.
        private const int _LightSwitcherIdx = 0;
        private const int _ThermometerIdx = 1;
        private const int _MotionDetectorIdx = 2;
        // Порт, на котором запущен сервер.
        private int _Port;

        public MainWindow()
        {
            InitializeComponent();
            // Инициализация и настройка программы.
            Init();
            Configure();
        }
        // Метод, выполняющий инициализацию всех объектов.
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
        // Метод, выполняющий настройку всех объектов.
        private void Configure()
        {
            _LightSwitcherStatus = false;
            _ThermometerUpdateInterval = 1; ;
            _MotionDetectorTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            _VerboseLogging = false;
            // Переключатель уровня журналирования в UI.
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
            // Переключатель необходимости прокрутки журнала в UI.
            ScrollToEndCheckBox.IsChecked = _ShouldScrollToEnd;
            ScrollToEndCheckBox.Checked += (sender, e) =>
            {
                _ShouldScrollToEnd = true;
            };
            ScrollToEndCheckBox.Unchecked += (sender, e) =>
            {
                _ShouldScrollToEnd = false;
            };
            // Метод, вызываемый при закрытии приложения. Выполняется остановка сервера.
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
            // При запуске генерируется случайный порт.
            _Port = sRandom.Next(MINIMAL_PORT_VALUE, MAXIMAL_PORT_VALUE);
            PortTextBox.Text = _Port.ToString();

            // Кнопка запуска сервера.
            StartServerButton.IsEnabled = true;
            StartServerButton.Click += (sender, e) =>
            {
                new Thread(new ThreadStart(delegate ()
                {
                    StartServer();
                })).Start();
            };
            // Кнопка остановки сервера.
            StopServerButton.IsEnabled = false;
            StopServerButton.Click += (sender, e) =>
            {
                new Thread(new ThreadStart(delegate ()
                {
                    StopServer();
                })).Start();
            };
            // Настройка блоков UI различных устройств в зависимости от подключения.
            AdjustLightSwitcherBlock(false);
            AdjustThermometerBlock(false);
            AdjustMotionDetectorBlock(false);
            // Кнопка переключателя света.
            LightSwitcherSwitchButton.Click += (sender, e) =>
            {
                if (_Sockets[_LightSwitcherIdx] != null && _Sockets[_LightSwitcherIdx].Connected)
                {
                    // Отправка запроса на переключение света.
                    SendMethodToInvoke(ref _Sockets[_LightSwitcherIdx], NETWORK_LIGHT_SWITCHER_METHOD_TO_SWITCH);
                }
            };
            // Кнопка установки периода обновления температуры.
            UpdateIntervalSetButton.Click += (sender, e) =>
            {
                try
                {
                    _ThermometerUpdateInterval = int.Parse(UpdateIntervalTextBlock.Text);
                    Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                        UPDATE_INTERVAL_LOG_LABEL + string.Format("Set to {0}\n", _ThermometerUpdateInterval));

                    if (_Sockets[_ThermometerIdx] != null && _Sockets[_ThermometerIdx].Connected)
                    {
                        // Отправка запроса на установку периода обновления термометра.
                        SendThermometerUpdateInterval(ref _Sockets[_ThermometerIdx], _ThermometerUpdateInterval);
                    }
                }
                catch (FormatException exc)
                {
                    Log(UPDATE_INTERVAL_LOG_LABEL + exc.Message + '\n');
                }
            };
            // Кнопка обновления температуры.
            TemperatureUpdateButton.Click += (sender, e) =>
            {
                if (_Sockets[_ThermometerIdx] != null && _Sockets[_ThermometerIdx].Connected)
                {
                    // Отправка запроса на обновление температуры.
                    SendMethodToInvoke(ref _Sockets[_ThermometerIdx], NETWORK_THERMOMETER_METHOD_TO_UPDATE_TEMP);
                }
            };
        }

        // Настройка потока, принимающего подключения.
        private Thread ConfigureListenerThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    // В один момент времени подключения принимает один поток.
                    _ListenerMutex.WaitOne();
                    _NetworkListener.Start();
                    TcpClient socket = _NetworkListener.AcceptTcpClient();
                    _NetworkListener.Stop();
                    _ListenerMutex.ReleaseMutex();
                    // Подключение принято, обработка нового устройства.
                    // Другой поток начинает ожидать новое подключение.
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
        // Настройка всех потоков, принимающих подключения, при помощи метода выше.
        private void ConfigureListenerThreads()
        {
            for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
            {
                _ListenerThreads[idx] = ConfigureListenerThread();
            }
        }
        // Настройка потока, обрабатывающего переключатель света.
        private Thread ConfigureLightSwitcherWorkerThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_LightSwitcherIdx] != null && _Sockets[_LightSwitcherIdx].Connected)
                    {
                        // Перед принятием новых данных, обработать имеющиеся в кэше.
                        ProcessLightSwitcherData(ref _LightSwitcherCache);
                        // Принять данные.
                        byte[] bytes = new byte[BUFFER_SIZE];
                        Receive(ref _Sockets[_LightSwitcherIdx], ref bytes);
                        // Закэшировать данные и обработать первых элемент полученных данных
                        // (если было получено несколько элементов данных сразу).
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
        // Настройка потока, опрашивающего состояние переключателя света.
        private Thread ConfigureLightSwitcherStatusThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_LightSwitcherIdx] != null && _Sockets[_LightSwitcherIdx].Connected)
                    {
                        // Отправка запроса на получение статуса.
                        SendMethodToInvoke(ref _Sockets[_LightSwitcherIdx], NETWORK_METHOD_TO_REQUEST_STATUS);
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL + "Status was requested." + '\n');
                        }
                        // Ожидание перед следующим запросом.
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
        // Настройка потока, обрабатывающего термометр.
        private Thread ConfigureThermometerWorkerThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_ThermometerIdx] != null && _Sockets[_ThermometerIdx].Connected)
                    {
                        // Обработка закэшированных данных.
                        ProcessThermometerData(ref _ThermometerCache);
                        // Получение данных.
                        byte[] bytes = new byte[BUFFER_SIZE];
                        Receive(ref _Sockets[_ThermometerIdx], ref bytes);
                        // Кэширование и обработка данных.
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
        // Настройка потока, опрашивающего состояние термометра.
        private Thread ConfigureThermometerStatusThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_ThermometerIdx] != null && _Sockets[_ThermometerIdx].Connected)
                    {
                        // Отправка запроса на получение состояния.
                        SendMethodToInvoke(ref _Sockets[_ThermometerIdx], NETWORK_METHOD_TO_REQUEST_STATUS);
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Status was requested." + '\n');
                        }
                        // Ожидание перед следующим запросом.
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
        // Настройка потока, обрабатывающего детектор движений.
        private Thread ConfigureMotionDetectorWorkerThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_MotionDetectorIdx] != null && _Sockets[_MotionDetectorIdx].Connected)
                    {
                        // Обработка закэшированных данных.
                        ProcessMotionDetectorData(ref _MotionDetectorCache);
                        // Принятие данных.
                        byte[] bytes = new byte[BUFFER_SIZE];
                        Receive(ref _Sockets[_MotionDetectorIdx], ref bytes);
                        // Кэширование и обработка данных.
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
        // Настройка потока, опрашивающего состояние детектора движений.
        private Thread ConfigureMotionDetectorStatusThread()
        {
            return new Thread(new ThreadStart(delegate ()
            {
                try
                {
                    while (_Sockets[_MotionDetectorIdx] != null && _Sockets[_MotionDetectorIdx].Connected)
                    {
                        // Отправка запроса на получение состояния.
                        SendMethodToInvoke(ref _Sockets[_MotionDetectorIdx], NETWORK_METHOD_TO_REQUEST_STATUS);
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL + "Status was requested." + '\n');
                        }
                        // Ожидание перед следующим запросом.
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
        // Запуск сервера.
        private void StartServer()
        {
            try
            {
                // Настройка порта.
                Dispatcher.Invoke(delegate ()
                {
                    _Port = int.Parse(PortTextBox.Text);
                });
                if (_Port < MINIMAL_PORT_VALUE || _Port > MAXIMAL_PORT_VALUE)
                {
                    throw new Exception(string.Format("Incorrect port value. [{0}; {1}] ports are allowed.",
                        MINIMAL_PORT_VALUE, MAXIMAL_PORT_VALUE));
                }
                // Настройка IP-адреса. Сервер запускается на внутреннем сетевом интерфейсе.
                IPAddress address = null;
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                address = ip.Address;
                            }
                        }
                    }
                }
                // Создание сервера на внутреннем адресе и заданном порте.
                _NetworkListener = new TcpListener(address, _Port);
                // Запуск потоков, принимающих подключения.
                ConfigureListenerThreads();
                for (int idx = 0; idx < MAXIMAL_THREADS_NUM_VALUE; ++idx)
                {
                    _ListenerThreads[idx].Start();
                }
                // Обновление состояния работы сервера.
                Dispatcher.Invoke(delegate ()
                {
                    ServerStatusLabel.Content = CONNECTION_UP;
                });
                SwitchButtonsOnConnectionStatusChanged(true);
                Log(NETWORK_LOG_LABEL + "Server successfully started." + '\n');
            }
            catch (Exception exc)
            {
                // Невозможно запустить сервер.
                Dispatcher.Invoke(delegate ()
                {
                    ServerStatusLabel.Content = CONNECTION_ERR;
                });
                Log(NETWORK_LOG_LABEL + "Unable to start server: " + exc.Message + '\n');
            }
        }
        // Остановка сервера.
        private void StopServer()
        {
            // Прекратить принимать подключения.
            _NetworkListener.Stop();
            // Завершить все работающие потоки.
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
            // Обновить состояние работы сервера.
            Dispatcher.Invoke(delegate ()
            {
                ServerStatusLabel.Content = CONNECTION_DOWN;
            });
            // Настроить UI для остановленного сервера.
            SwitchButtonsOnConnectionStatusChanged(false);
            AdjustLightSwitcherBlock(false);
            AdjustThermometerBlock(false);
            AdjustMotionDetectorBlock(false);

            Log(NETWORK_LOG_LABEL + "Server successfully stopped." + '\n');
        }
        // Отправка данных в сокет.
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
        // Приём данных из сокета.
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
        // Обработка нового устройства.
        private void HandleNewClient(ref TcpClient socket)
        {
            // Принять данные.
            byte[] bytes = new byte[BUFFER_SIZE];
            Receive(ref socket, ref bytes);
            // Получить данные.
            string data = Encoding.Unicode.GetString(bytes);
            if (string.IsNullOrEmpty(data) || data.Equals(""))
            {
                Log(NETWORK_LOG_LABEL + "Empty data received." + '\n');
                return;
            }
            // Закэшировать данные и получить первый элемент.
            string first = CacheData(data, ref _Cache);

            int idx;
            // Получен тип устройства.
            if ((idx = first.IndexOf(NETWORK_DEVICE_ARG)) >= 0)
            {
                int startIdx = idx + NETWORK_DEVICE_ARG.Length, endIdx = first.IndexOf(DELIMITER);
                // Получить тип.
                string device = first.Substring(startIdx, endIdx - startIdx);
                // Переключатель света.
                if (string.Equals(device, NETWORK_DEVICE_LIGHT_SWITCHER))
                {
                    // Сохранить сокет, переместить полученные от него данные в
                    // кэш переключателя света и запустить обработку.
                    _Sockets[_LightSwitcherIdx] = socket;
                    MoveData(ref _Cache, ref _LightSwitcherCache);
                    HandleLightSwitcher();
                }
                // Термометр.
                else if (string.Equals(device, NETWORK_DEVICE_THERMOMETER))
                {
                    // Сохранить сокет, переместить полученные от него данные в
                    // кэш термометра и запустить обработку.
                    _Sockets[_ThermometerIdx] = socket;
                    MoveData(ref _Cache, ref _ThermometerCache);
                    HandleThermometer();
                }
                // Детектор движений.
                else if (string.Equals(device, NETWORK_DEVICE_MOTION_DETECTOR))
                {
                    // Сохранить сокет, переместить полученные от него данные в
                    // кэш детектора движений и запустить обработку.
                    _Sockets[_MotionDetectorIdx] = socket;
                    MoveData(ref _Cache, ref _MotionDetectorCache);
                    HandleMotionDetector();
                }
                else // Неизвестное устройство.
                {
                    Log(NETWORK_LOG_LABEL + "Unknown device tried to connect." + '\n');
                }
            }
            else // Получены неизвестные данные.
            {
                Log(NETWORK_LOG_LABEL + "Unknown data received." + '\n');
            }
        }
        // Обработка подключения переключателя света.
        private void HandleLightSwitcher()
        {
            // Настроить UI.
            AdjustLightSwitcherBlock(true);
            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL + "Connected" + '\n');
            // Запустить поток, обрабатывающий данные.
            _WorkerThreads[_LightSwitcherIdx] = ConfigureLightSwitcherWorkerThread();
            _WorkerThreads[_LightSwitcherIdx].Start();
            // Запустить поток, опрашивающий статус.
            _StatusThreads[_LightSwitcherIdx] = ConfigureLightSwitcherStatusThread();
            _StatusThreads[_LightSwitcherIdx].Start();
        }
        // Обработка подключения термометра.
        private void HandleThermometer()
        {
            // Настроить UI.
            AdjustThermometerBlock(true);
            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Connected" + '\n');
            // Запустить поток, обрабатывающий данные.
            _WorkerThreads[_ThermometerIdx] = ConfigureThermometerWorkerThread();
            _WorkerThreads[_ThermometerIdx].Start();
            // Запустить поток, опрашивающий статус.
            _StatusThreads[_ThermometerIdx] = ConfigureThermometerStatusThread();
            _StatusThreads[_ThermometerIdx].Start();
        }
        // Обработка подключения детектора движений.
        private void HandleMotionDetector()
        {
            // Настроить UI.
            AdjustMotionDetectorBlock(true);
            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL + "Connected" + '\n');
            // Запустить поток, обрабатывающий данные.
            _WorkerThreads[_MotionDetectorIdx] = ConfigureMotionDetectorWorkerThread();
            _WorkerThreads[_MotionDetectorIdx].Start();
            // Запустить поток, опрашивающий статус.
            _StatusThreads[_MotionDetectorIdx] = ConfigureMotionDetectorStatusThread();
            _StatusThreads[_MotionDetectorIdx].Start();
        }
        // Кэширование данные и возврат первого элемента.
        string CacheData(string data, ref List<string> cache)
        {
            int delimiterIdx = data.IndexOf(DELIMITER);
            string first = data.Substring(0, delimiterIdx + 1);
            // Обработка строки вида: “arg:val;arg:val;arg:val;...”.
            // Отбросить первый элемент.
            data = data.Substring(delimiterIdx + 1, data.Length - delimiterIdx - 1);
            // Проход по элементам.
            for (delimiterIdx = data.IndexOf(DELIMITER); delimiterIdx >= 0; delimiterIdx = data.IndexOf(DELIMITER))
            {
                // Кэширование текущего элемента.
                cache.Add(data.Substring(0, delimiterIdx + 1));
                // Отбросить текущий элемент.
                data = data.Substring(delimiterIdx + 1, data.Length - delimiterIdx - 1);
            }

            return first;
        }

        // Обработка данных переключателя света.
        private void ProcessLightSwitcherData(string data)
        {
            if (string.IsNullOrEmpty(data) || data.Equals(""))
            {
                return;
            }

            int idx;
            // Состояние света.
            if ((idx = data.IndexOf(NETWORK_LIGHTS_ARG)) >= 0)
            {
                try
                {
                    // Обработка полученного состояния.
                    int startIdx = idx + NETWORK_LIGHTS_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    bool lights = _LightSwitcherStatus = bool.Parse(data.Substring(startIdx, endIdx - startIdx));
                    // Обновление полученным состоянием.
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
            // Состояние устройства.
            else if ((idx = data.IndexOf(NETWORK_STATUS_ARG)) >= 0)
            {
                try
                {
                    // Обработка полученного состояния.
                    int startIdx = idx + NETWORK_STATUS_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    int status = int.Parse(data.Substring(startIdx, endIdx - startIdx));

                    if (status == DEVICE_STATUS_UP) // Всё хорошо.
                    {
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL + "Device is up." + '\n');
                        }
                    }
                    else // Некорректное состояние.
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
            // Метод для исполнения.
            else if ((idx = data.IndexOf(NETWORK_METHOD_TO_INVOKE_ARG)) >= 0)
            {
                int startIdx = idx + NETWORK_METHOD_TO_INVOKE_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                // Получение метода.
                string method = data.Substring(startIdx, endIdx - startIdx);
                // Переключить состояние света.
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
                // Отключить устройство.
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
        // Обработка списка данных от переключателя света.
        private void ProcessLightSwitcherData(ref List<string> dataSet)
        {
            foreach (string data in dataSet)
            {
                ProcessLightSwitcherData(data);
            }

            dataSet.Clear();
        }
        // Обработка данных термометра.
        private void ProcessThermometerData(string data)
        {
            if (string.IsNullOrEmpty(data) || data.Equals(""))
            {
                return;
            }

            int idx;
            // Интервал обновления температуры.
            if ((idx = data.IndexOf(NETWORK_UPDATE_INTERVAL_ARG)) >= 0)
            {
                try
                {
                    // Получить интервал.
                    int startIdx = idx + NETWORK_UPDATE_INTERVAL_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    int updateInterval = int.Parse(data.Substring(startIdx, endIdx - startIdx));
                    // Обновить интервал.
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
            // Температура.
            else if ((idx = data.IndexOf(NETWORK_TEMPERATURE_ARG)) >= 0)
            {
                try
                {
                    // Получить температуру.
                    int startIdx = idx + NETWORK_TEMPERATURE_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    double temperature = double.Parse(data.Substring(startIdx, endIdx - startIdx));
                    // Обновить температуру.
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
            // Состояние устройства.
            else if ((idx = data.IndexOf(NETWORK_STATUS_ARG)) >= 0)
            {
                try
                {
                    int startIdx = idx + NETWORK_STATUS_ARG.Length, endIdx = data.IndexOf(DELIMITER); // Получить состояние.
                    int status = int.Parse(data.Substring(startIdx, endIdx - startIdx));

                    if (status == DEVICE_STATUS_UP) // Всё хорошо.
                    {
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Device is up." + '\n');
                        }
                    }
                    else // Некорректный статус.
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
            // Метод для исполнения.
            else if ((idx = data.IndexOf(NETWORK_METHOD_TO_INVOKE_ARG)) >= 0)
            {
                // Получить метод.
                int startIdx = idx + NETWORK_METHOD_TO_INVOKE_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                string method = data.Substring(startIdx, endIdx - startIdx);
                // Отключение устройства.
                if (!string.IsNullOrEmpty(method) && method.Equals(NETWORK_METHOD_TO_DISCONNECT))
                {
                    Dispatcher.Invoke(delegate ()
                    {
                        CloseThermometerConnection();
                    });
                }
            }
            else // Неизвестные данные.
            {
                Log(string.Format(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL +
                    "Unknown data received: \"{0}\"" + '\n', data));
            }
        }
        // Обработка списка данных от термометра.
        private void ProcessThermometerData(ref List<string> dataSet)
        {
            foreach (string data in dataSet)
            {
                ProcessThermometerData(data);
            }

            dataSet.Clear();
        }
        // Обработка данных от детектора движений.
        private void ProcessMotionDetectorData(string data)
        {
            if (string.IsNullOrEmpty(data) || data.Equals(""))
            {
                return;
            }

            int idx;
            // Время.
            if ((idx = data.IndexOf(NETWORK_TIME_ARG)) >= 0)
            {
                try
                {
                    // Получение времени относительно epoch-time.
                    int startIdx = idx + NETWORK_TIME_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    long time = long.Parse(data.Substring(startIdx, endIdx - startIdx));
                    _MotionDetectorTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    _MotionDetectorTime = _MotionDetectorTime.AddSeconds(time);
                    // Обновление времени.
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
            // Состояние устройства.
            else if ((idx = data.IndexOf(NETWORK_STATUS_ARG)) >= 0)
            {
                try
                {
                    // Получение статуса.
                    int startIdx = idx + NETWORK_STATUS_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                    int status = int.Parse(data.Substring(startIdx, endIdx - startIdx));
                    // Всё хорошо.
                    if (status == DEVICE_STATUS_UP)
                    {
                        if (_VerboseLogging)
                        {
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_MOTION_DETECTOR_LOG_LABEL + "Device is up." + '\n');
                        }
                    }
                    else // Некорректный статус.
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

            // Метод для исполнения.
            else if ((idx = data.IndexOf(NETWORK_METHOD_TO_INVOKE_ARG)) >= 0)
            {
                // Получение метода.
                int startIdx = idx + NETWORK_METHOD_TO_INVOKE_ARG.Length, endIdx = data.IndexOf(DELIMITER);
                string method = data.Substring(startIdx, endIdx - startIdx);
                // Отключение устройства.
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
        // Обработка списка данных от детектора движений.
        private void ProcessMotionDetectorData(ref List<string> dataSet)
        {
            foreach (string data in dataSet)
            {
                ProcessMotionDetectorData(data);
            }

            dataSet.Clear();
        }
        // Отправить период обновления термометру.
        private void SendThermometerUpdateInterval(ref TcpClient socket, double updateInterval)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(string.Format(NETWORK_UPDATE_INTERVAL_ARG + "{0}" + DELIMITER, updateInterval));
            Send(ref socket, ref bytes);

            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Sent update interval" + '\n');
        }
        // Отправить метод для исполнения.
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
        // Закрыть подключение к переключателю света.
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
        // Закрыть подключение к термометру.
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
        // Закрыть подключение к детектору движений.
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
        // Настройка UI-блока переключателя света.
        private void AdjustLightSwitcherBlock(bool isConnected)
        {
            Dispatcher.Invoke(delegate ()
            {
                LightSwitcherConnectionValueLabel.Content = isConnected ? CONNECTION_UP
                                                                        : CONNECTION_DOWN;
                LightSwitcherSwitchButton.IsEnabled = isConnected;
            });
        }
        // Настройка UI-блока термометра.
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
        // Настройка UI-блока детектора движений.
        private void AdjustMotionDetectorBlock(bool isConnected)
        {
            Dispatcher.Invoke(delegate ()
            {
                MotionDetectorConnectionValueLabel.Content = isConnected ? CONNECTION_UP
                                                                         : CONNECTION_DOWN;
            });
        }
        // Обновить доступность кнопок при изменение состояния запущенности сервера.
        private void SwitchButtonsOnConnectionStatusChanged(bool isConnected)
        {
            Dispatcher.Invoke(delegate ()
            {
                PortTextBox.IsEnabled = !isConnected;

                StartServerButton.IsEnabled = !isConnected;
                StopServerButton.IsEnabled = isConnected;
            });
        }

        // Закрыть сокет.
        private void CloseSocket(ref TcpClient socket)
        {
            for (int idx = 0; idx < MAXIMAL_CLIENTS_NUM_VALUE; ++idx)
            {
                if (_Sockets[idx] == socket)
                {
                    switch (idx)
                    {
                        // Переключатель света.
                        case _LightSwitcherIdx:
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_LIGHT_SWITCHER_LOG_LABEL + "Light switcher doesn't respond." + '\n');
                            Dispatcher.Invoke(delegate ()
                            {
                                CloseLightSwitcherConnection();
                                LightSwitcherConnectionValueLabel.Content = CONNECTION_ERR;
                            });
                            break;
                        // Термометр.
                        case _ThermometerIdx:
                            Log(NETWORK_LOG_LABEL + NETWORK_DEVICE_THERMOMETER_LOG_LABEL + "Thermometer doesn't respond." + '\n');
                            Dispatcher.Invoke(delegate ()
                            {
                                CloseThermometerConnection();
                                ThermometerConnectionValueLabel.Content = CONNECTION_ERR;
                            });
                            break;
                        // Детектор движений.
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
        // Переместить данные из одного списка в другой.
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

        // Вывести сообщение в журнал.
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

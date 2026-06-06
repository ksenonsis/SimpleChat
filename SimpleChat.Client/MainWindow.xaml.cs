using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

// =========================================================
//  SimpleChat — Клиент
//  Весь код — прямо здесь, в code-behind. Никаких паттернов!
// =========================================================

namespace SimpleChat.Client
{
    public partial class MainWindow : Window
    {
        // Объекты для работы с сетью
        private TcpClient   _client;
        private StreamReader _reader;
        private StreamWriter _writer;

        // Флаг: подключены ли мы прямо сейчас
        private bool _isConnected = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        // ─────────────────────────────────────────────────
        //  ПОДКЛЮЧЕНИЕ К СЕРВЕРУ
        // ─────────────────────────────────────────────────
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что имя введено
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Введите ваше имя!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string host = txtHost.Text;
                int    port = int.Parse(txtPort.Text);

                // Подключаемся
                _client = new TcpClient();
                _client.Connect(host, port);

                // Создаём reader и writer для удобной работы со строками
                NetworkStream stream = _client.GetStream();
                _reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                _writer = new StreamWriter(stream, System.Text.Encoding.UTF8);

                _isConnected = true;

                // Первым делом отправляем своё имя серверу
                _writer.WriteLine(txtName.Text);
                _writer.Flush();

                // Запускаем фоновый поток для получения сообщений
                Thread receiveThread = new Thread(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                // Обновляем интерфейс
                SetConnectedState(true);
                AddMessage($">>> Вы вошли как {txtName.Text}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось подключиться:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────
        //  ОТКЛЮЧЕНИЕ
        // ─────────────────────────────────────────────────
        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (!_isConnected) return;

            _isConnected = false;

            try { _client.Close(); } catch { }

            // Обновляем интерфейс из любого потока
            Dispatcher.Invoke(() =>
            {
                SetConnectedState(false);
                lstUsers.Items.Clear();
                AddMessage(">>> Вы отключились от сервера.");
            });
        }

        // ─────────────────────────────────────────────────
        //  ПОЛУЧЕНИЕ СООБЩЕНИЙ (работает в фоновом потоке)
        // ─────────────────────────────────────────────────
        private void ReceiveMessages()
        {
            try
            {
                while (_isConnected)
                {
                    string message = _reader.ReadLine();

                    if (message == null) break; // сервер закрыл соединение

                    // Если сообщение начинается с "USERS:" — это список пользователей
                    if (message.StartsWith("USERS:"))
                    {
                        UpdateUserList(message);
                    }
                    else
                    {
                        // Обычное сообщение — показываем в чате
                        // Dispatcher.Invoke нужен, чтобы обновить UI из другого потока
                        Dispatcher.Invoke(() => AddMessage(message));
                    }
                }
            }
            catch (Exception)
            {
                // Соединение разорвано
            }
            finally
            {
                Disconnect();
            }
        }

        // ─────────────────────────────────────────────────
        //  ОТПРАВКА СООБЩЕНИЯ
        // ─────────────────────────────────────────────────
        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        // Enter в поле ввода тоже отправляет сообщение
        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendMessage();
            }
        }

        private void SendMessage()
        {
            string text = txtMessage.Text.Trim();

            if (string.IsNullOrEmpty(text)) return;
            if (!_isConnected) return;

            try
            {
                _writer.WriteLine(text);
                _writer.Flush();

                // Показываем своё сообщение у себя
                AddMessage($"Вы: {text}");

                // Очищаем поле ввода
                txtMessage.Text = "";
                txtMessage.Focus();
            }
            catch (Exception ex)
            {
                AddMessage("Ошибка отправки: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────
        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ─────────────────────────────────────────────────

        // Добавить строку в список сообщений
        private void AddMessage(string message)
        {
            lstMessages.Items.Add(message);

            // Прокручиваем вниз
            lstMessages.ScrollIntoView(lstMessages.Items[lstMessages.Items.Count - 1]);
        }

        // Обновить список пользователей из сообщения типа "USERS:Аня,Боря,Вася"
        private void UpdateUserList(string message)
        {
            // Убираем префикс "USERS:"
            string names = message.Substring("USERS:".Length);

            // Обновляем список — нужен Dispatcher, так как мы в другом потоке
            Dispatcher.Invoke(() =>
            {
                lstUsers.Items.Clear();

                if (!string.IsNullOrEmpty(names))
                {
                    string[] nameArray = names.Split(',');
                    foreach (string name in nameArray)
                    {
                        lstUsers.Items.Add(name);
                    }
                }
            });
        }

        // Переключить состояние кнопок и полей
        private void SetConnectedState(bool connected)
        {
            btnConnect.IsEnabled    = !connected;
            btnDisconnect.IsEnabled = connected;
            btnSend.IsEnabled       = connected;
            txtMessage.IsEnabled    = connected;
            txtHost.IsEnabled       = !connected;
            txtPort.IsEnabled       = !connected;
            txtName.IsEnabled       = !connected;

            if (connected)
            {
                txtMessage.Focus();
            }
        }

        // Закрытие окна — отключаемся
        protected override void OnClosed(EventArgs e)
        {
            Disconnect();
            base.OnClosed(e);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

// =========================================================
//  SimpleChat — Сервер
//  Запускается первым. Порт: 5000
// =========================================================

namespace SimpleChat.Server
{
    class Program
    {
        // Список подключённых клиентов
        static List<ClientInfo> clients = new List<ClientInfo>();

        // Объект для блокировки списка (чтобы потоки не мешали друг другу)
        static object locker = new object();

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "SimpleChat — Сервер";

            TcpListener server = new TcpListener(IPAddress.Any, 5000);
            server.Start();

            Console.WriteLine("=== Сервер запущен на порту 5000 ===");
            Console.WriteLine("Ожидаю подключений...\n");

            // Бесконечно ждём новых клиентов
            while (true)
            {
                TcpClient tcpClient = server.AcceptTcpClient();
                Console.WriteLine("Новое подключение: " + tcpClient.Client.RemoteEndPoint);

                // Каждый клиент обрабатывается в отдельном потоке
                ClientInfo client = new ClientInfo(tcpClient);
                Thread thread = new Thread(HandleClient);
                thread.IsBackground = true;
                thread.Start(client);
            }
        }

        // Метод для обработки одного клиента
        static void HandleClient(object obj)
        {
            ClientInfo client = (ClientInfo)obj;

            try
            {
                // Первое сообщение от клиента — это его имя
                client.Name = client.Reader.ReadLine();

                // Добавляем в общий список
                lock (locker)
                {
                    clients.Add(client);
                }

                Console.WriteLine($"+ {client.Name} вошёл в чат.");

                // Сообщаем всем, что пришёл новый участник
                BroadcastMessage($">>> {client.Name} вошёл в чат.", client);
                
                // Отправляем обновлённый список пользователей всем
                SendUserList();

                // Читаем сообщения от этого клиента
                while (true)
                {
                    string message = client.Reader.ReadLine();

                    if (message == null) break; // клиент отключился

                    Console.WriteLine($"[{client.Name}]: {message}");

                    // Рассылаем сообщение всем остальным
                    BroadcastMessage($"{client.Name}: {message}", client);
                }
            }
            catch (Exception)
            {
                // Клиент отключился с ошибкой — это нормально
            }
            finally
            {
                // Убираем клиента из списка
                lock (locker)
                {
                    clients.Remove(client);
                }

                Console.WriteLine($"- {client.Name} вышел из чата.");
                BroadcastMessage($">>> {client.Name} вышел из чата.", client);
                SendUserList();

                client.TcpClient.Close();
            }
        }

        // Отправить сообщение всем клиентам, кроме отправителя
        static void BroadcastMessage(string message, ClientInfo sender)
        {
            lock (locker)
            {
                foreach (ClientInfo c in clients)
                {
                    if (c != sender)
                    {
                        try
                        {
                            c.Writer.WriteLine(message);
                            c.Writer.Flush();
                        }
                        catch { }
                    }
                }
            }
        }

        // Отправить список пользователей всем
        // Формат: "USERS:Аня,Боря,Вася"
        static void SendUserList()
        {
            string names = "";

            lock (locker)
            {
                foreach (ClientInfo c in clients)
                {
                    if (names != "") names += ",";
                    names += c.Name;
                }

                string message = "USERS:" + names;

                foreach (ClientInfo c in clients)
                {
                    try
                    {
                        c.Writer.WriteLine(message);
                        c.Writer.Flush();
                    }
                    catch { }
                }
            }
        }
    }

    // =========================================================
    //  Класс для хранения информации об одном клиенте
    // =========================================================
    class ClientInfo
    {
        public TcpClient TcpClient { get; }
        public StreamReader Reader  { get; }
        public StreamWriter Writer  { get; }
        public string Name          { get; set; } = "Неизвестный";

        public ClientInfo(TcpClient tcpClient)
        {
            TcpClient = tcpClient;

            NetworkStream stream = tcpClient.GetStream();
            Reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            Writer = new StreamWriter(stream, System.Text.Encoding.UTF8);
        }
    }
}

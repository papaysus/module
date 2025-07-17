using System.Text;
using RabbitMQ.Client;
using NLog;
using NLog.Config;
using NLog.Targets;
class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    static void Main()
    {
        ConfigureLogging();
        Logger.Info("FeedHandler запущен");

        var factory = new ConnectionFactory()
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest"
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare("quantity", true, false, false);

        var folderPath = @"C:\production_practice\module\json";


        foreach (var file in Directory.GetFiles(folderPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var body = Encoding.UTF8.GetBytes(json);
                channel.BasicPublish("", "quantity", null, body);
                Logger.Info($"Отправлен существующий файл: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Ошибка при обработке существующего файла: {file}");
            }
        }

        var watcher = new FileSystemWatcher(folderPath, "*.json");
        watcher.Created += (s, e) =>
        {
            try
            {
                System.Threading.Thread.Sleep(100);

                var json = File.ReadAllText(e.FullPath);
                var body = Encoding.UTF8.GetBytes(json);
                channel.BasicPublish("", "quantity", null, body);
                Logger.Info($"Отправлен новый файл: {e.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Ошибка при обработке нового файла: {e.Name}");
            }
        };
        watcher.EnableRaisingEvents = true;

        Logger.Info($"Готов к обработке в папке: {folderPath}");
        Console.ReadLine();
    }

    private static void ConfigureLogging()
    {
        var config = new LoggingConfiguration();

        var logConsole = new ConsoleTarget("logconsole")
        {
            Layout = @"[${longdate}] ${level:uppercase=true} - ${message} ${exception}"
        };

        config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);
        LogManager.Configuration = config;
    }
}

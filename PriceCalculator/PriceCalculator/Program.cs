using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NLog;
using Newtonsoft.Json;
using Shared;

class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    static void Main()
    {
        ConfigureLogging();

        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare("price", true, false, false);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            try
            {
                if (ea.Body.IsEmpty)
                {
                    Logger.Info("Получено ПУСТОЕ СООБЩЕНИЕ (IsEmpty=true)");
                    Console.WriteLine("Получено ПУСТОЕ СООБЩЕНИЕ (IsEmpty=true)");
                    channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Info("Получено ПУСТОЕ СООБЩЕНИЕ (пустая строка после декодирования)");
                    Console.WriteLine("Получено ПУСТОЕ СООБЩЕНИЕ (пустая строка после декодирования)");
                    channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var msgs = JsonConvert.DeserializeObject<QuantityMessage[]>(json);

                if (msgs == null || !msgs.Any())
                {
                    Logger.Info("Получено пустое сообщение в очереди price (пустой JSON-массив)");
                    Console.WriteLine("Получено пустое сообщение в очереди price (пустой JSON-массив)");
                    channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var prices = msgs.Select(m =>
                {
                    try
                    {
                        decimal whPrice = m.Type == "product"
                            ? (m.Warehouses.Any() ? m.Warehouses.Average(w => w.Price) : 0)
                            : m.Type == "set"
                                ? m.SubProducts.Sum(sp => sp.Warehouses.Any() ? sp.Warehouses.Average(w => w.Price) : 0)
                                : m.SubProducts.Any()
                                    ? m.SubProducts.Min(sp => sp.Warehouses.Any() ? sp.Warehouses.Average(w => w.Price) : decimal.MaxValue)
                                    : 0;

                        decimal supPrice = m.Type == "product"
                            ? (m.Suppliers.Any() ? m.Suppliers.Min(s => s.Price) : 0)
                            : 0;

                        decimal minPrice = new[] { whPrice, supPrice }
                            .Where(x => x > 0)
                            .DefaultIfEmpty(0)
                            .Min();

                        return new PriceMessage
                        {
                            Id = m.Id,
                            Type = m.Type,
                            WarehouseQuantity = m.WarehouseQuantity,
                            SupplierQuantity = m.SupplierQuantity,
                            Quantity = m.Quantity,
                            WarehousePrice = whPrice,
                            SupplierPrice = supPrice,
                            MinPrice = minPrice,
                            Suppliers = m.Suppliers,
                            SubProducts = m.SubProducts,
                            Warehouses = m.Warehouses
                        };
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Ошибка при обработке товара с Id: {m.Id}");
                        return null;
                    }
                })
                .Where(p => p != null)
                .ToArray();

                var folderPath = @"C:\production_practice\module\results";
                Directory.CreateDirectory(folderPath);
                var filename = $"{folderPath}\\prices_{DateTime.Now:yyyyMMddHHmmss}.json";
                File.WriteAllText(filename, JsonConvert.SerializeObject(prices, Newtonsoft.Json.Formatting.Indented));

                channel.BasicAck(ea.DeliveryTag, false);
                Logger.Info($"Цены сохранены в {filename}");
                Console.WriteLine($"Цены сохранены в {filename}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ошибка при обработке сообщения");
                Console.WriteLine($"Ошибка при обработке сообщения: {ex.Message}");
            }
        };

        channel.BasicConsume("price", false, consumer);
        Logger.Info("Ожидание сообщений в очереди price...");
        Console.WriteLine("Ожидание сообщений в очереди price...");
        Console.ReadLine();
    }

    private static void ConfigureLogging()
    {
        var config = new NLog.Config.LoggingConfiguration();
        var logConsole = new NLog.Targets.ConsoleTarget("logconsole")
        {
            Layout = @"[${longdate}] ${level:uppercase=true} - ${message}"
        };
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);
        LogManager.Configuration = config;
    }
}

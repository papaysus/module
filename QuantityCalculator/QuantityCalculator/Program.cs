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
        using var chIn = connection.CreateModel();
        using var chOut = connection.CreateModel();

        chIn.QueueDeclare("quantity", true, false, false);
        chOut.QueueDeclare("price", true, false, false);

        var consumer = new EventingBasicConsumer(chIn);
        consumer.Received += (model, ea) =>
        {
            try
            {
                var products = JsonConvert.DeserializeObject<Product[]>(Encoding.UTF8.GetString(ea.Body.ToArray()));

                if (products == null)
                {
                    Logger.Warn("Получено пустое сообщение продуктов");
                    chIn.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var messages = products.Select(p =>
                {
                    int whQty = p.Type == "product"
                        ? p.Warehouses.Sum(w => w.Quantity)
                        : p.SubProducts.Min(sp => sp.Warehouses.Sum(w => w.Quantity));

                    int supQty = p.Type == "product"
                        ? p.Suppliers.Sum(s => s.Quantity)
                        : 0;

                    return new QuantityMessage
                    {
                        Id = p.Id,
                        Type = p.Type,
                        WarehouseQuantity = whQty,
                        SupplierQuantity = supQty,
                        Quantity = whQty + supQty,
                        Suppliers = p.Suppliers,
                        SubProducts = p.SubProducts,
                        Warehouses = p.Warehouses
                    };
                }).ToArray();

                var outJson = JsonConvert.SerializeObject(messages);
                chOut.BasicPublish("", "price", null, Encoding.UTF8.GetBytes(outJson));
                chIn.BasicAck(ea.DeliveryTag, false);
                Logger.Info("Количество рассчитано и отправлено в очередь price");
                Console.WriteLine("Количество рассчитано и отправлено в очередь price");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ошибка при расчете количества");
                Console.WriteLine($"Ошибка при расчете количества: {ex.Message}");
            }
        };

        chIn.BasicConsume("quantity", false, consumer);
        Logger.Info("Ожидание сообщений в очереди quantity...");
        Console.WriteLine("Ожидание сообщений в очереди quantity...");
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

using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace OnlineShop.ApiService;

public sealed class LocationUpdater(
    IHubContext<LocationHub> locationHub,
    IConnection rabbitConnection) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel =
            await rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);

        const string queueName = "orders.created";

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.ToArray());
                var message = JsonSerializer.Deserialize<OrderCreatedMessage>(json);

                if (message is null)
                {
                    await channel.BasicNackAsync(
                        args.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                await SetInitialDeliveryLocation(
                    message.OrderId,
                    stoppingToken);

                await channel.BasicAckAsync(
                    args.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch
            {
                await channel.BasicNackAsync(
                    args.DeliveryTag,
                    false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task SetInitialDeliveryLocation(
        int orderId,
        CancellationToken cancellationToken)
    {
        await Task.Delay(3000, cancellationToken);
        await UpdateLocation(orderId, 51.5074, -0.1276, cancellationToken);
    }

    private async Task UpdateLocation(
        int orderId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        await locationHub.Clients.All.SendAsync(
            "ReceiveLocationUpdate",
            latitude,
            longitude,
            cancellationToken);
    }

    private sealed record OrderCreatedMessage(int OrderId);
}
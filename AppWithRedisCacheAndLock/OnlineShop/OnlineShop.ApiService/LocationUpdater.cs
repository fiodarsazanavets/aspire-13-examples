using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace OnlineShop.ApiService;

public sealed class LocationUpdater(
    IHubContext<LocationHub> locationHub,
    QueueServiceClient queueServiceClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueClient = queueServiceClient
            .GetQueueClient("orders-created");
        await queueClient.CreateIfNotExistsAsync();

        while (!stoppingToken.IsCancellationRequested)
        {

            QueueMessage[] messages = queueClient
            .ReceiveMessages(maxMessages: 10);

            foreach (var message in messages)
            {
                var body =
                    JsonSerializer.Deserialize<OrderCreatedMessage>(message.MessageText);

                await SetInitialDeliveryLocation(
                        body.OrderId,
                        stoppingToken);
            }
        }
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
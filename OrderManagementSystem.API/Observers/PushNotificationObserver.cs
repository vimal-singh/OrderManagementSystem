namespace OrderManagementSystem.API.Observers
{
    /// <summary>
    /// Observer that sends push notifications when an order is created.
    /// Currently simulated via structured logging — replace the body with
    /// a real Firebase Cloud Messaging / APNs call for production use.
    /// </summary>
    public class PushNotificationObserver : IOrderObserver
    {
        private readonly ILogger<PushNotificationObserver> _logger;

        public PushNotificationObserver(ILogger<PushNotificationObserver> logger)
        {
            _logger = logger;
        }

        public Task OnOrderCreatedAsync(OrderCreatedEvent orderEvent)
        {
            _logger.LogInformation(
                "[Push] 🔔 Notification sent for Order #{OrderId}. " +
                "Customer={CustomerName} (ID: {CustomerId}), " +
                "Total={TotalAmount:C}, Items={ItemCount}.",
                orderEvent.OrderId,
                orderEvent.CustomerName,
                orderEvent.CustomerId,
                orderEvent.TotalAmount,
                orderEvent.ItemCount);

            // TODO: Replace with actual push notification service, e.g.:
            // await _pushService.SendAsync(new PushMessage
            // {
            //     UserId = orderEvent.CustomerId.ToString(),
            //     Title = "Order Placed!",
            //     Body = $"Order #{orderEvent.OrderId} for {orderEvent.TotalAmount:C} is confirmed."
            // });

            return Task.CompletedTask;
        }
    }
}

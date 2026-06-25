namespace OrderManagementSystem.API.Observers
{
    /// <summary>
    /// Observer that sends SMS notifications when an order is created.
    /// Currently simulated via structured logging — replace the body with
    /// a real Twilio/SNS call for production use.
    /// </summary>
    public class SmsNotificationObserver : IOrderObserver
    {
        private readonly ILogger<SmsNotificationObserver> _logger;

        public SmsNotificationObserver(ILogger<SmsNotificationObserver> logger)
        {
            _logger = logger;
        }

        public Task OnOrderCreatedAsync(OrderCreatedEvent orderEvent)
        {
            _logger.LogInformation(
                "[SMS] 📱 Notification sent for Order #{OrderId}. " +
                "Customer={CustomerName} (ID: {CustomerId}), " +
                "Total={TotalAmount:C}, Items={ItemCount}.",
                orderEvent.OrderId,
                orderEvent.CustomerName,
                orderEvent.CustomerId,
                orderEvent.TotalAmount,
                orderEvent.ItemCount);

            // TODO: Replace with actual SMS service integration, e.g.:
            // await _smsService.SendAsync(new SmsMessage
            // {
            //     To = customerPhone,
            //     Body = $"Your order #{orderEvent.OrderId} of {orderEvent.TotalAmount:C} has been placed!"
            // });

            return Task.CompletedTask;
        }
    }
}

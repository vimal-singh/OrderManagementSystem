namespace OrderManagementSystem.API.Observers
{
    /// <summary>
    /// Observer that sends email notifications when an order is created.
    /// Currently simulated via structured logging — replace the body with
    /// a real SMTP/SendGrid/SES call for production use.
    /// </summary>
    public class EmailNotificationObserver : IOrderObserver
    {
        private readonly ILogger<EmailNotificationObserver> _logger;

        public EmailNotificationObserver(ILogger<EmailNotificationObserver> logger)
        {
            _logger = logger;
        }

        public Task OnOrderCreatedAsync(OrderCreatedEvent orderEvent)
        {
            // Simulate email dispatch latency.
            _logger.LogInformation(
                "[Email] 📧 Notification sent for Order #{OrderId}. " +
                "Customer={CustomerName} (ID: {CustomerId}), " +
                "Total={TotalAmount:C}, Items={ItemCount}, " +
                "OrderDate={OrderDate:u}.",
                orderEvent.OrderId,
                orderEvent.CustomerName,
                orderEvent.CustomerId,
                orderEvent.TotalAmount,
                orderEvent.ItemCount,
                orderEvent.OrderDate);

            // TODO: Replace with actual email service integration, e.g.:
            // await _emailService.SendAsync(new EmailMessage
            // {
            //     To = customerEmail,
            //     Subject = $"Order #{orderEvent.OrderId} Confirmation",
            //     Body = BuildEmailBody(orderEvent)
            // });

            return Task.CompletedTask;
        }
    }
}

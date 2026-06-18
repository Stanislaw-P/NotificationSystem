using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Worker.Data
{
    public class NotificationLog
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        // Success / Failed
        public string Status { get; set; } = string.Empty;

        // Текст ошибки если Status = Failed
        public string? ErrorMessage { get; set; }

        public DateTime ProcessedAt { get; set; }
    }
}

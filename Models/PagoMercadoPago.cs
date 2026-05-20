using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vinto.Api.Models
{
    public class PagoMercadoPago
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PedidoId { get; set; }

        [ForeignKey("PedidoId")]
        public Pedido? Pedido { get; set; }

        [Required, MaxLength(100)]
        public string PaymentId { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? StatusDetail { get; set; }

        [Precision(18, 2)]
        public decimal Monto { get; set; }

        public DateTime FechaEvento { get; set; } = DateTime.UtcNow;

        // JSON completo del payload del webhook (para debug/auditoría)
        public string? RawWebhookData { get; set; }

        public bool ProcesadoConExito { get; set; } = false;
    }
}

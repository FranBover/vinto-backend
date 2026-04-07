using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vinto.Api.Models
{
    public class Pedido
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AdministradorId { get; set; }

        [ForeignKey("AdministradorId")]
        public Administrador Administrador { get; set; }

        [Required, MaxLength(30)]
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(30)]
        public string Estado { get; set; } = "Pendiente";
        [Required, MaxLength(120)]
        public string NombreCliente { get; set; } = string.Empty;
        [Required]
        public string TelefonoCliente { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string FormaPago { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string FormaEntrega { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal? MontoPagoEfectivo { get; set; }
        [MaxLength(200)]
        public string? DireccionCliente { get; set; }
        [MaxLength(200)]
        public string? ReferenciaDireccion { get; set; }
        [MaxLength(300)]
        public string? UbicacionUrl { get; set; }

        
        [Required, Precision(18, 2)]
        public decimal Total { get; set; }

        public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    }
}

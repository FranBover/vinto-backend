using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.Models
{
    public class OpcionVariante
    {
        public int Id { get; set; }

        [Required]
        public int TipoVarianteId { get; set; }

        public TipoVariante TipoVariante { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Valor { get; set; } = string.Empty;

        public int Orden { get; set; } = 0;
    }
}

namespace Vinto.Api.Models
{
    public class Imagen
    {
        public int Id { get; set; }

        public int AdministradorId { get; set; }
        public Administrador Administrador { get; set; } = null!;

        public string NombreOriginal { get; set; } = string.Empty;
        public string NombreAlmacenado { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long TamanioBytes { get; set; }
        public string Url { get; set; } = string.Empty;

        /// <summary>"producto" o "logo"</summary>
        public string Tipo { get; set; } = string.Empty;

        /// <summary>ProductoId si Tipo="producto", null si Tipo="logo"</summary>
        public int? EntidadId { get; set; }

        public int Orden { get; set; } = 0;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}

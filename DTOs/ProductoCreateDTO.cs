namespace Eat_Experience.DTOs
{
    public class ProductoCreateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
        public bool Disponible { get; set; } = true;
        public int CategoriaId { get; set; }

        // Se mantiene en el body por compatibilidad (por ahora).
        public int AdministradorId { get; set; }
    }
}


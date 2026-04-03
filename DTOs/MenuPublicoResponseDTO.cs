namespace Eat_Experience.DTOs
{
    public class ProductoExtraMenuDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioAdicional { get; set; }
    }

    public class ProductoMenuDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public string? ImagenUrl { get; set; }
        public bool Disponible { get; set; }
        public List<ProductoExtraMenuDTO> Extras { get; set; } = new();
    }

    public class CategoriaMenuDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public List<ProductoMenuDTO> Productos { get; set; } = new();
    }

    public class LocalInfoDTO
    {
        public string NombreLocal { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string? LinkWhatsapp { get; set; }
        public string? LogoUrl { get; set; }
        public string Direccion { get; set; } = string.Empty;
        public bool EsActivo { get; set; }
    }

    public class MenuPublicoResponseDTO
    {
        public LocalInfoDTO Local { get; set; } = null!;
        public List<CategoriaMenuDTO> Categorias { get; set; } = new();
    }
}

namespace Vinto.Api.DTOs
{
    public class ImagenMenuDTO
    {
        public string Url { get; set; } = string.Empty;
        public int Orden { get; set; }
    }

    public class ProductoExtraMenuDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioAdicional { get; set; }
    }

    public class OpcionVarianteMenuDTO
    {
        public int Id { get; set; }
        public string Valor { get; set; } = string.Empty;
        public int Orden { get; set; }
    }

    public class TipoVarianteMenuDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public List<OpcionVarianteMenuDTO> Opciones { get; set; } = new();
    }

    public class VarianteMenuDTO
    {
        public int Id { get; set; }
        public decimal Precio { get; set; }
        public int? Stock { get; set; }
        public bool Disponible { get; set; }
        public int Opcion1Id { get; set; }
        public int? Opcion2Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
    }

    public class ProductoMenuDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal? Precio { get; set; }
        public string? ImagenUrl { get; set; }
        public bool Disponible { get; set; }
        public bool TieneVariantes { get; set; }
        public List<ProductoExtraMenuDTO> Extras { get; set; } = new();
        public List<ImagenMenuDTO> Imagenes { get; set; } = new();
        public List<TipoVarianteMenuDTO> TiposVariante { get; set; } = new();
        public List<VarianteMenuDTO> Variantes { get; set; } = new();
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
        public string? LogoImagenUrl { get; set; }
        public string Direccion { get; set; } = string.Empty;
        public bool EsActivo { get; set; }
        public string? AliasTransferencia { get; set; }
        public string? TitularCuenta { get; set; }
        public string? Horarios { get; set; }
        public string? UbicacionUrl { get; set; }
        public string ZonaEnvio { get; set; } = "Nacional";
        public decimal? CostoEnvio { get; set; }
    }

    public class MenuPublicoResponseDTO
    {
        public LocalInfoDTO Local { get; set; } = null!;
        public List<CategoriaMenuDTO> Categorias { get; set; } = new();
    }
}

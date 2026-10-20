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

    public class DescuentoMenuItemDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
    }

    public class DescuentoPedidoCompletoMenuDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public decimal Valor { get; set; }
    }

    public class VarianteMenuDTO
    {
        public int Id { get; set; }
        public decimal Precio { get; set; }
        public decimal PrecioConDescuento { get; set; }
        public int PorcentajeDescuentoTotal { get; set; }
        public List<DescuentoMenuItemDTO> DescuentosAplicados { get; set; } = new();
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
        public decimal? PrecioConDescuento { get; set; }
        public int PorcentajeDescuentoTotal { get; set; }
        public List<DescuentoMenuItemDTO> DescuentosAplicados { get; set; } = new();
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
        public int Orden { get; set; }
        public string? ImagenUrl { get; set; }
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

        // LEGADO. Antes era la cuenta habilitada del local; hoy el menú solo se sirve a cuentas habilitadas, así que
        // siempre valía true. Se conserva porque el front desplegado lo lee para mostrar "cerrado": al responder, el
        // menú le pone el valor de Estado.EstaAbierto (se calcula en cada request, nunca sale de la caché).
        // El front nuevo debe usar Estado y Horario; cuando ya no quede front viejo, este campo se elimina.
        public bool EsActivo { get; set; }
        public string? AliasTransferencia { get; set; }
        public string? TitularCuenta { get; set; }

        // LEGADO. Horario en texto libre (Administrador.Horarios). Se sigue devolviendo tal cual para el front viejo;
        // los horarios estructurados viajan en MenuPublicoResponseDTO.Horario.
        public string? Horarios { get; set; }
        public string? UbicacionUrl { get; set; }
        public string ZonaEnvio { get; set; } = "Nacional";
        public decimal? CostoEnvio { get; set; }
        public bool MercadoPagoHabilitado { get; set; }

        // Copia para responder sin mutar la instancia que vive en la caché del menú: lo que depende del request
        // (host del logo) o de la hora (esActivo = local abierto ahora) se fija acá, nunca en la entrada cacheada.
        public LocalInfoDTO ParaRespuesta(string? logoImagenUrl, bool esActivo)
        {
            var copia = (LocalInfoDTO)MemberwiseClone();
            copia.LogoImagenUrl = logoImagenUrl;
            copia.EsActivo = esActivo;
            return copia;
        }
    }

    // Horarios estructurados del local, en el mismo formato ("HH:mm") que el GET del panel.
    // Sin filas: Dias vacío = el local no tiene horarios fijos (siempre abierto).
    public class HorarioMenuDTO
    {
        public string ZonaHoraria { get; set; } = string.Empty;
        public List<HorarioDiaDTO> Dias { get; set; } = new();
    }

    public class MenuPublicoResponseDTO
    {
        public LocalInfoDTO Local { get; set; } = null!;
        public List<CategoriaMenuDTO> Categorias { get; set; } = new();
        public List<DescuentoPedidoCompletoMenuDTO> DescuentosPedidoCompleto { get; set; } = new();

        // Estado del local en este instante. Depende de la hora: se calcula en cada request y NUNCA se cachea
        // (en la entrada cacheada vale null).
        public EstadoLocal? Estado { get; set; }

        // No depende de la hora: sí se cachea (se invalida al cambiar horarios, zona o cierre manual).
        public HorarioMenuDTO? Horario { get; set; }
    }
}

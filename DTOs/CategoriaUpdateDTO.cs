namespace Vinto.Api.DTOs
{
    public class CategoriaUpdateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public int? Orden { get; set; }

        // Se mantiene en el body por compatibilidad (por ahora).
        public int AdministradorId { get; set; }
    }
}

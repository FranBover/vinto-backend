namespace Eat_Experience.DTOs
{
    public class CategoriaCreateDTO
    {
        public string Nombre { get; set; } = string.Empty;

        // Se mantiene en el body por compatibilidad (por ahora).
        public int AdministradorId { get; set; }
    }
}


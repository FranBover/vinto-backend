using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IAdministradorService
    {
        Task<IEnumerable<Administrador>> ObtenerTodos();
        Task<Administrador?> ObtenerPorId(int id);
        Task Crear(Administrador administrador);
        Task Actualizar(Administrador administrador);
        Task Eliminar(int id);
        // Valida formato y unicidad de un slug elegido a mano; devuelve el slug normalizado.
        // Lanza ValidacionException (400) si no es válido o ya lo usa otro local.
        Task<string> ValidarNuevoSlugAsync(int adminId, string slug);
    }
}

using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class DescuentoService : IDescuentoService
    {
        private readonly IDescuentoRepository _descuentoRepository;
        private readonly IProductoRepository _productoRepository;
        private readonly ICategoriaRepository _categoriaRepository;

        public DescuentoService(
            IDescuentoRepository descuentoRepository,
            IProductoRepository productoRepository,
            ICategoriaRepository categoriaRepository)
        {
            _descuentoRepository = descuentoRepository;
            _productoRepository = productoRepository;
            _categoriaRepository = categoriaRepository;
        }

        public async Task<List<DescuentoResponseDTO>> GetAllAsync(int administradorId, bool? activo = null)
        {
            var descuentos = await _descuentoRepository.ObtenerPorAdminAsync(administradorId, activo);
            return descuentos.Select(MapToResponseDto).ToList();
        }

        public async Task<DescuentoResponseDTO?> GetByIdAsync(int id, int administradorId)
        {
            var descuento = await _descuentoRepository.ObtenerPorIdAsync(id, administradorId);
            return descuento == null ? null : MapToResponseDto(descuento);
        }

        public async Task<DescuentoResponseDTO> CreateAsync(DescuentoCreateDTO dto, int administradorId)
        {
            await ValidarReglas(dto.Nombre, dto.Tipo, dto.Valor, dto.ProductoId, dto.CategoriaId,
                dto.AplicaAPedidoCompleto, dto.FechaInicio, dto.FechaFin, administradorId);

            var descuento = new Descuento
            {
                AdministradorId = administradorId,
                Nombre = dto.Nombre.Trim(),
                Tipo = dto.Tipo,
                Valor = dto.Valor,
                ProductoId = dto.ProductoId,
                CategoriaId = dto.CategoriaId,
                AplicaAPedidoCompleto = dto.AplicaAPedidoCompleto,
                FechaInicio = dto.FechaInicio,
                FechaFin = dto.FechaFin,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            var creado = await _descuentoRepository.CrearAsync(descuento);

            // Recargar con navegaciones para poblar nombres en el DTO
            var conNav = await _descuentoRepository.ObtenerPorIdAsync(creado.Id, administradorId);
            return MapToResponseDto(conNav!);
        }

        public async Task<DescuentoResponseDTO?> UpdateAsync(int id, DescuentoUpdateDTO dto, int administradorId)
        {
            var descuento = await _descuentoRepository.ObtenerPorIdAsync(id, administradorId);
            if (descuento == null)
                return null;

            await ValidarReglas(dto.Nombre, dto.Tipo, dto.Valor, dto.ProductoId, dto.CategoriaId,
                dto.AplicaAPedidoCompleto, dto.FechaInicio, dto.FechaFin, administradorId);

            descuento.Nombre = dto.Nombre.Trim();
            descuento.Tipo = dto.Tipo;
            descuento.Valor = dto.Valor;
            descuento.ProductoId = dto.ProductoId;
            descuento.CategoriaId = dto.CategoriaId;
            descuento.AplicaAPedidoCompleto = dto.AplicaAPedidoCompleto;
            descuento.FechaInicio = dto.FechaInicio;
            descuento.FechaFin = dto.FechaFin;
            descuento.Activo = dto.Activo;

            await _descuentoRepository.ActualizarAsync(descuento);

            var actualizado = await _descuentoRepository.ObtenerPorIdAsync(id, administradorId);
            return MapToResponseDto(actualizado!);
        }

        private async Task ValidarReglas(
            string nombre, string tipo, decimal valor,
            int? productoId, int? categoriaId, bool aplicaAPedidoCompleto,
            DateTime? fechaInicio, DateTime? fechaFin,
            int administradorId)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new ValidacionException("El nombre del descuento es obligatorio");

            if (nombre.Length > 100)
                throw new ValidacionException("El nombre del descuento no puede superar los 100 caracteres");

            if (tipo != "Porcentaje" && tipo != "MontoFijo")
                throw new ValidacionException("El tipo debe ser 'Porcentaje' o 'MontoFijo'");

            if (tipo == "Porcentaje" && (valor < 0.01m || valor > 100))
                throw new ValidacionException("Para un descuento porcentual, el valor debe estar entre 0.01 y 100");

            if (tipo == "MontoFijo" && valor <= 0)
                throw new ValidacionException("Para un descuento de monto fijo, el valor debe ser mayor a 0");

            int alcancesActivos = (productoId.HasValue ? 1 : 0)
                                + (categoriaId.HasValue ? 1 : 0)
                                + (aplicaAPedidoCompleto ? 1 : 0);

            if (alcancesActivos != 1)
                throw new ValidacionException("El descuento debe aplicarse a exactamente uno: producto específico, categoría, o pedido completo");

            if (fechaInicio.HasValue && fechaFin.HasValue && fechaFin <= fechaInicio)
                throw new ValidacionException("La fecha de fin debe ser posterior a la fecha de inicio");

            if (productoId.HasValue)
            {
                var producto = await _productoRepository.ObtenerPorId(productoId.Value);
                if (producto == null || producto.AdministradorId != administradorId)
                    throw new ValidacionException("Producto no encontrado", 404);
            }

            if (categoriaId.HasValue)
            {
                var categoria = await _categoriaRepository.ObtenerPorId(categoriaId.Value);
                if (categoria == null || categoria.AdministradorId != administradorId)
                    throw new ValidacionException("Categoría no encontrada", 404);
            }
        }

        private static DescuentoResponseDTO MapToResponseDto(Descuento d) => new()
        {
            Id = d.Id,
            Nombre = d.Nombre,
            Tipo = d.Tipo,
            Valor = d.Valor,
            ProductoId = d.ProductoId,
            ProductoNombre = d.Producto?.Nombre,
            CategoriaId = d.CategoriaId,
            CategoriaNombre = d.Categoria?.Nombre,
            AplicaAPedidoCompleto = d.AplicaAPedidoCompleto,
            FechaInicio = d.FechaInicio,
            FechaFin = d.FechaFin,
            Activo = d.Activo,
            FechaCreacion = d.FechaCreacion
        };
    }
}

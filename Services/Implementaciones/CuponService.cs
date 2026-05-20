using System.Globalization;
using System.Text.RegularExpressions;
using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class CuponService : ICuponService
    {
        private readonly ICuponRepository _cuponRepository;

        public CuponService(ICuponRepository cuponRepository)
        {
            _cuponRepository = cuponRepository;
        }

        public async Task<List<CuponResponseDTO>> GetAllAsync(int administradorId, bool? activo = null)
        {
            var cupones = await _cuponRepository.ObtenerPorAdminAsync(administradorId, activo);
            return cupones.Select(MapToResponseDto).ToList();
        }

        public async Task<CuponResponseDTO?> GetByIdAsync(int id, int administradorId)
        {
            var cupon = await _cuponRepository.ObtenerPorIdAsync(id, administradorId);
            return cupon == null ? null : MapToResponseDto(cupon);
        }

        public async Task<CuponResponseDTO> CreateAsync(CuponCreateDTO dto, int administradorId)
        {
            var codigoNorm = NormalizarCodigo(dto.Codigo);

            ValidarCodigo(codigoNorm);
            ValidarTipoValor(dto.Tipo, dto.Valor);
            ValidarCamposOpcionales(dto.LimiteUsos, dto.PedidoMinimo);

            var existente = await _cuponRepository.ObtenerPorCodigoAsync(codigoNorm, administradorId);
            if (existente != null)
                throw new ValidacionException("Ya existe un cupón con ese código en tu local");

            var cupon = new Cupon
            {
                AdministradorId = administradorId,
                Codigo = codigoNorm,
                Tipo = dto.Tipo,
                Valor = dto.Valor,
                FechaVencimiento = dto.FechaVencimiento,
                LimiteUsos = dto.LimiteUsos,
                PedidoMinimo = dto.PedidoMinimo,
                UsosActuales = 0,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            var creado = await _cuponRepository.CrearAsync(cupon);
            return MapToResponseDto(creado);
        }

        public async Task<CuponResponseDTO?> UpdateAsync(int id, CuponUpdateDTO dto, int administradorId)
        {
            var cupon = await _cuponRepository.ObtenerPorIdAsync(id, administradorId);
            if (cupon == null)
                return null;

            var codigoNorm = NormalizarCodigo(dto.Codigo);

            if (cupon.UsosActuales > 0)
            {
                bool codigoCambia = codigoNorm != cupon.Codigo;
                bool tipoCambia = dto.Tipo != cupon.Tipo;
                bool valorCambia = dto.Valor != cupon.Valor;
                bool fechaCambia = dto.FechaVencimiento != cupon.FechaVencimiento;
                bool limiteCambia = dto.LimiteUsos != cupon.LimiteUsos;
                bool minimoCambia = dto.PedidoMinimo != cupon.PedidoMinimo;

                if (codigoCambia || tipoCambia || valorCambia || fechaCambia || limiteCambia || minimoCambia)
                    throw new ValidacionException("Este cupón ya tiene usos y no puede modificarse. Podés desactivarlo y crear uno nuevo.");

                cupon.Activo = dto.Activo;
                await _cuponRepository.ActualizarAsync(cupon);
                return MapToResponseDto(cupon);
            }

            ValidarCodigo(codigoNorm);
            ValidarTipoValor(dto.Tipo, dto.Valor);
            ValidarCamposOpcionales(dto.LimiteUsos, dto.PedidoMinimo);

            if (codigoNorm != cupon.Codigo)
            {
                var existente = await _cuponRepository.ObtenerPorCodigoAsync(codigoNorm, administradorId);
                if (existente != null)
                    throw new ValidacionException("Ya existe un cupón con ese código en tu local");
            }

            cupon.Codigo = codigoNorm;
            cupon.Tipo = dto.Tipo;
            cupon.Valor = dto.Valor;
            cupon.FechaVencimiento = dto.FechaVencimiento;
            cupon.LimiteUsos = dto.LimiteUsos;
            cupon.PedidoMinimo = dto.PedidoMinimo;
            cupon.Activo = dto.Activo;

            await _cuponRepository.ActualizarAsync(cupon);
            return MapToResponseDto(cupon);
        }

        public async Task<CuponMetricasDTO?> GetMetricasAsync(int id, int administradorId)
        {
            var cupon = await _cuponRepository.ObtenerPorIdAsync(id, administradorId);
            if (cupon == null)
                return null;

            return await _cuponRepository.ObtenerMetricasAsync(id);
        }

        public async Task<ValidarCuponResponseDTO> ValidarCuponPublicoAsync(string slug, ValidarCuponRequestDTO request)
        {
            var slugNorm = slug.Trim().ToLowerInvariant();

            var admin = await _cuponRepository.ObtenerAdminActivoPorSlugAsync(slugNorm);
            if (admin == null)
                throw new KeyNotFoundException("Local no encontrado.");

            var codigoNorm = NormalizarCodigo(request.Codigo);

            if (string.IsNullOrEmpty(codigoNorm))
                return Invalido("Debés ingresar un código de cupón");

            var cupon = await _cuponRepository.ObtenerPorCodigoAsync(codigoNorm, admin.Id);

            if (cupon == null || !cupon.Activo)
                return Invalido("El cupón no existe o fue dado de baja");

            if (cupon.FechaVencimiento.HasValue && cupon.FechaVencimiento.Value < DateTime.UtcNow)
                return Invalido("El cupón está vencido");

            if (cupon.LimiteUsos.HasValue && cupon.UsosActuales >= cupon.LimiteUsos.Value)
                return Invalido("El cupón alcanzó el límite de usos");

            if (cupon.PedidoMinimo.HasValue && request.SubtotalPostDescuentos < cupon.PedidoMinimo.Value)
            {
                var montoFormateado = FormatearMonto(cupon.PedidoMinimo.Value);
                return Invalido($"El cupón requiere un pedido mínimo de {montoFormateado}");
            }

            decimal monto;
            if (cupon.Tipo == "Porcentaje")
            {
                monto = Math.Round(request.SubtotalPostDescuentos * (cupon.Valor / 100), 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                if (cupon.Valor > request.SubtotalPostDescuentos)
                    return Invalido("El cupón supera el valor del pedido. Agregá más productos o usá otro cupón");

                monto = cupon.Valor;
            }

            return new ValidarCuponResponseDTO
            {
                Valido = true,
                Codigo = cupon.Codigo,
                Tipo = cupon.Tipo,
                Valor = cupon.Valor,
                MontoDescuento = monto,
                NuevoSubtotal = request.SubtotalPostDescuentos - monto
            };
        }

        private static ValidarCuponResponseDTO Invalido(string motivo) =>
            new() { Valido = false, Motivo = motivo };

        private static string FormatearMonto(decimal monto)
        {
            var cultura = new CultureInfo("es-AR");
            return monto == Math.Floor(monto)
                ? "$" + monto.ToString("N0", cultura)
                : "$" + monto.ToString("N2", cultura);
        }

        private static string NormalizarCodigo(string codigo) =>
            codigo.Trim().ToUpperInvariant();

        private static void ValidarCodigo(string codigoNorm)
        {
            if (string.IsNullOrEmpty(codigoNorm))
                throw new ValidacionException("El código del cupón es obligatorio");

            if (codigoNorm.Length < 3 || codigoNorm.Length > 30)
                throw new ValidacionException("El código debe tener entre 3 y 30 caracteres");

            if (!Regex.IsMatch(codigoNorm, @"^[A-Z0-9]+$"))
                throw new ValidacionException("El código solo puede contener letras y números");
        }

        private static void ValidarTipoValor(string tipo, decimal valor)
        {
            if (tipo != "Porcentaje" && tipo != "MontoFijo")
                throw new ValidacionException("El tipo debe ser 'Porcentaje' o 'MontoFijo'");

            if (tipo == "Porcentaje" && (valor < 0.01m || valor > 100))
                throw new ValidacionException("Para un cupón porcentual, el valor debe estar entre 0.01 y 100");

            if (tipo == "MontoFijo" && valor <= 0)
                throw new ValidacionException("Para un cupón de monto fijo, el valor debe ser mayor a 0");
        }

        private static void ValidarCamposOpcionales(int? limiteUsos, decimal? pedidoMinimo)
        {
            if (limiteUsos.HasValue && limiteUsos.Value <= 0)
                throw new ValidacionException("El límite de usos debe ser mayor a 0");

            if (pedidoMinimo.HasValue && pedidoMinimo.Value <= 0)
                throw new ValidacionException("El pedido mínimo debe ser mayor a 0");
        }

        private static CuponResponseDTO MapToResponseDto(Cupon c) => new()
        {
            Id = c.Id,
            Codigo = c.Codigo,
            Tipo = c.Tipo,
            Valor = c.Valor,
            FechaVencimiento = c.FechaVencimiento,
            LimiteUsos = c.LimiteUsos,
            UsosActuales = c.UsosActuales,
            PedidoMinimo = c.PedidoMinimo,
            Activo = c.Activo,
            FechaCreacion = c.FechaCreacion
        };
    }
}

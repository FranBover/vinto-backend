using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Vinto.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IOptions<JwtSettings> jwtSettings, IConfiguration configuration)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterAdminDTO dto)
        {
            var adminKey = _configuration["AdminRegistroKey"];
            var headerKey = Request.Headers["X-Admin-Key"].FirstOrDefault();

            if (string.IsNullOrEmpty(headerKey) || headerKey != adminKey)
                return Unauthorized("Clave de registro inválida.");

            var emailExiste = _context.Administradores.Any(a => a.Email == dto.Email);
            if (emailExiste)
                return Conflict("Ya existe un administrador con ese email.");

            var passwordHasher = new PasswordHasher<Administrador>();
            var admin = new Administrador
            {
                Nombre = dto.Nombre,
                Email = dto.Email,
                NombreLocal = dto.NombreLocal,
                Telefono = dto.Telefono,
                Direccion = dto.Direccion,
                EsActivo = true,
                FechaRegistro = DateTime.UtcNow,
                PasswordHash = string.Empty
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, dto.Password);

            _context.Administradores.Add(admin);
            await _context.SaveChangesAsync();

            return CreatedAtAction(null, null, new
            {
                id = admin.Id,
                nombre = admin.Nombre,
                email = admin.Email,
                nombreLocal = admin.NombreLocal
            });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequestDTO login)
        {
            var admin = _context.Administradores.FirstOrDefault(a => a.Email == login.Email);
            if (admin == null)
                return Unauthorized("Email o contraseña incorrectos.");

            var passwordHasher = new PasswordHasher<Administrador>();
            var result = passwordHasher.VerifyHashedPassword(null, admin.PasswordHash, login.Contraseña);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Email o contraseña incorrectos.");

            var token = GenerateToken(admin);
            return Ok(new { token });
        }

        private string GenerateToken(Administrador admin)
        {
            Console.WriteLine("SecretKey cargada: " + _jwtSettings.Key); // Debug


            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, admin.Email),
                new Claim("adminId", admin.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationInMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

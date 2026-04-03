using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Eat_Experience.Data;
using Eat_Experience.DTOs;
using Eat_Experience.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Eat_Experience.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtSettings _jwtSettings;

        public AuthController(AppDbContext context, IOptions<JwtSettings> jwtSettings)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
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

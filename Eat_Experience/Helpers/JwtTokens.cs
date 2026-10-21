using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Vinto.Api.Models;

namespace Vinto.Api.Helpers
{
    // Único lugar que arma y valida los dos JWT del sistema (Program.cs y AuthController los usan desde acá):
    //  - Sesión del panel: audience = JwtSettings:Audience, dura JwtSettings:ExpirationInMinutes, va en el
    //    header Authorization. Lo valida el esquema JwtBearer por defecto.
    //  - Ticket del hub: audience propio (AudienceHub), dura 60 s y solo sirve para abrir la conexión de
    //    SignalR. Lo valida el esquema EsquemaHub, el único que lee access_token del query string.
    // Misma clave e issuer, audience distinto: cada esquema valida el suyo, así que ninguno de los dos
    // sirve en el lugar del otro. No hay que "mirar la ruta" para separarlos.
    public static class JwtTokens
    {
        public const string EsquemaHub = "HubTicket";
        public const int DuracionTicketSegundos = 60;

        // Con la tolerancia por defecto (5 min) un ticket de 60 s viviría seis minutos. Alcanza con cubrir
        // la diferencia de reloj entre instancias del App Service.
        private static readonly TimeSpan ToleranciaRelojTicket = TimeSpan.FromSeconds(5);

        // Derivado del audience de la API: no agrega una clave de configuración y nunca coincide con él.
        public static string AudienceHub(JwtSettings jwt) => jwt.Audience + ".hub";

        public static string EmitirSesion(JwtSettings jwt, string email, int adminId, DateTime ahoraUtc)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim("adminId", adminId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwt.Issuer,
                audience: jwt.Audience,
                claims: claims,
                expires: ahoraUtc.AddMinutes(jwt.ExpirationInMinutes),
                signingCredentials: Credenciales(jwt)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string EmitirTicketHub(JwtSettings jwt, int adminId, DateTime ahoraUtc)
        {
            var claims = new[]
            {
                new Claim("adminId", adminId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwt.Issuer,
                audience: AudienceHub(jwt),
                claims: claims,
                expires: ahoraUtc.AddSeconds(DuracionTicketSegundos),
                signingCredentials: Credenciales(jwt)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static TokenValidationParameters ParametrosApi(JwtSettings jwt) => new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = Clave(jwt)
        };

        public static TokenValidationParameters ParametrosHub(JwtSettings jwt) => new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = AudienceHub(jwt),
            IssuerSigningKey = Clave(jwt),
            ClockSkew = ToleranciaRelojTicket
        };

        private static SymmetricSecurityKey Clave(JwtSettings jwt)
        {
            if (string.IsNullOrEmpty(jwt.Key))
                throw new InvalidOperationException("JWT key not found");

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        }

        private static SigningCredentials Credenciales(JwtSettings jwt) =>
            new(Clave(jwt), SecurityAlgorithms.HmacSha256);
    }
}

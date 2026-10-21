using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Vinto.Api.Helpers;
using Vinto.Api.Models;

namespace Eat_Experience.Tests;

// El ticket del hub y el JWT de sesión comparten clave e issuer y se distinguen solo por el audience.
// Estos tests validan con los mismos TokenValidationParameters que usa Program.cs para cada esquema
// (JwtTokens.ParametrosApi / ParametrosHub), con el mismo handler que JwtBearer (JsonWebTokenHandler).
public class JwtTokensTests
{
    private const int AdminId = 42;

    private static readonly JwtSettings Jwt = new()
    {
        Key = "clave-de-prueba-de-al-menos-32-caracteres!!",
        Issuer = "VintoAPI",
        Audience = "VintoFrontend",
        ExpirationInMinutes = 720
    };

    private static Task<TokenValidationResult> ComoToken(string token, TokenValidationParameters parametros) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, parametros);

    private static string Sesion(DateTime? ahoraUtc = null) =>
        JwtTokens.EmitirSesion(Jwt, "admin@ejemplo.com", AdminId, ahoraUtc ?? DateTime.UtcNow);

    private static string Ticket(DateTime? ahoraUtc = null) =>
        JwtTokens.EmitirTicketHub(Jwt, AdminId, ahoraUtc ?? DateTime.UtcNow);

    // ---------- Cada token pasa por SU esquema ----------

    [Fact]
    public async Task El_ticket_pasa_como_ticket_del_hub_y_lleva_el_adminId()
    {
        var resultado = await ComoToken(Ticket(), JwtTokens.ParametrosHub(Jwt));

        Assert.True(resultado.IsValid, resultado.Exception?.Message);
        Assert.Equal(AdminId.ToString(), resultado.Claims["adminId"]?.ToString());
    }

    [Fact]
    public async Task El_jwt_de_sesion_pasa_como_token_de_la_API()
    {
        var resultado = await ComoToken(Sesion(), JwtTokens.ParametrosApi(Jwt));

        Assert.True(resultado.IsValid, resultado.Exception?.Message);
    }

    // ---------- El audience se valida en los dos sentidos ----------

    [Fact]
    public async Task Un_ticket_no_pasa_como_token_de_la_API()
    {
        var resultado = await ComoToken(Ticket(), JwtTokens.ParametrosApi(Jwt));

        Assert.False(resultado.IsValid);
        Assert.IsType<SecurityTokenInvalidAudienceException>(resultado.Exception);
    }

    [Fact]
    public async Task Un_token_de_la_API_no_pasa_como_ticket_del_hub()
    {
        var resultado = await ComoToken(Sesion(), JwtTokens.ParametrosHub(Jwt));

        Assert.False(resultado.IsValid);
        Assert.IsType<SecurityTokenInvalidAudienceException>(resultado.Exception);
    }

    [Theory]
    [InlineData("VintoFrontend")]
    [InlineData("hub")]
    [InlineData("VintoFrontend.hub")]
    public void El_audience_del_hub_nunca_coincide_con_el_de_la_API(string audienceApi)
    {
        var jwt = new JwtSettings { Key = Jwt.Key, Issuer = Jwt.Issuer, Audience = audienceApi };

        Assert.NotEqual(jwt.Audience, JwtTokens.AudienceHub(jwt));
    }

    // ---------- Vida del ticket ----------

    [Fact]
    public void El_ticket_dura_60_segundos()
    {
        var ahora = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(Ticket(ahora));

        Assert.Equal(ahora.AddSeconds(60), token.ValidTo);
        Assert.Equal(60, JwtTokens.DuracionTicketSegundos);
    }

    [Fact]
    public async Task Un_ticket_vencido_se_rechaza()
    {
        // Emitido hace 2 min: venció hace 1 min. Con la tolerancia por defecto de JwtBearer (5 min)
        // todavía pasaría; el ticket usa una de 5 s.
        var resultado = await ComoToken(Ticket(DateTime.UtcNow.AddMinutes(-2)), JwtTokens.ParametrosHub(Jwt));

        Assert.False(resultado.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(resultado.Exception);
    }

    [Fact]
    public async Task Una_sesion_emitida_hace_una_hora_sigue_valida_en_la_API()
    {
        // Contraste: la sesión dura 12 h; que el ticket sea de 60 s no le afecta.
        var resultado = await ComoToken(Sesion(DateTime.UtcNow.AddMinutes(-60)), JwtTokens.ParametrosApi(Jwt));

        Assert.True(resultado.IsValid, resultado.Exception?.Message);
    }

    // ---------- Firma e issuer ----------

    [Fact]
    public async Task Un_ticket_firmado_con_otra_clave_se_rechaza()
    {
        var otra = new JwtSettings { Key = "otra-clave-distinta-de-al-menos-32-caracteres", Issuer = Jwt.Issuer, Audience = Jwt.Audience };

        var resultado = await ComoToken(JwtTokens.EmitirTicketHub(otra, AdminId, DateTime.UtcNow), JwtTokens.ParametrosHub(Jwt));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public async Task Un_ticket_de_otro_issuer_se_rechaza()
    {
        var otro = new JwtSettings { Key = Jwt.Key, Issuer = "OtraApi", Audience = Jwt.Audience };

        var resultado = await ComoToken(JwtTokens.EmitirTicketHub(otro, AdminId, DateTime.UtcNow), JwtTokens.ParametrosHub(Jwt));

        Assert.False(resultado.IsValid);
        Assert.IsType<SecurityTokenInvalidIssuerException>(resultado.Exception);
    }
}

using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using static Eat_Experience.Tests.Horarios;

namespace Eat_Experience.Tests;

// Fechas de referencia (2026): lun 19, mar 20, mié 21, jue 22, vie 23, sáb 24, dom 25, lun 26 de octubre.
public class LocalCerradoMensajeTests
{
    private static EstadoLocal Cerrado(DateTimeOffset? apertura, string motivo = MotivoEstadoLocal.Horario)
        => new(false, motivo, apertura);

    private static string Armar(EstadoLocal estado, DateTimeOffset ahora, string zona = Argentina)
        => LocalCerradoMensaje.Armar(estado, zona, ahora);

    // ---------- Cuándo abre ----------

    [Fact]
    public void Abre_hoy_a_las_19()
        => Assert.Equal("El local está cerrado. Abre hoy a las 19:00.",
            Armar(Cerrado(EnArgentina(2026, 10, 20, 19, 0)), EnArgentina(2026, 10, 20, 12, 0)));

    [Fact]
    public void Abre_manana_si_la_apertura_cae_en_el_dia_siguiente()
        => Assert.Equal("El local está cerrado. Abre mañana a las 10:00.",
            Armar(Cerrado(EnArgentina(2026, 10, 21, 10, 0)), EnArgentina(2026, 10, 20, 23, 30)));

    [Fact]
    public void Abre_un_dia_de_la_semana_si_faltan_entre_2_y_6_dias()
        => Assert.Equal("El local está cerrado. Abre el viernes a las 19:00.",
            Armar(Cerrado(EnArgentina(2026, 10, 23, 19, 0)), EnArgentina(2026, 10, 20, 12, 0)));

    [Fact]
    public void Abre_el_proximo_si_es_el_mismo_dia_de_la_semana_pero_de_la_semana_que_viene()
        => Assert.Equal("El local está cerrado. Abre el próximo lunes a las 10:00.",
            Armar(Cerrado(EnArgentina(2026, 10, 26, 10, 0)), EnArgentina(2026, 10, 19, 15, 0)));

    [Fact]
    public void Con_seis_dias_de_diferencia_todavia_dice_el_dia_sin_proximo()
        => Assert.Equal("El local está cerrado. Abre el domingo a las 12:00.",
            Armar(Cerrado(EnArgentina(2026, 10, 25, 12, 0)), EnArgentina(2026, 10, 19, 15, 0)));

    // ---------- La hora y el día se dicen en la zona del LOCAL ----------

    [Fact]
    public void La_hora_es_la_del_local_y_no_la_UTC()
    {
        // 22:00 UTC = 19:00 en Argentina.
        var apertura = new DateTimeOffset(2026, 10, 20, 22, 0, 0, TimeSpan.Zero);
        var ahora = new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

        var mensaje = Armar(Cerrado(apertura), ahora);

        Assert.Contains("19:00", mensaje);
        Assert.DoesNotContain("22:00", mensaje);
    }

    [Fact]
    public void Un_mismo_instante_se_dice_distinto_segun_la_zona_del_local()
    {
        var apertura = new DateTimeOffset(2026, 10, 20, 23, 0, 0, TimeSpan.Zero); // 20:00 en AR, 19:00 en Nueva York (EDT)
        var ahora = new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

        Assert.Contains("a las 20:00", Armar(Cerrado(apertura), ahora, Argentina));
        Assert.Contains("a las 19:00", Armar(Cerrado(apertura), ahora, "America/New_York"));
    }

    [Fact]
    public void El_hoy_y_el_manana_se_deciden_con_la_fecha_local_no_con_la_de_UTC()
    {
        // Ahora: miércoles 02:00 UTC = martes 23:00 en AR. Apertura: miércoles 13:00 UTC = miércoles 10:00 en AR.
        // En UTC es el mismo día ("hoy"); para el local es mañana.
        var ahora = new DateTimeOffset(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);
        var apertura = new DateTimeOffset(2026, 10, 21, 13, 0, 0, TimeSpan.Zero);

        Assert.Equal("El local está cerrado. Abre mañana a las 10:00.", Armar(Cerrado(apertura), ahora));
    }

    [Fact]
    public void Una_apertura_pasada_la_medianoche_local_ya_es_de_manana()
        => Assert.Equal("El local está cerrado. Abre mañana a las 00:30.",
            Armar(Cerrado(EnArgentina(2026, 10, 21, 0, 30)), EnArgentina(2026, 10, 20, 23, 50)));

    // ---------- Redacción ----------

    [Fact]
    public void A_la_una_se_dice_a_la_no_a_las()
        => Assert.Equal("El local está cerrado. Abre mañana a la 01:00.",
            Armar(Cerrado(EnArgentina(2026, 10, 21, 1, 0)), EnArgentina(2026, 10, 20, 22, 0)));

    [Fact]
    public void Las_horas_de_un_digito_llevan_cero_adelante()
        => Assert.Contains("a las 09:05",
            Armar(Cerrado(EnArgentina(2026, 10, 21, 9, 5)), EnArgentina(2026, 10, 20, 22, 0)));

    [Fact]
    public void A_las_doce_de_la_noche_dice_00_00()
        => Assert.Contains("a las 00:00",
            Armar(Cerrado(EnArgentina(2026, 10, 21, 0, 0)), EnArgentina(2026, 10, 20, 22, 0)));

    // ---------- Sin hora de reapertura ----------

    [Fact]
    public void Cierre_manual_no_promete_hora_aunque_venga_un_proximo_cambio()
        => Assert.Equal("El local está cerrado por el momento.",
            Armar(Cerrado(EnArgentina(2026, 10, 20, 19, 0), MotivoEstadoLocal.CerradoManualmente), EnArgentina(2026, 10, 20, 12, 0)));

    [Fact]
    public void Cierre_manual_sin_proximo_cambio_tampoco()
        => Assert.Equal("El local está cerrado por el momento.",
            Armar(Cerrado(null, MotivoEstadoLocal.CerradoManualmente), EnArgentina(2026, 10, 20, 12, 0)));

    [Fact]
    public void Cerrado_por_horario_sin_ninguna_apertura_prevista_no_promete_hora()
        => Assert.Equal("El local está cerrado por el momento.",
            Armar(Cerrado(null), EnArgentina(2026, 10, 20, 12, 0)));

    [Theory]
    [InlineData("")]
    [InlineData("No/Existe")]
    public void Con_una_zona_rota_informa_sin_hora_en_lugar_de_fallar(string zona)
        => Assert.Equal("El local está cerrado por el momento.",
            Armar(Cerrado(EnArgentina(2026, 10, 20, 19, 0)), EnArgentina(2026, 10, 20, 12, 0), zona));

    // ---------- La excepción 409 ----------

    [Fact]
    public void LocalCerradoException_es_un_409_con_codigo_y_proximo_cambio()
    {
        var apertura = EnArgentina(2026, 10, 20, 19, 0);

        var ex = new LocalCerradoException("El local está cerrado.", apertura);

        Assert.IsAssignableFrom<ValidacionException>(ex);
        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("LocalCerrado", ex.Codigo);
        Assert.Equal(apertura, ex.ProximoCambio);
        Assert.Null(new LocalCerradoException("x", null).ProximoCambio);
    }
}

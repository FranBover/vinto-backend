using Vinto.Api.Helpers;
using Vinto.Api.Models;
using static Eat_Experience.Tests.Horarios;

namespace Eat_Experience.Tests;

public class HorarioLocalValidatorTests
{
    private static IReadOnlyList<string> Validar(IReadOnlyList<HorarioLocal> horarios, string? zona = Argentina)
        => HorarioLocalValidator.Validar(horarios, zona);

    private static void AssertError(IReadOnlyList<string> errores, string fragmento)
        => Assert.True(errores.Any(e => e.Contains(fragmento)),
            $"Se esperaba un error que contenga '{fragmento}'. Errores: [{string.Join(" | ", errores)}]");

    // ---------- Horarios válidos ----------

    [Fact]
    public void Un_horario_completo_y_valido_pasa()
    {
        var horarios = Semana(
            Turnos(Lunes, "10:00", "14:00", "19:00", "23:00"),
            Turnos(Martes, "10:00", "14:00", "19:00", "23:00"),
            Dia24h(Miercoles),
            Turnos(Jueves, "10:00", "14:00", "19:00", "23:00"),
            Turnos(Viernes, "10:00", "14:00", "19:00", "23:59"),
            Turnos(Sabado, "20:00", "01:00"),
            Cerrado(Domingo));

        Assert.Empty(Validar(horarios));
    }

    [Fact]
    public void Una_lista_vacia_es_valida_y_significa_local_siempre_abierto()
        => Assert.Empty(Validar(new List<HorarioLocal>()));

    [Fact]
    public void Un_turno_nocturno_que_termina_justo_cuando_empieza_otro_no_se_superpone()
    {
        // Sábado 20:00-01:00 y domingo 01:00-03:00: el cierre es exclusivo y la apertura inclusiva.
        var horarios = Semana(Turnos(Sabado, "20:00", "01:00"), Turnos(Domingo, "01:00", "03:00"));

        Assert.Empty(Validar(horarios));
    }

    [Fact]
    public void Los_siete_dias_24h_son_validos()
        => Assert.Empty(Validar(Semana24h()));

    // ---------- Cantidad de días: 0 o exactamente 7 ----------

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void Rechaza_menos_de_siete_dias(int cantidad)
    {
        var horarios = Semana().Take(cantidad).ToList();

        var errores = Validar(horarios);

        Assert.Single(errores);
        AssertError(errores, "tienen que cargar los 7 días de la semana");
        AssertError(errores, $"se recibieron {cantidad}");
    }

    [Fact]
    public void Rechaza_mas_de_siete_dias()
    {
        var horarios = Semana();
        horarios.Add(Cerrado(Lunes)); // un octavo, repetido

        var errores = Validar(horarios);

        AssertError(errores, "se recibieron 8");
        AssertError(errores, "Hay más de un horario para el lunes");
    }

    // ---------- Día de la semana ----------

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Rechaza_un_dia_fuera_de_0_a_6(int dia)
    {
        // Seis días válidos (domingo a viernes) + el inválido: son 7 filas, así que solo falla el día.
        var horarios = Semana().Take(6).ToList();
        horarios.Add(new HorarioLocal { DiaSemana = dia, Modo = ModoHorario.Cerrado });

        var errores = Validar(horarios);

        Assert.Single(errores);
        AssertError(errores, $"El día de la semana {dia} no es válido");
        AssertError(errores, "entre 0 (domingo) y 6 (sábado)");
    }

    [Fact]
    public void Rechaza_un_dia_repetido_y_lo_nombra()
    {
        // Siete filas con el martes dos veces (y sin miércoles).
        var horarios = Semana().Where(h => h.DiaSemana != Miercoles).ToList();
        horarios.Add(Turnos(Martes, "10:00", "14:00"));

        var errores = Validar(horarios);

        Assert.Single(errores);
        AssertError(errores, "Hay más de un horario para el martes");
    }

    // ---------- Modo y turnos cargados ----------

    [Fact]
    public void Rechaza_un_modo_desconocido_o_con_otras_mayusculas()
    {
        var errores = Validar(Semana(
            new HorarioLocal { DiaSemana = Lunes, Modo = "Abierto" },
            new HorarioLocal { DiaSemana = Martes, Modo = "turnos" },
            new HorarioLocal { DiaSemana = Miercoles, Modo = "" }));

        Assert.Equal(3, errores.Count);
        AssertError(errores, "El modo 'Abierto' del lunes no es válido");
        AssertError(errores, "El modo 'turnos' del martes no es válido");
        AssertError(errores, "El modo '' del miércoles no es válido");
        AssertError(errores, "Cerrado, 24h, Turnos");
    }

    [Fact]
    public void Rechaza_modo_Turnos_sin_turno_1()
    {
        var errores = Validar(Semana(new HorarioLocal { DiaSemana = Lunes, Modo = ModoHorario.Turnos }));

        Assert.Single(errores);
        AssertError(errores, "El lunes está en modo Turnos pero no tiene el turno 1 cargado");
    }

    [Fact]
    public void Rechaza_turno_2_sin_turno_1()
    {
        var horario = new HorarioLocal
        {
            DiaSemana = Lunes, Modo = ModoHorario.Turnos,
            Turno2Apertura = Hora("19:00"), Turno2Cierre = Hora("23:00")
        };

        AssertError(Validar(Semana(horario)), "el turno 2 no puede usarse sin el turno 1");
    }

    [Fact]
    public void Rechaza_turno_1_incompleto()
    {
        var horario = new HorarioLocal { DiaSemana = Lunes, Modo = ModoHorario.Turnos, Turno1Apertura = Hora("10:00") };

        AssertError(Validar(Semana(horario)), "El turno 1 del lunes está incompleto");
    }

    [Theory]
    [InlineData("19:00", null)]
    [InlineData(null, "23:00")]
    public void Rechaza_turno_2_incompleto(string? apertura2, string? cierre2)
    {
        var horario = Turnos(Lunes, "10:00", "14:00");
        horario.Turno2Apertura = apertura2 == null ? null : Hora(apertura2);
        horario.Turno2Cierre = cierre2 == null ? null : Hora(cierre2);

        AssertError(Validar(Semana(horario)), "El turno 2 del lunes está incompleto");
    }

    [Theory]
    [InlineData(ModoHorario.Cerrado)]
    [InlineData(ModoHorario.VeinticuatroHoras)]
    public void Rechaza_turnos_cargados_en_modo_Cerrado_o_24h(string modo)
    {
        var horario = new HorarioLocal { DiaSemana = Jueves, Modo = modo, Turno1Apertura = Hora("10:00"), Turno1Cierre = Hora("14:00") };

        var errores = Validar(Semana(horario));

        AssertError(errores, $"El jueves está en modo {modo} y no puede tener turnos cargados");
    }

    [Fact]
    public void Rechaza_un_solo_valor_de_turno_cargado_en_modo_Cerrado()
    {
        var horario = new HorarioLocal { DiaSemana = Jueves, Modo = ModoHorario.Cerrado, Turno2Cierre = Hora("23:00") };

        AssertError(Validar(Semana(horario)), "no puede tener turnos cargados");
    }

    // ---------- Apertura igual a cierre ----------

    [Fact]
    public void Rechaza_apertura_igual_a_cierre_y_sugiere_el_modo_24h()
    {
        var errores = Validar(Semana(Turnos(Viernes, "10:00", "14:00", "20:00", "20:00")));

        Assert.Single(errores);
        AssertError(errores, "El turno 2 del viernes abre y cierra a la misma hora (20:00)");
        AssertError(errores, "usá el modo 24h");
    }

    // ---------- Orden de los turnos del día ----------

    [Fact]
    public void Dos_turnos_seguidos_del_mismo_dia_son_validos()
        => Assert.Empty(Validar(Semana(Turnos(Martes, "10:00", "14:00", "14:00", "18:00"))));

    [Fact]
    public void El_turno_2_puede_cruzar_la_medianoche_porque_es_el_ultimo_del_dia()
        => Assert.Empty(Validar(Semana(Turnos(Viernes, "12:00", "15:00", "20:00", "02:00"))));

    [Fact]
    public void Un_unico_turno_puede_cruzar_la_medianoche()
        => Assert.Empty(Validar(Semana(Turnos(Sabado, "20:00", "03:00"))));

    [Fact]
    public void Rechaza_turno_2_que_empieza_antes_del_cierre_del_turno_1()
    {
        var errores = Validar(Semana(Turnos(Martes, "10:00", "14:00", "13:00", "18:00")));

        Assert.Single(errores);
        AssertError(errores, "El turno 2 del martes (13:00 a 18:00) tiene que empezar a partir del cierre del turno 1 (14:00)");
    }

    [Fact]
    public void Rechaza_turno_2_cargado_antes_que_el_turno_1()
    {
        // No se superponen, pero el turno 2 tiene que ser el que viene después.
        var errores = Validar(Semana(Turnos(Martes, "19:00", "23:00", "10:00", "14:00")));

        Assert.Single(errores);
        AssertError(errores, "El turno 2 del martes (10:00 a 14:00) tiene que empezar a partir del cierre del turno 1 (23:00)");
    }

    [Fact]
    public void Rechaza_que_el_turno_1_cruce_la_medianoche_si_hay_turno_2()
    {
        var errores = Validar(Semana(Turnos(Lunes, "22:00", "02:00", "23:00", "23:30")));

        Assert.Single(errores);
        AssertError(errores, "El turno 1 del lunes (22:00 a 02:00) cruza la medianoche, y solo el último turno del día puede hacerlo");
    }

    [Fact]
    public void Rechaza_que_el_turno_1_termine_a_las_00_00_si_hay_turno_2()
    {
        // 18:00-00:00 termina a la medianoche siguiente: no deja lugar a un turno 2 el mismo día.
        var errores = Validar(Semana(Turnos(Lunes, "18:00", "00:00", "20:00", "22:00")));

        AssertError(errores, "El turno 1 del lunes (18:00 a 00:00) cruza la medianoche");
    }

    // ---------- Superposiciones entre días ----------

    [Fact]
    public void Rechaza_un_turno_nocturno_que_pisa_el_primer_turno_del_dia_siguiente()
    {
        // Lunes 20:00-01:00 termina el martes a la 01:00 y el martes abre a las 00:30.
        var errores = Validar(Semana(Turnos(Lunes, "20:00", "01:00"), Turnos(Martes, "00:30", "03:00")));

        Assert.Single(errores);
        AssertError(errores, "Se superponen el turno 1 del lunes (20:00 a 01:00, termina el martes) y el turno 1 del martes (00:30 a 03:00)");
    }

    [Fact]
    public void El_desborde_del_sabado_se_compara_con_el_domingo_porque_la_semana_es_circular()
    {
        var errores = Validar(Semana(Turnos(Sabado, "20:00", "01:00"), Turnos(Domingo, "00:30", "03:00")));

        Assert.Single(errores);
        AssertError(errores, "Se superponen el turno 1 del sábado (20:00 a 01:00, termina el domingo) y el turno 1 del domingo (00:30 a 03:00)");
    }

    [Fact]
    public void Rechaza_un_turno_nocturno_que_pisa_un_dia_24h()
    {
        var errores = Validar(Semana(Turnos(Sabado, "20:00", "01:00"), Dia24h(Domingo)));

        Assert.Single(errores);
        AssertError(errores, "Se superponen el turno 1 del sábado (20:00 a 01:00, termina el domingo) y el domingo (24h)");
    }

    [Fact]
    public void El_desborde_de_un_turno_2_nocturno_tambien_se_chequea()
    {
        var errores = Validar(Semana(Turnos(Viernes, "10:00", "14:00", "22:00", "02:00"), Turnos(Sabado, "01:00", "05:00")));

        AssertError(errores, "turno 2 del viernes (22:00 a 02:00, termina el sábado)");
    }

    [Fact]
    public void Un_dia_con_errores_propios_no_genera_ademas_errores_de_superposicion()
    {
        // El lunes tiene apertura == cierre: se informa eso y no una superposición confusa derivada.
        var errores = Validar(Semana(Turnos(Lunes, "20:00", "20:00"), Turnos(Martes, "00:30", "03:00")));

        Assert.Single(errores);
        AssertError(errores, "abre y cierra a la misma hora");
    }

    // ---------- Zona horaria ----------

    [Theory]
    [InlineData("Marte/Olympus_Mons")]
    [InlineData("Buenos Aires")]
    [InlineData("../../etc/passwd")]
    public void Rechaza_una_zona_horaria_inexistente(string zona)
    {
        var errores = Validar(new List<HorarioLocal>(), zona);

        Assert.Single(errores);
        AssertError(errores, $"La zona horaria '{zona}' no es válida");
        AssertError(errores, "America/Argentina/Buenos_Aires");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rechaza_una_zona_horaria_vacia(string? zona)
        => AssertError(Validar(new List<HorarioLocal>(), zona), "La zona horaria es obligatoria");

    [Fact]
    public void Rechaza_una_zona_horaria_mas_larga_que_la_columna()
    {
        var zona = new string('a', 65);

        AssertError(Validar(new List<HorarioLocal>(), zona), "no es válida");
    }

    [Theory]
    [InlineData("America/Argentina/Buenos_Aires")]
    [InlineData("America/New_York")]
    [InlineData("UTC")]
    public void Acepta_zonas_horarias_IANA_validas(string zona)
        => Assert.Empty(Validar(new List<HorarioLocal>(), zona));

    // ---------- Varios errores juntos ----------

    [Fact]
    public void Devuelve_todos_los_errores_de_una_vez()
    {
        // Siete filas (así no salta el error de cantidad): un día inválido, uno sin turno 1 y uno con apertura == cierre.
        var horarios = new List<HorarioLocal>
        {
            new() { DiaSemana = 9, Modo = ModoHorario.Cerrado },
            new() { DiaSemana = Lunes, Modo = ModoHorario.Turnos },
            Turnos(Martes, "10:00", "10:00"),
            Cerrado(Miercoles), Cerrado(Jueves), Cerrado(Viernes), Cerrado(Sabado)
        };

        var errores = Validar(horarios, "No/Existe");

        Assert.Equal(4, errores.Count);
        AssertError(errores, "zona horaria");
        AssertError(errores, "día de la semana 9");
        AssertError(errores, "lunes está en modo Turnos");
        AssertError(errores, "martes abre y cierra a la misma hora");
    }

    // ---------- Nombres de día ----------

    [Theory]
    [InlineData(0, "domingo")]
    [InlineData(1, "lunes")]
    [InlineData(6, "sábado")]
    [InlineData(7, "día 7")]
    [InlineData(-1, "día -1")]
    public void NombreDelDia_usa_el_orden_de_System_DayOfWeek(int dia, string esperado)
        => Assert.Equal(esperado, HorarioLocalValidator.NombreDelDia(dia));
}

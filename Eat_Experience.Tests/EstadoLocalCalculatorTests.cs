using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Services.Implementaciones;
using static Eat_Experience.Tests.Horarios;

namespace Eat_Experience.Tests;

// Fechas de referencia (2026): dom 18, lun 19, mar 20, mié 21, jue 22, vie 23, sáb 24, dom 25, lun 26 de octubre.
public class EstadoLocalCalculatorTests
{
    private readonly EstadoLocalCalculator _calculator = new();

    private EstadoLocal Calcular(IReadOnlyList<HorarioLocal> horarios, DateTimeOffset ahoraUtc, bool cerradoManualmente = false, string zona = Argentina)
        => _calculator.Calcular(horarios, cerradoManualmente, zona, ahoraUtc);

    private static void Verificar(EstadoLocal estado, bool abierto, string motivo, DateTimeOffset? proximoCambio)
    {
        Assert.Equal(abierto, estado.EstaAbierto);
        Assert.Equal(motivo, estado.Motivo);
        Assert.Equal(proximoCambio, estado.ProximoCambio);
        if (estado.ProximoCambio is { } cambio)
            Assert.Equal(TimeSpan.Zero, cambio.Offset); // ProximoCambio viaja en UTC
    }

    [Fact]
    public void Las_fechas_de_referencia_caen_en_el_dia_que_dicen()
    {
        Assert.Equal(DayOfWeek.Tuesday, new DateOnly(2026, 10, 20).DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, new DateOnly(2026, 10, 24).DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, new DateOnly(2026, 10, 25).DayOfWeek);
    }

    // ---------- Sin horarios y cierre manual ----------

    [Fact]
    public void Sin_horarios_esta_siempre_abierto()
    {
        var estado = Calcular(new List<HorarioLocal>(), EnArgentina(2026, 10, 20, 3, 0));

        Verificar(estado, true, MotivoEstadoLocal.SinHorarios, null);
    }

    [Fact]
    public void Cierre_manual_gana_sobre_un_horario_que_dice_abierto()
    {
        var estado = Calcular(Semana24h(), EnArgentina(2026, 10, 20, 12, 0), cerradoManualmente: true);

        Verificar(estado, false, MotivoEstadoLocal.CerradoManualmente, null);
    }

    [Fact]
    public void Cierre_manual_gana_tambien_sin_horarios()
    {
        var estado = Calcular(new List<HorarioLocal>(), EnArgentina(2026, 10, 20, 12, 0), cerradoManualmente: true);

        Verificar(estado, false, MotivoEstadoLocal.CerradoManualmente, null);
    }

    // ---------- Martes 10:00-14:00 y 19:00-23:00 ----------

    private static List<HorarioLocal> MartesDosTurnos()
        => Semana(Turnos(Martes, "10:00", "14:00", "19:00", "23:00"));

    [Fact]
    public void Martes_13_59_esta_abierto_y_cierra_a_las_14()
        => Verificar(Calcular(MartesDosTurnos(), EnArgentina(2026, 10, 20, 13, 59)),
            true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 14, 0));

    [Fact]
    public void Martes_14_00_en_punto_ya_esta_cerrado_y_abre_a_las_19()
        => Verificar(Calcular(MartesDosTurnos(), EnArgentina(2026, 10, 20, 14, 0)),
            false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 19, 0));

    [Fact]
    public void Lunes_cerrado_al_mediodia_abre_el_martes_a_las_10()
        => Verificar(Calcular(MartesDosTurnos(), EnArgentina(2026, 10, 19, 12, 0)),
            false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 10, 0));

    [Fact]
    public void La_apertura_es_inclusiva()
        => Verificar(Calcular(MartesDosTurnos(), EnArgentina(2026, 10, 20, 10, 0)),
            true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 14, 0));

    [Fact]
    public void Martes_23_00_en_punto_cierra_y_la_proxima_apertura_es_el_martes_siguiente()
        => Verificar(Calcular(MartesDosTurnos(), EnArgentina(2026, 10, 20, 23, 0)),
            false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 27, 10, 0));

    // ---------- Turno nocturno: sábado 20:00-01:00, domingo cerrado ----------

    private static List<HorarioLocal> SabadoNocturno()
        => Semana(Turnos(Sabado, "20:00", "01:00"));

    [Fact]
    public void Sabado_23_00_esta_abierto_y_cierra_el_domingo_a_la_1()
        => Verificar(Calcular(SabadoNocturno(), EnArgentina(2026, 10, 24, 23, 0)),
            true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 25, 1, 0));

    [Fact]
    public void Domingo_00_30_sigue_abierto_por_el_desborde_del_sabado_aunque_el_domingo_figure_cerrado()
        => Verificar(Calcular(SabadoNocturno(), EnArgentina(2026, 10, 25, 0, 30)),
            true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 25, 1, 0));

    [Fact]
    public void Domingo_01_00_en_punto_ya_esta_cerrado_y_abre_el_sabado_siguiente()
        => Verificar(Calcular(SabadoNocturno(), EnArgentina(2026, 10, 25, 1, 0)),
            false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 31, 20, 0));

    // ---------- 24 horas y todo cerrado ----------

    [Fact]
    public void Abierto_las_24h_todos_los_dias_nunca_cambia()
        => Verificar(Calcular(Semana24h(), EnArgentina(2026, 10, 20, 12, 0)),
            true, MotivoEstadoLocal.Horario, null);

    [Fact]
    public void Lunes_24h_y_martes_cerrado_lunes_23_00_esta_abierto_y_cierra_a_las_00()
        => Verificar(Calcular(Semana(Dia24h(Lunes)), EnArgentina(2026, 10, 19, 23, 0)),
            true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 0, 0));

    [Fact]
    public void Todos_los_dias_cerrados_esta_cerrado_y_no_hay_cambio()
        => Verificar(Calcular(Semana(), EnArgentina(2026, 10, 20, 12, 0)),
            false, MotivoEstadoLocal.Horario, null);

    // ---------- Zona horaria ----------

    [Fact]
    public void Las_02_00_UTC_del_miercoles_son_martes_23_00_en_Argentina_y_cuenta_el_horario_del_martes()
    {
        var ahora = new DateTimeOffset(2026, 10, 21, 2, 0, 0, TimeSpan.Zero); // miércoles en UTC

        // Martes 19:00-23:30: martes 23:00 está abierto. Si se evaluara en UTC (miércoles, cerrado), daría cerrado.
        var estado = Calcular(Semana(Turnos(Martes, "19:00", "23:30")), ahora);

        Verificar(estado, true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 23, 30));
    }

    [Fact]
    public void Las_02_00_UTC_del_miercoles_no_cuentan_como_miercoles()
    {
        var ahora = new DateTimeOffset(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);

        // Miércoles 24h y martes cerrado: martes 23:00 en Argentina está cerrado. En UTC (miércoles) daría abierto.
        var estado = Calcular(Semana(Dia24h(Miercoles)), ahora);

        Verificar(estado, false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 21, 0, 0));
    }

    [Fact]
    public void Una_zona_al_este_de_UTC_tambien_cambia_de_dia()
    {
        // 2026-10-20 12:00 UTC = miércoles 01:00 en Auckland (UTC+13 por horario de verano).
        var ahora = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

        var estado = Calcular(Semana(Turnos(Miercoles, "00:00", "06:00")), ahora, zona: "Pacific/Auckland");

        Verificar(estado, true, MotivoEstadoLocal.Horario, new DateTimeOffset(2026, 10, 20, 17, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void El_mismo_instante_con_otro_offset_da_el_mismo_resultado()
    {
        var utc = EnArgentina(2026, 10, 20, 13, 59);
        var conOffsetDeArgentina = new DateTimeOffset(2026, 10, 20, 13, 59, 0, TimeSpan.FromHours(-3));
        var conOffsetDeEspana = new DateTimeOffset(2026, 10, 20, 18, 59, 0, TimeSpan.FromHours(2));

        var esperado = Calcular(MartesDosTurnos(), utc);

        Assert.Equal(esperado, Calcular(MartesDosTurnos(), conOffsetDeArgentina));
        Assert.Equal(esperado, Calcular(MartesDosTurnos(), conOffsetDeEspana));
    }

    [Fact]
    public void Zona_invalida_con_horarios_lanza_ArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => Calcular(MartesDosTurnos(), EnArgentina(2026, 10, 20, 12, 0), zona: "No/Existe"));

        Assert.Contains("No/Existe", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Zona_vacia_con_horarios_lanza_ArgumentException(string zona)
        => Assert.Throws<ArgumentException>(() => Calcular(MartesDosTurnos(), EnArgentina(2026, 10, 20, 12, 0), zona: zona));

    [Fact]
    public void La_zona_no_hace_falta_si_hay_cierre_manual_o_no_hay_horarios()
    {
        // Un dato de zona roto no debe tumbar el menú de un local que de todos modos no usa los horarios.
        var ahora = EnArgentina(2026, 10, 20, 12, 0);

        Verificar(Calcular(MartesDosTurnos(), ahora, cerradoManualmente: true, zona: "No/Existe"), false, MotivoEstadoLocal.CerradoManualmente, null);
        Verificar(Calcular(new List<HorarioLocal>(), ahora, zona: "No/Existe"), true, MotivoEstadoLocal.SinHorarios, null);
    }

    // ---------- Bordes que no estaban en la lista ----------

    [Fact]
    public void Un_turno_que_cierra_a_las_00_00_termina_a_la_medianoche_siguiente()
    {
        var horarios = Semana(Turnos(Lunes, "18:00", "00:00"));

        Verificar(Calcular(horarios, EnArgentina(2026, 10, 19, 23, 59)), true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 0, 0));
        Verificar(Calcular(horarios, EnArgentina(2026, 10, 20, 0, 0)), false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 26, 18, 0));
    }

    [Fact]
    public void Abrir_a_las_00_00_es_inclusivo()
        => Verificar(Calcular(Semana(Turnos(Martes, "00:00", "06:00")), EnArgentina(2026, 10, 20, 0, 0)),
            true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 6, 0));

    [Fact]
    public void Un_cierre_que_coincide_con_la_apertura_del_dia_siguiente_no_parte_la_atencion()
    {
        // Lunes 20:00-00:00 y martes 24h seguidos: no hay cierre a la medianoche; recién cierra el miércoles a las 00:00.
        var horarios = Semana(Turnos(Lunes, "20:00", "00:00"), Dia24h(Martes));

        Verificar(Calcular(horarios, EnArgentina(2026, 10, 19, 23, 0)), true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 21, 0, 0));
        Verificar(Calcular(horarios, EnArgentina(2026, 10, 20, 0, 0)), true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 21, 0, 0));
    }

    [Fact]
    public void Un_turno_nocturno_que_se_pisa_con_un_dia_24h_se_fusiona_sin_romper_el_calculo()
    {
        // El validador rechaza esta combinación (el desborde del sábado pisa al domingo 24h), pero si llegara
        // a la base el cálculo debe seguir siendo coherente: abierto sin cortes desde el sábado hasta el lunes 00:00.
        var horarios = Semana(Turnos(Sabado, "20:00", "01:00"), Dia24h(Domingo));

        Verificar(Calcular(horarios, EnArgentina(2026, 10, 25, 0, 30)), true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 26, 0, 0));
    }

    [Fact]
    public void Cuando_el_unico_horario_ya_paso_esta_semana_la_proxima_apertura_es_la_semana_que_viene()
        => Verificar(Calcular(Semana(Turnos(Lunes, "10:00", "14:00")), EnArgentina(2026, 10, 19, 14, 0)),
            false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 26, 10, 0));

    [Fact]
    public void Los_dias_sin_fila_se_consideran_cerrados()
    {
        // Solo hay fila del martes: lunes y miércoles no figuran.
        var horarios = new List<HorarioLocal> { Turnos(Martes, "10:00", "14:00") };

        Verificar(Calcular(horarios, EnArgentina(2026, 10, 19, 12, 0)), false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 10, 0));
        Verificar(Calcular(horarios, EnArgentina(2026, 10, 21, 12, 0)), false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 27, 10, 0));
    }

    [Fact]
    public void Datos_corruptos_nunca_abren_el_local_por_error()
    {
        var horarios = new List<HorarioLocal>
        {
            new() { DiaSemana = Lunes, Modo = "Abierto" },                                          // modo desconocido
            new() { DiaSemana = Martes, Modo = "turnos", Turno1Apertura = Hora("10:00"), Turno1Cierre = Hora("14:00") }, // mayúsculas distintas
            new() { DiaSemana = Miercoles, Modo = ModoHorario.Turnos, Turno1Apertura = Hora("10:00") },                    // turno incompleto
            new() { DiaSemana = Jueves, Modo = ModoHorario.Turnos, Turno1Apertura = Hora("10:00"), Turno1Cierre = Hora("10:00") } // apertura == cierre
        };

        foreach (var dia in new[] { (19, 12), (20, 12), (21, 12), (22, 10) })
            Verificar(Calcular(horarios, EnArgentina(2026, 10, dia.Item1, dia.Item2, 0)), false, MotivoEstadoLocal.Horario, null);
    }

    [Fact]
    public void Dos_filas_del_mismo_dia_se_suman_en_vez_de_pisarse()
    {
        // El validador no deja pasar esto, pero si llegara a la base el cálculo no debe perder un turno.
        var horarios = new List<HorarioLocal> { Turnos(Martes, "10:00", "14:00"), Turnos(Martes, "19:00", "23:00") };

        Verificar(Calcular(horarios, EnArgentina(2026, 10, 20, 12, 0)), true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 14, 0));
        Verificar(Calcular(horarios, EnArgentina(2026, 10, 20, 20, 0)), true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 23, 0));
    }

    // ---------- Horario de verano ----------

    [Fact]
    public void Una_apertura_que_cae_en_el_salto_de_reloj_se_corre_a_la_hora_siguiente()
    {
        // Nueva York, 2026-03-08: a las 02:00 el reloj salta a las 03:00. La apertura de las 02:30 no existe.
        // Convención: se usa el offset estándar, o sea abre a las 03:30 EDT (07:30 UTC), y cierra a las 04:00 EDT.
        var horarios = Semana(Turnos(Domingo, "02:30", "04:00"));
        var zona = "America/New_York";

        Verificar(Calcular(horarios, new DateTimeOffset(2026, 3, 8, 6, 0, 0, TimeSpan.Zero), zona: zona),
            false, MotivoEstadoLocal.Horario, new DateTimeOffset(2026, 3, 8, 7, 30, 0, TimeSpan.Zero));
        Verificar(Calcular(horarios, new DateTimeOffset(2026, 3, 8, 7, 30, 0, TimeSpan.Zero), zona: zona),
            true, MotivoEstadoLocal.Horario, new DateTimeOffset(2026, 3, 8, 8, 0, 0, TimeSpan.Zero));
    }

    // Barrido: el estado cambia exactamente en ProximoCambio y no antes. Si la conversión de horas locales a UTC
    // fuera inconsistente (saltos de reloj, horas repetidas, desbordes nocturnos), esto lo detecta.
    [Theory]
    [InlineData("America/Argentina/Buenos_Aires", 2026, 10, 18)]
    [InlineData("America/New_York", 2026, 3, 5)]   // salto de reloj: domingo 2026-03-08
    [InlineData("America/New_York", 2026, 10, 29)] // hora repetida: domingo 2026-11-01
    public void ProximoCambio_es_el_instante_exacto_en_que_cambia_el_estado(string zona, int anio, int mes, int dia)
    {
        var horarios = Semana(
            Turnos(Sabado, "20:00", "01:00"),
            Turnos(Domingo, "02:30", "04:00", "18:00", "00:00"),
            Turnos(Lunes, "00:00", "06:00"),
            Dia24h(Miercoles),
            Turnos(Jueves, "10:00", "14:00", "19:00", "23:00"));
        Assert.Empty(HorarioLocalValidator.Validar(horarios, zona)); // el barrido solo vale para horarios que el validador aceptaría

        var inicio = new DateTimeOffset(anio, mes, dia, 0, 0, 0, TimeSpan.Zero);
        var cambios = 0;
        for (var paso = 0; paso < 10 * 24 * 12; paso++)
        {
            var ahora = inicio.AddMinutes(paso * 5);
            var estado = Calcular(horarios, ahora, zona: zona);

            if (estado.ProximoCambio is not { } cambio)
                continue;

            Assert.True(cambio > ahora, $"ProximoCambio {cambio:o} no es posterior a {ahora:o}");
            Assert.Equal(estado.EstaAbierto, Calcular(horarios, cambio.AddTicks(-1), zona: zona).EstaAbierto);
            Assert.Equal(!estado.EstaAbierto, Calcular(horarios, cambio, zona: zona).EstaAbierto);
            cambios++;
        }

        Assert.True(cambios > 0);
    }
}

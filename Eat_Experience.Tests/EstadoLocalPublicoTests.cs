using Microsoft.Extensions.Logging;
using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Services.Implementaciones;
using Vinto.Api.Services.Interfaces;
using static Eat_Experience.Tests.Horarios;

namespace Eat_Experience.Tests;

// Decisión de "fallar abierto": un horario que no se puede evaluar nunca tira el menú ni cierra el local.
public class EstadoLocalPublicoTests
{
    private const int AdminId = 42;
    private static readonly DateTimeOffset Ahora = EnArgentina(2026, 10, 20, 12, 0);

    private sealed class CalculadoraQueFalla : IEstadoLocalCalculator
    {
        public EstadoLocal Calcular(IReadOnlyList<HorarioLocal> horarios, bool cerradoManualmente, string zonaHoraria, DateTimeOffset ahoraUtc)
            => throw new InvalidOperationException("falla inesperada del cálculo");
    }

    private static EstadoLocal Calcular(IEstadoLocalCalculator calculadora, IReadOnlyList<HorarioLocal> horarios, string zona, FakeLogger logger, bool cerradoManualmente = false)
        => EstadoLocalPublico.Calcular(calculadora, horarios, cerradoManualmente, zona, Ahora, logger, AdminId);

    // ---------- Zona inválida: abierto + error en el log ----------

    [Theory]
    [InlineData("No/Existe")]
    [InlineData("")]
    [InlineData("   ")]
    public void Con_zona_invalida_y_horarios_el_local_figura_abierto_y_se_loguea_un_error(string zona)
    {
        var logger = new FakeLogger();
        // Un horario que diría "cerrado" (martes cerrado, hoy es martes): si se evaluara, daría cerrado.
        var horarios = Semana(Turnos(Lunes, "10:00", "14:00"));

        var estado = Calcular(new EstadoLocalCalculator(), horarios, zona, logger);

        Assert.True(estado.EstaAbierto);
        Assert.Equal(MotivoEstadoLocal.SinHorarios, estado.Motivo);
        Assert.Null(estado.ProximoCambio);

        var error = Assert.Single(logger.Entradas);
        Assert.Equal(LogLevel.Error, error.Nivel);
        Assert.Contains(AdminId.ToString(), error.Mensaje);
        Assert.Contains("abierto", error.Mensaje);
        Assert.IsType<ArgumentException>(error.Excepcion); // la causa queda en el log, no se traga
    }

    [Fact]
    public void El_error_nombra_la_zona_rota_para_poder_corregirla()
    {
        var logger = new FakeLogger();

        Calcular(new EstadoLocalCalculator(), MartesDosTurnos(), "Marte/Olympus", logger);

        Assert.Contains("Marte/Olympus", Assert.Single(logger.Entradas).Mensaje);
    }

    // ---------- Cualquier otra falla del cálculo también falla abierto ----------

    [Fact]
    public void Una_falla_inesperada_del_calculo_tampoco_tira_el_menu()
    {
        var logger = new FakeLogger();

        var estado = Calcular(new CalculadoraQueFalla(), MartesDosTurnos(), Argentina, logger);

        Assert.True(estado.EstaAbierto);
        Assert.Equal(MotivoEstadoLocal.SinHorarios, estado.Motivo);
        var error = Assert.Single(logger.Entradas);
        Assert.Equal(LogLevel.Error, error.Nivel);
        Assert.IsType<InvalidOperationException>(error.Excepcion);
    }

    // ---------- Lo que NO es fallar abierto ----------

    [Fact]
    public void Con_zona_valida_devuelve_el_resultado_del_calculo_y_no_loguea()
    {
        var logger = new FakeLogger();
        var martes13h = EnArgentina(2026, 10, 20, 13, 59);

        var estado = EstadoLocalPublico.Calcular(new EstadoLocalCalculator(), MartesDosTurnos(), false, Argentina, martes13h, logger, AdminId);

        Assert.True(estado.EstaAbierto);
        Assert.Equal(MotivoEstadoLocal.Horario, estado.Motivo);
        Assert.Equal(EnArgentina(2026, 10, 20, 14, 0), estado.ProximoCambio);
        Assert.Empty(logger.Entradas);
    }

    [Fact]
    public void Un_cierre_real_por_horario_se_respeta_y_no_se_confunde_con_una_falla()
    {
        var logger = new FakeLogger();

        // Martes 12:00 con turnos de 10 a 11 y de 19 a 23 → cerrado hasta las 19:00.
        var estado = Calcular(new EstadoLocalCalculator(), Semana(Turnos(Martes, "10:00", "11:00", "19:00", "23:00")), Argentina, logger);

        Assert.False(estado.EstaAbierto);
        Assert.Equal(EnArgentina(2026, 10, 20, 19, 0), estado.ProximoCambio);
        Assert.Empty(logger.Entradas);
    }

    [Fact]
    public void El_cierre_manual_sigue_cerrando_aunque_la_zona_este_rota()
    {
        // La calculadora no necesita la zona si el dueño cerró a mano: no hay nada que "fallar abierto".
        var logger = new FakeLogger();

        var estado = Calcular(new EstadoLocalCalculator(), MartesDosTurnos(), "No/Existe", logger, cerradoManualmente: true);

        Assert.False(estado.EstaAbierto);
        Assert.Equal(MotivoEstadoLocal.CerradoManualmente, estado.Motivo);
        Assert.Empty(logger.Entradas);
    }

    [Fact]
    public void Sin_horarios_la_zona_rota_es_irrelevante_y_no_se_loguea()
    {
        var logger = new FakeLogger();

        var estado = Calcular(new EstadoLocalCalculator(), new List<HorarioLocal>(), "No/Existe", logger);

        Assert.True(estado.EstaAbierto);
        Assert.Equal(MotivoEstadoLocal.SinHorarios, estado.Motivo);
        Assert.Empty(logger.Entradas);
    }

    private static List<HorarioLocal> MartesDosTurnos() => Semana(Turnos(Martes, "10:00", "14:00", "19:00", "23:00"));
}

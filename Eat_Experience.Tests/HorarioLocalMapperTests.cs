using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using static Eat_Experience.Tests.Horarios;

namespace Eat_Experience.Tests;

public class HorarioLocalMapperTests
{
    // ---------- TryParsearHora ----------

    [Theory]
    [InlineData("19:30", 19, 30)]
    [InlineData("09:05", 9, 5)]
    [InlineData("9:05", 9, 5)]
    [InlineData("00:00", 0, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData("  14:00  ", 14, 0)]
    [InlineData("14:00:00", 14, 0)]
    public void Acepta_horas_validas(string texto, int hora, int minuto)
    {
        Assert.True(HorarioLocalMapper.TryParsearHora(texto, out var resultado));
        Assert.Equal(new TimeOnly(hora, minuto), resultado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Vacio_significa_sin_valor_y_no_es_un_error(string? texto)
    {
        Assert.True(HorarioLocalMapper.TryParsearHora(texto, out var resultado));
        Assert.Null(resultado);
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("25:00")]
    [InlineData("12:60")]
    [InlineData("7pm")]
    [InlineData("19.30")]
    [InlineData("abc")]
    [InlineData("19:30 PM")]
    public void Rechaza_textos_que_no_son_una_hora(string texto)
    {
        Assert.False(HorarioLocalMapper.TryParsearHora(texto, out var resultado));
        Assert.Null(resultado);
    }

    // ---------- Formatear ----------

    [Fact]
    public void Formatea_como_HH_mm_sin_importar_la_cultura()
    {
        var anterior = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            Assert.Equal("09:05", HorarioLocalMapper.Formatear(new TimeOnly(9, 5)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = anterior;
        }
        Assert.Null(HorarioLocalMapper.Formatear(null));
    }

    // ---------- AEntidades / ADto ----------

    [Fact]
    public void Convierte_los_dias_del_DTO_a_entidades()
    {
        var dias = new[]
        {
            new HorarioDiaDTO { DiaSemana = Martes, Modo = "Turnos", Turno1Apertura = "10:00", Turno1Cierre = "14:00", Turno2Apertura = "19:00", Turno2Cierre = "23:00" },
            new HorarioDiaDTO { DiaSemana = Lunes, Modo = "Cerrado" }
        };

        var (horarios, errores) = HorarioLocalMapper.AEntidades(dias);

        Assert.Empty(errores);
        Assert.Equal(2, horarios.Count);
        var martes = horarios[0];
        Assert.Equal(Martes, martes.DiaSemana);
        Assert.Equal(ModoHorario.Turnos, martes.Modo);
        Assert.Equal(Hora("10:00"), martes.Turno1Apertura);
        Assert.Equal(Hora("23:00"), martes.Turno2Cierre);
        Assert.Null(horarios[1].Turno1Apertura);
        Assert.All(horarios, h => Assert.Equal(0, h.AdministradorId)); // el id lo pone quien guarda, nunca el cliente
    }

    [Fact]
    public void Informa_cada_hora_mal_escrita_nombrando_el_dia_y_el_campo()
    {
        var dias = new[]
        {
            new HorarioDiaDTO { DiaSemana = Viernes, Modo = "Turnos", Turno1Apertura = "25:00", Turno1Cierre = "14:00", Turno2Apertura = "19:00", Turno2Cierre = "mediodia" }
        };

        var (_, errores) = HorarioLocalMapper.AEntidades(dias);

        Assert.Equal(2, errores.Count);
        Assert.Contains(errores, e => e.Contains("'25:00'") && e.Contains("la apertura del turno 1") && e.Contains("viernes") && e.Contains("HH:mm"));
        Assert.Contains(errores, e => e.Contains("'mediodia'") && e.Contains("el cierre del turno 2") && e.Contains("viernes"));
    }

    [Fact]
    public void Un_modo_nulo_pasa_como_texto_vacio_para_que_lo_rechace_el_validador()
    {
        var (horarios, errores) = HorarioLocalMapper.AEntidades(new[] { new HorarioDiaDTO { DiaSemana = Lunes, Modo = null! } });

        Assert.Empty(errores);
        Assert.Equal(string.Empty, horarios[0].Modo);
    }

    [Fact]
    public void Un_elemento_nulo_en_la_lista_es_un_error_y_no_una_excepcion()
    {
        var (horarios, errores) = HorarioLocalMapper.AEntidades(new HorarioDiaDTO?[] { null }!);

        Assert.Empty(horarios);
        Assert.Single(errores);
        Assert.Contains("elemento vacío", errores[0]);
    }

    [Fact]
    public void Ida_y_vuelta_entre_entidad_y_DTO_conserva_los_datos()
    {
        var original = Turnos(Sabado, "20:00", "01:00");

        var dto = HorarioLocalMapper.ADto(original);
        var (vuelta, errores) = HorarioLocalMapper.AEntidades(new[] { dto });

        Assert.Empty(errores);
        Assert.Equal("20:00", dto.Turno1Apertura);
        Assert.Equal("01:00", dto.Turno1Cierre);
        Assert.Null(dto.Turno2Apertura);
        Assert.Equal(original.Turno1Apertura, vuelta[0].Turno1Apertura);
        Assert.Equal(original.Turno1Cierre, vuelta[0].Turno1Cierre);
        Assert.Equal(original.Modo, vuelta[0].Modo);
    }
}

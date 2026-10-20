using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;
using static Eat_Experience.Tests.Horarios;

namespace Eat_Experience.Tests;

public class MenuPublicoArmadorTests
{
    private const string BaseUrl = "https://api.vinto.test";

    // Lo que vive en la caché: esActivo=true (la cuenta está habilitada o no se serviría), sin Estado, con el Horario.
    private static MenuPublicoCacheado Entrada(string? logo = null)
    {
        var menu = new MenuPublicoResponseDTO
        {
            Local = new LocalInfoDTO
            {
                NombreLocal = "Carripollo",
                Telefono = "3510000000",
                Direccion = "Calle 1",
                EsActivo = true,
                Horarios = "Lun a vie 10 a 14",
                CostoEnvio = 500m
            },
            Categorias = new List<CategoriaMenuDTO> { new() { Id = 1, Nombre = "Pollos" } },
            Horario = new HorarioMenuDTO
            {
                ZonaHoraria = Argentina,
                Dias = new List<HorarioDiaDTO> { new() { DiaSemana = Martes, Modo = "Turnos", Turno1Apertura = "10:00", Turno1Cierre = "14:00" } }
            }
        };
        return new MenuPublicoCacheado(menu, logo, new HorarioMenuCacheado(7, Argentina, false, new List<HorarioLocal>()));
    }

    private static readonly EstadoLocal Abierto = new(true, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 14, 0));
    private static readonly EstadoLocal Cerrado = new(false, MotivoEstadoLocal.Horario, EnArgentina(2026, 10, 20, 19, 0));

    // ---------- Compatibilidad: local.esActivo = estado.estaAbierto ----------

    [Fact]
    public void Con_el_local_abierto_esActivo_es_true()
    {
        var respuesta = MenuPublicoArmador.Armar(Entrada(), BaseUrl, Abierto);

        Assert.True(respuesta.Local.EsActivo);
        Assert.True(respuesta.Estado!.EstaAbierto);
    }

    [Fact]
    public void Con_el_local_cerrado_esActivo_pasa_a_false_para_que_el_front_viejo_muestre_cerrado()
    {
        var respuesta = MenuPublicoArmador.Armar(Entrada(), BaseUrl, Cerrado);

        Assert.False(respuesta.Local.EsActivo);
        Assert.False(respuesta.Estado!.EstaAbierto);
    }

    [Theory]
    [InlineData(MotivoEstadoLocal.CerradoManualmente)]
    [InlineData(MotivoEstadoLocal.Horario)]
    public void El_cierre_manual_y_el_de_horario_se_ven_igual_en_esActivo(string motivo)
        => Assert.False(MenuPublicoArmador.Armar(Entrada(), BaseUrl, new EstadoLocal(false, motivo, null)).Local.EsActivo);

    // ---------- Estado y horario nuevos ----------

    [Fact]
    public void La_respuesta_trae_el_estado_calculado_y_el_horario_estructurado()
    {
        var entrada = Entrada();

        var respuesta = MenuPublicoArmador.Armar(entrada, BaseUrl, Cerrado);

        Assert.Same(Cerrado, respuesta.Estado);
        Assert.Equal(Cerrado.ProximoCambio, respuesta.Estado!.ProximoCambio);
        Assert.Equal(Argentina, respuesta.Horario!.ZonaHoraria);
        var dia = Assert.Single(respuesta.Horario.Dias);
        Assert.Equal("10:00", dia.Turno1Apertura); // mismo formato "HH:mm" que el GET del panel
    }

    // ---------- Lo cacheado no depende de la hora ----------

    [Fact]
    public void Armar_no_muta_la_entrada_cacheada()
    {
        var entrada = Entrada();

        MenuPublicoArmador.Armar(entrada, BaseUrl, Cerrado);

        Assert.True(entrada.Menu.Local.EsActivo);   // el local cacheado no cambió
        Assert.Null(entrada.Menu.Estado);           // y no se le pegó ningún estado
        Assert.Null(entrada.Menu.Local.LogoImagenUrl);
    }

    [Fact]
    public void La_misma_entrada_cacheada_da_respuestas_distintas_segun_el_estado_del_momento()
    {
        var entrada = Entrada();

        var abierto = MenuPublicoArmador.Armar(entrada, BaseUrl, Abierto);
        var cerrado = MenuPublicoArmador.Armar(entrada, BaseUrl, Cerrado);
        var otraVezAbierto = MenuPublicoArmador.Armar(entrada, BaseUrl, Abierto);

        Assert.True(abierto.Local.EsActivo);
        Assert.False(cerrado.Local.EsActivo);
        Assert.True(otraVezAbierto.Local.EsActivo);
    }

    [Fact]
    public void Lo_que_no_cambia_se_comparte_con_la_entrada_y_el_texto_libre_sigue_igual()
    {
        var entrada = Entrada();

        var respuesta = MenuPublicoArmador.Armar(entrada, BaseUrl, Cerrado);

        Assert.Same(entrada.Menu.Categorias, respuesta.Categorias);
        Assert.Same(entrada.Menu.Horario, respuesta.Horario);
        Assert.Equal("Lun a vie 10 a 14", respuesta.Local.Horarios); // local.horarios (legado) intacto
        Assert.Equal("Carripollo", respuesta.Local.NombreLocal);
        Assert.Equal(500m, respuesta.Local.CostoEnvio);
    }

    // ---------- Logo (comportamiento previo, sin cambios) ----------

    [Fact]
    public void Sin_logo_no_hay_url()
        => Assert.Null(MenuPublicoArmador.Armar(Entrada(null), BaseUrl, Abierto).Local.LogoImagenUrl);

    [Fact]
    public void Un_logo_relativo_se_completa_con_el_host_del_request()
        => Assert.Equal("https://api.vinto.test/uploads/logo.png",
            MenuPublicoArmador.Armar(Entrada("/uploads/logo.png"), BaseUrl, Abierto).Local.LogoImagenUrl);

    [Theory]
    [InlineData("https://acct.blob.core.windows.net/logo.png")]
    [InlineData("http://cdn.ejemplo.com/logo.png")]
    [InlineData("HTTPS://cdn.ejemplo.com/logo.png")]
    public void Un_logo_ya_absoluto_no_lleva_el_host_adelante(string logo)
        => Assert.Equal(logo, MenuPublicoArmador.Armar(Entrada(logo), BaseUrl, Abierto).Local.LogoImagenUrl);
}

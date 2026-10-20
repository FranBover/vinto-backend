<#
.SYNOPSIS
  Verificacion extremo a extremo de horarios, cierre manual, menu publico y pedidos
  contra la API REAL y una base de datos LOCAL.

.DESCRIPTION
  Cubre lo que los tests unitarios no pueden (persistencia, transaccion de reemplazo, indice unico,
  invalidacion del cache, JWT, rate limiting): endpoints del panel (api/Administrador/local/...),
  menu publico (estado, horario, compatibilidad de esActivo, X-Cache), pedido rechazado con 409 cuando el
  local esta cerrado, y los otros flujos publicos (cupon, pago de MP) que NO deben bloquearse.

  Crea admins descartables (email verif-api-*@example.test) con su categoria, producto y pedidos, y los
  BORRA al terminar (tambien al empezar, por si una corrida anterior quedo a medias). Los admins que ya
  existian no se tocan; al final se compara el estado antes/despues.

  SOLO LOCAL: se niega a correr si la URL de la API o el servidor SQL no son locales.

.PARAMETER BaseUrl
  URL de la API local. Por defecto http://localhost:5299.

.PARAMETER SqlServer
  Instancia SQL local donde esta la base que usa la API. Por defecto localhost\SQLEXPRESS (autenticacion Windows).

.PARAMETER Database
  Nombre de la base. Por defecto EatExperienceDb.

.PARAMETER AdminKey
  Clave de registro de admins (header X-Admin-Key). Si no se pasa: variable de entorno VINTO_ADMIN_KEY y, si no
  existe, AdminRegistroKey de Eat_Experience\appsettings.Development.json / appsettings.json (archivos locales, no
  versionados). Nunca se imprime.

.PARAMETER IniciarApi
  Levanta la API desde el build de Debug (requiere `dotnet build Eat_Experience.sln` previo), la apaga al terminar y
  usa su log para verificar mensajes de error. Recomendado: el rate limiting es en memoria, asi cada corrida
  empieza de cero (register 3/15 min, login 5/5 min, pedidos publicos 10/10 min por IP; una corrida usa 2, 4 y 7).

.PARAMETER ApiLog
  Ruta del log (stdout) de una API que ya esta corriendo, para verificar las lineas de error. Opcional.

.PARAMETER SinEspera
  Omite el caso que espera ~1 minuto para ver el estado cambiar de abierto a cerrado con el menu en cache.

.EXAMPLE
  dotnet build Eat_Experience.sln
  powershell -ExecutionPolicy Bypass -File tools\verificar-horarios-local.ps1 -IniciarApi

.NOTES
  Sale con codigo 1 si alguna verificacion falla. Compatible con Windows PowerShell 5.1 (archivo solo ASCII).
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5299',
    [string]$SqlServer = 'localhost\SQLEXPRESS',
    [string]$Database = 'EatExperienceDb',
    [string]$AdminKey,
    [string]$ApiLog,
    [switch]$IniciarApi,
    [switch]$SinEspera
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$script:fallos = 0
$script:pasos = 0
$apiProc = $null

# ---------- Salvaguardas: solo local ----------
$uriBase = [Uri]$BaseUrl
if ($uriBase.Host -notin 'localhost', '127.0.0.1', '[::1]', '::1') { throw "Este script solo corre contra una API local (BaseUrl=$BaseUrl)." }
if ($SqlServer -notmatch '^(localhost|127\.0\.0\.1|\(local\)|\.)(\\|,|$)') { throw "Este script solo corre contra un SQL Server local (SqlServer=$SqlServer)." }
$base = $BaseUrl.TrimEnd('/')

# ---------- Helpers ----------
function U([string]$s) { [regex]::Unescape($s) }   # el archivo es ASCII: los acentos van como \uXXXX

function Sql([string]$q) {
    (sqlcmd -S $SqlServer -d $Database -E -C -h -1 -W -Q "SET NOCOUNT ON; $q" 2>&1 | Out-String).Trim()
}

# PowerShell 5.1 manda los cuerpos de texto en ISO-8859-1: se envian bytes UTF-8 explicitos.
function Call([string]$m, [string]$p, $body = $null, [string]$tok = $null, [string]$rawJson = $null) {
    $h = @{}; if ($tok) { $h['Authorization'] = "Bearer $tok" }
    $a = @{ Uri = "$base$p"; Method = $m; UseBasicParsing = $true; Headers = $h; TimeoutSec = 30 }
    if ($rawJson) { $a.Body = [Text.Encoding]::UTF8.GetBytes($rawJson); $a.ContentType = 'application/json; charset=utf-8' }
    elseif ($null -ne $body) { $a.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 8 -Compress)); $a.ContentType = 'application/json; charset=utf-8' }
    try { $r = Invoke-WebRequest @a; $st = [int]$r.StatusCode; $c = $r.Content; $hd = $r.Headers }
    catch [System.Net.WebException] {
        $resp = $_.Exception.Response; if (-not $resp) { throw }
        $st = [int]$resp.StatusCode; $c = (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd(); $hd = $resp.Headers
    }
    $j = $null; if ($c) { try { $j = $c | ConvertFrom-Json } catch {} }
    [pscustomobject]@{ Status = $st; Body = $c; Json = $j; Headers = $hd }
}

function Check([string]$desc, [bool]$ok, [string]$detalle = '') {
    $script:pasos++
    if ($ok) { "  OK    $desc" } else { $script:fallos++; "  FALLA $desc   $detalle" }
}

function Dia($d, $modo, $a1 = $null, $c1 = $null, $a2 = $null, $c2 = $null) {
    @{ diaSemana = $d; modo = $modo; turno1Apertura = $a1; turno1Cierre = $c1; turno2Apertura = $a2; turno2Cierre = $c2 }
}

function SemanaCerrada { @(0..6 | ForEach-Object { Dia $_ 'Cerrado' }) }

# Argentina = UTC-3 sin horario de verano. Devuelve el dia de la semana y un turno que cubre "ahora" (usado con el reloj real).
function AhoraAr { [DateTime]::UtcNow.AddHours(-3) }
function SemanaAbiertaAhora([double]$horasHastaCierre) {
    $ar = AhoraAr
    $desde = if ($ar.Hour -lt 1) { '00:00' } else { $ar.AddHours(-1).ToString('HH:mm') }   # cerca de medianoche no se cruza al dia anterior
    $hasta = $ar.AddHours($horasHastaCierre).ToString('HH:mm')
    @(0..6 | ForEach-Object { if ($_ -eq [int]$ar.DayOfWeek) { Dia $_ 'Turnos' $desde $hasta } else { Dia $_ 'Cerrado' } })
}

function PutHorarios($semana, [string]$zona = 'America/Argentina/Buenos_Aires') {
    Call 'PUT' '/api/Administrador/local/horarios' @{ zonaHoraria = $zona; dias = $semana } -tok $script:tok
}
function Menu { Call 'GET' "/api/public/locales/$($script:slug)/menu" }
function Cierre([bool]$cerrado) { Call 'PATCH' '/api/Administrador/local/cierre-manual' @{ cerrado = $cerrado } -tok $script:tok }
function NuevoPedido {
    Call 'POST' "/api/public/locales/$($script:slug)/pedidos" @{
        nombreCliente = 'Verificacion'; telefonoCliente = '3510000000'; formaPago = 'Efectivo'; formaEntrega = 'Retira'
        montoPagoEfectivo = 1000; detalles = @(@{ productoId = $script:prodId; cantidad = 1 })
    }
}
function CantPedidos { [int](Sql "SELECT COUNT(*) FROM Pedidos WHERE AdministradorId=$($script:adminId)") }
function CantMovimientos { [int](Sql "SELECT COUNT(*) FROM MovimientosStock WHERE AdministradorId=$($script:adminId)") }

$patronEmail = 'verif-api-%@example.test'
function Limpiar {
    # Solo los descartables de este script (patron de email). Orden por las FK Restrict; el resto cae en cascada.
    $ids = "SELECT Id FROM Administradores WHERE Email LIKE '$patronEmail'"
    $null = Sql "DELETE FROM MovimientosStock WHERE AdministradorId IN ($ids)"
    $null = Sql "DELETE FROM Pedidos WHERE AdministradorId IN ($ids)"
    $null = Sql "DELETE FROM Productos WHERE AdministradorId IN ($ids)"
    $null = Sql "DELETE FROM Categorias WHERE AdministradorId IN ($ids)"
    $null = Sql "DELETE FROM Administradores WHERE Email LIKE '$patronEmail'"
}

# ---------- Clave de registro (nunca se imprime) ----------
if (-not $AdminKey) { $AdminKey = $env:VINTO_ADMIN_KEY }
if (-not $AdminKey) {
    foreach ($f in 'appsettings.Development.json', 'appsettings.json') {
        $ruta = Join-Path $repo "Eat_Experience\$f"
        if (-not $AdminKey -and (Test-Path $ruta)) { $AdminKey = (Get-Content $ruta -Raw | ConvertFrom-Json).AdminRegistroKey }
    }
}
if (-not $AdminKey) { throw 'No hay clave de registro: pasar -AdminKey, definir VINTO_ADMIN_KEY o tener AdminRegistroKey en los appsettings locales.' }

# ---------- API propia (opcional) ----------
if ($IniciarApi) {
    $dll = Join-Path $repo 'Eat_Experience\bin\Debug\net9.0\Vinto.Api.dll'
    if (-not (Test-Path $dll)) { throw "No esta el build ($dll). Correr antes: dotnet build Eat_Experience.sln" }
    $salida = Join-Path $env:TEMP 'vinto-verificacion-api.out.log'
    $errores = Join-Path $env:TEMP 'vinto-verificacion-api.err.log'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'; $env:ASPNETCORE_URLS = $base
    $apiProc = Start-Process -FilePath 'dotnet' -ArgumentList "`"$dll`"" -WorkingDirectory (Join-Path $repo 'Eat_Experience') `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput $salida -RedirectStandardError $errores
    $ApiLog = $salida
    $lista = $false
    for ($i = 0; $i -lt 60 -and -not $lista; $i++) {
        Start-Sleep -Milliseconds 750
        if ($apiProc.HasExited) { throw "La API termino al arrancar (exit $($apiProc.ExitCode)). Ver $errores" }
        try { $lista = (Invoke-WebRequest -UseBasicParsing "$base/swagger/v1/swagger.json" -TimeoutSec 3).StatusCode -eq 200 } catch {}
    }
    if (-not $lista) { throw 'La API no respondio a tiempo.' }
    "API iniciada para la verificacion (PID $($apiProc.Id)) en $base"
}
else {
    try { $null = Invoke-WebRequest -UseBasicParsing "$base/swagger/v1/swagger.json" -TimeoutSec 5 }
    catch { throw "No hay una API respondiendo en $base (Development). Usar -IniciarApi o levantarla a mano." }
}

Limpiar
$mail = "verif-api-$([guid]::NewGuid().ToString('N').Substring(0,8))@example.test"
$adminId = $null; $prodId = $null
$antes = Sql "SELECT STRING_AGG(CONCAT(Id,':',EsActivo,':',CerradoManualmente,':',ZonaHoraria), ' | ') WITHIN GROUP (ORDER BY Id) FROM Administradores"
$antesHor = Sql "SELECT COUNT(*) FROM HorariosLocal"
"Estado inicial local -> admins: $antes ; filas HorariosLocal: $antesHor"

try {
    # ================= 0. Superficie de Administrador =================
    "`n[0] Rutas de Administrador en swagger.json (build actual)"
    $sw = (Invoke-WebRequest -UseBasicParsing "$base/swagger/v1/swagger.json").Content | ConvertFrom-Json
    foreach ($ruta in ($sw.paths.PSObject.Properties | Where-Object { $_.Name -like '*Administrador*' })) {
        "    $($ruta.Name)  [" + (($ruta.Value.PSObject.Properties.Name | ForEach-Object { $_.ToUpper() }) -join ',') + "]"
    }

    # ================= 1. Alta de un admin descartable =================
    "`n[1] Registro + login de un admin descartable"
    $pwd = 'Test-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
    $regBody = @{ nombre = 'Verificacion'; email = $mail; password = $pwd; nombreLocal = "Verif Horarios $([guid]::NewGuid().ToString('N').Substring(0,6))"; telefono = '3510000000'; direccion = 'Test 123' } | ConvertTo-Json -Compress
    $reg = Invoke-WebRequest -UseBasicParsing -Uri "$base/api/Auth/register" -Method Post -Body $regBody -ContentType 'application/json' -Headers @{ 'X-Admin-Key' = $AdminKey }
    $regJson = $reg.Content | ConvertFrom-Json
    $adminId = [int]$regJson.id; $slug = $regJson.slugLocal
    Check "registro 201 (id=$adminId, slug=$slug)" ($reg.StatusCode -eq 201)
    # La clave se manda como \u00f1 para no depender de la codificacion del archivo.
    $loginBody = '{"email":"' + $mail + '","contrase\u00f1a":"' + $pwd + '"}'
    $login = Call 'POST' '/api/Auth/login' -rawJson $loginBody
    $tok = $login.Json.token
    Check 'login devuelve token' ([bool]$tok)

    # ================= 2. Auth =================
    "`n[2] Autenticacion"
    Check 'GET horarios sin token -> 401' ((Call 'GET' '/api/Administrador/local/horarios').Status -eq 401)
    Check 'PUT horarios sin token -> 401' ((Call 'PUT' '/api/Administrador/local/horarios' @{ zonaHoraria = 'UTC'; dias = @() }).Status -eq 401)
    Check 'PATCH cierre-manual sin token -> 401' ((Call 'PATCH' '/api/Administrador/local/cierre-manual' @{ cerrado = $true }).Status -eq 401)

    # ================= 3. GET inicial =================
    "`n[3] GET inicial (sin horarios)"
    $g = Call 'GET' '/api/Administrador/local/horarios' -tok $tok
    Check 'GET -> 200' ($g.Status -eq 200) $g.Body
    Check 'zona por defecto Buenos Aires' ($g.Json.zonaHoraria -eq 'America/Argentina/Buenos_Aires')
    Check 'cerradoManualmente=false y dias vacio' (($g.Json.cerradoManualmente -eq $false) -and (@($g.Json.dias).Count -eq 0))
    Check 'estado: abierto, SinHorarios, proximoCambio null' (($g.Json.estado.estaAbierto -eq $true) -and ($g.Json.estado.motivo -eq 'SinHorarios') -and ($null -eq $g.Json.estado.proximoCambio)) $g.Body

    # ================= 4. Cache del menu =================
    "`n[4] Cache del menu publico (X-Cache)"
    $m1 = Menu; $m2 = Menu
    Check "menu 200 y MISS->HIT ($($m1.Headers['X-Cache'])->$($m2.Headers['X-Cache']))" (($m1.Status -eq 200) -and ($m1.Headers['X-Cache'] -eq 'MISS') -and ($m2.Headers['X-Cache'] -eq 'HIT'))

    # ================= 5. PUT valido =================
    "`n[5] PUT semana A (valida)"
    $t = @('10:00', '14:00', '19:00', '23:00')
    $semanaA = @((Dia 0 'Cerrado'), (Dia 1 'Turnos' $t[0] $t[1] $t[2] $t[3]), (Dia 2 'Turnos' $t[0] $t[1] $t[2] $t[3]), (Dia 3 'Turnos' $t[0] $t[1] $t[2] $t[3]),
        (Dia 4 'Turnos' $t[0] $t[1] $t[2] $t[3]), (Dia 5 'Turnos' $t[0] $t[1] $t[2] $t[3]), (Dia 6 'Turnos' '20:00' '01:00'))
    $p = PutHorarios $semanaA
    Check 'PUT -> 200' ($p.Status -eq 200) $p.Body
    Check '7 dias devueltos, ordenados por diaSemana' ((@($p.Json.dias).Count -eq 7) -and ((@($p.Json.dias | ForEach-Object { $_.diaSemana }) -join ',') -eq '0,1,2,3,4,5,6'))
    Check 'horas como HH:mm' (($p.Json.dias[6].turno1Apertura -eq '20:00') -and ($p.Json.dias[6].turno1Cierre -eq '01:00') -and ($null -eq $p.Json.dias[6].turno2Apertura))
    Check 'estado calculado con motivo Horario' ($p.Json.estado.motivo -eq 'Horario')
    Check 'BD: 7 filas del admin' ((Sql "SELECT COUNT(*) FROM HorariosLocal WHERE AdministradorId=$adminId") -eq '7')
    $m3 = Menu; $m4 = Menu
    Check "PUT invalida el menu: MISS->HIT ($($m3.Headers['X-Cache'])->$($m4.Headers['X-Cache']))" (($m3.Headers['X-Cache'] -eq 'MISS') -and ($m4.Headers['X-Cache'] -eq 'HIT'))

    # ================= 6. Reemplazo =================
    "`n[6] PUT semana B (reemplaza la misma clave admin+dia, otro orden)"
    $semanaB = @((Dia 6 'Cerrado'), (Dia 5 '24h'), (Dia 4 '24h'), (Dia 3 '24h'), (Dia 2 '24h'), (Dia 1 '24h'), (Dia 0 'Cerrado'))
    $r = PutHorarios $semanaB 'America/New_York'
    Check 'PUT reemplazo -> 200' ($r.Status -eq 200) $r.Body
    Check 'BD: sigue habiendo 7 filas (no 14)' ((Sql "SELECT COUNT(*) FROM HorariosLocal WHERE AdministradorId=$adminId") -eq '7')
    Check 'BD: modos de la semana B' ((Sql "SELECT STRING_AGG(CONCAT(DiaSemana,'=',Modo), ',') WITHIN GROUP (ORDER BY DiaSemana) FROM HorariosLocal WHERE AdministradorId=$adminId") -eq '0=Cerrado,1=24h,2=24h,3=24h,4=24h,5=24h,6=Cerrado')
    Check 'BD: zona actualizada en la misma operacion' ((Sql "SELECT ZonaHoraria FROM Administradores WHERE Id=$adminId") -eq 'America/New_York')
    Check 'turnos viejos no quedaron' ((Sql "SELECT COUNT(*) FROM HorariosLocal WHERE AdministradorId=$adminId AND (Turno1Apertura IS NOT NULL OR Turno2Apertura IS NOT NULL)") -eq '0')

    # ================= 7. Validacion =================
    "`n[7] PUT invalidos -> 400 con errores, BD intacta"
    $estadoBD = Sql "SELECT STRING_AGG(CONCAT(DiaSemana,Modo), ',') WITHIN GROUP (ORDER BY DiaSemana) FROM HorariosLocal WHERE AdministradorId=$adminId"
    $malos = [ordered]@{
        '3 dias (cantidad)'           = @{ zonaHoraria = 'UTC'; dias = @((Dia 0 'Cerrado'), (Dia 1 'Cerrado'), (Dia 2 'Cerrado')) }
        'zona inexistente'            = @{ zonaHoraria = 'Marte/Olympus'; dias = $semanaA }
        'zona ausente'                = @{ dias = $semanaA }
        'turno 2 antes del cierre 1'  = @{ zonaHoraria = 'UTC'; dias = @((Dia 0 'Cerrado'), (Dia 1 'Turnos' '10:00' '14:00' '13:00' '18:00'), (Dia 2 'Cerrado'), (Dia 3 'Cerrado'), (Dia 4 'Cerrado'), (Dia 5 'Cerrado'), (Dia 6 'Cerrado')) }
        'nocturno pisa dia siguiente' = @{ zonaHoraria = 'UTC'; dias = @((Dia 0 'Turnos' '00:30' '03:00'), (Dia 1 'Cerrado'), (Dia 2 'Cerrado'), (Dia 3 'Cerrado'), (Dia 4 'Cerrado'), (Dia 5 'Cerrado'), (Dia 6 'Turnos' '20:00' '01:00')) }
        'modo invalido'               = @{ zonaHoraria = 'UTC'; dias = @((Dia 0 'Abierto'), (Dia 1 'Cerrado'), (Dia 2 'Cerrado'), (Dia 3 'Cerrado'), (Dia 4 'Cerrado'), (Dia 5 'Cerrado'), (Dia 6 'Cerrado')) }
        'dia repetido'                = @{ zonaHoraria = 'UTC'; dias = @((Dia 1 'Cerrado'), (Dia 1 'Cerrado'), (Dia 2 'Cerrado'), (Dia 3 'Cerrado'), (Dia 4 'Cerrado'), (Dia 5 'Cerrado'), (Dia 6 'Cerrado')) }
    }
    $mensajeInvalido = U 'Los horarios no son v\u00e1lidos.'
    foreach ($k in $malos.Keys) {
        $x = Call 'PUT' '/api/Administrador/local/horarios' $malos[$k] -tok $tok
        $okForma = ($x.Status -eq 400) -and ($x.Json.mensaje -eq $mensajeInvalido) -and (@($x.Json.errores).Count -ge 1)
        Check "$k -> 400 + errores" $okForma $x.Body
    }
    $raw1 = Call 'PUT' '/api/Administrador/local/horarios' -tok $tok -rawJson '{"zonaHoraria":"UTC","dias":[{"diaSemana":1,"modo":"Turnos","turno1Apertura":"25:00","turno1Cierre":"14:00"}]}'
    Check 'hora "25:00" -> 400 con mensaje de formato' (($raw1.Status -eq 400) -and ($raw1.Body -match 'HH:mm')) $raw1.Body
    $raw2 = Call 'PUT' '/api/Administrador/local/horarios' -tok $tok -rawJson '{"zonaHoraria":"UTC"}'
    Check 'sin "dias" -> 400 (no se interpreta como vaciar)' (($raw2.Status -eq 400) -and ($raw2.Body -match 'lista de d.as')) $raw2.Body
    $raw3 = Call 'PUT' '/api/Administrador/local/horarios' -tok $tok -rawJson '{"zonaHoraria":"UTC","dias":[null]}'
    Check '"dias":[null] -> 400 (no 500)' ($raw3.Status -eq 400) $raw3.Body
    Check 'JSON invalido -> 400' ((Call 'PUT' '/api/Administrador/local/horarios' -tok $tok -rawJson 'esto no es json').Status -eq 400)
    Check 'BD intacta tras todos los rechazos' ((Sql "SELECT STRING_AGG(CONCAT(DiaSemana,Modo), ',') WITHIN GROUP (ORDER BY DiaSemana) FROM HorariosLocal WHERE AdministradorId=$adminId") -eq $estadoBD)
    Check 'zona intacta tras los rechazos' ((Sql "SELECT ZonaHoraria FROM Administradores WHERE Id=$adminId") -eq 'America/New_York')

    # ================= 8. Tenant desde el JWT =================
    "`n[8] El tenant sale del JWT (el body no puede apuntar a otro admin)"
    $otro = Sql "SELECT TOP 1 Id FROM Administradores WHERE Email NOT LIKE '$patronEmail' ORDER BY Id"
    $t8 = Call 'PUT' '/api/Administrador/local/horarios' @{ zonaHoraria = 'UTC'; administradorId = [int]$otro; adminId = [int]$otro; dias = $semanaA } -tok $tok
    Check "PUT con administradorId=$otro en el body -> 200" ($t8.Status -eq 200) $t8.Body
    Check "el otro admin ($otro) no recibio horarios" ((Sql "SELECT COUNT(*) FROM HorariosLocal WHERE AdministradorId=$otro") -eq '0')
    Check 'el admin del token si (7 filas)' ((Sql "SELECT COUNT(*) FROM HorariosLocal WHERE AdministradorId=$adminId") -eq '7')

    # ================= 9. Reloj real =================
    "`n[9] Estado con el reloj real (TimeProvider)"
    $a = PutHorarios (SemanaAbiertaAhora 2)
    $cierreReal = [DateTimeOffset]$a.Json.estado.proximoCambio
    $cierreEsperado = [DateTimeOffset]::UtcNow.AddHours(2)
    Check 'abierto ahora (turno alrededor de la hora actual)' (($a.Status -eq 200) -and ($a.Json.estado.estaAbierto -eq $true) -and ($a.Json.estado.motivo -eq 'Horario')) $a.Body
    Check "proximoCambio ~ ahora+2h (diff $([math]::Round(([math]::Abs(($cierreReal - $cierreEsperado).TotalMinutes)),1)) min)" ([math]::Abs(($cierreReal - $cierreEsperado).TotalMinutes) -lt 2)
    Check 'proximoCambio viaja en UTC (offset +00:00)' ($a.Body -match '"proximoCambio":"[^"]*(Z|\+00:00)"')
    $c = PutHorarios (SemanaCerrada)
    Check 'todo Cerrado -> cerrado por Horario, proximoCambio null' (($c.Json.estado.estaAbierto -eq $false) -and ($c.Json.estado.motivo -eq 'Horario') -and ($null -eq $c.Json.estado.proximoCambio)) $c.Body

    # ================= 10. Vaciar =================
    "`n[10] PUT con lista vacia"
    $v = PutHorarios @()
    Check 'PUT dias=[] -> 200, SinHorarios' (($v.Status -eq 200) -and ($v.Json.estado.motivo -eq 'SinHorarios') -and ($v.Json.estado.estaAbierto -eq $true)) $v.Body
    Check 'BD: 0 filas del admin' ((Sql "SELECT COUNT(*) FROM HorariosLocal WHERE AdministradorId=$adminId") -eq '0')

    # ================= 11. Cierre manual =================
    "`n[11] PATCH cierre manual"
    $mm1 = Menu; $mm2 = Menu
    $pc = Cierre $true
    Check 'cerrado=true -> 200, CerradoManualmente, proximoCambio null' (($pc.Status -eq 200) -and ($pc.Json.estaAbierto -eq $false) -and ($pc.Json.motivo -eq 'CerradoManualmente') -and ($null -eq $pc.Json.proximoCambio)) $pc.Body
    Check 'BD: CerradoManualmente=1' ((Sql "SELECT CerradoManualmente FROM Administradores WHERE Id=$adminId") -eq '1')
    $mm3 = Menu
    Check "PATCH invalida el menu (antes $($mm2.Headers['X-Cache']), despues $($mm3.Headers['X-Cache']))" (($mm2.Headers['X-Cache'] -eq 'HIT') -and ($mm3.Headers['X-Cache'] -eq 'MISS'))
    $gc = Call 'GET' '/api/Administrador/local/horarios' -tok $tok
    Check 'GET refleja cerradoManualmente=true' (($gc.Json.cerradoManualmente -eq $true) -and ($gc.Json.estado.motivo -eq 'CerradoManualmente'))
    Check 'EsActivo no se toca (sigue 1)' ((Sql "SELECT CAST(EsActivo AS int) FROM Administradores WHERE Id=$adminId") -eq '1')
    $pc2 = Cierre $false
    Check 'cerrado=false -> reabre (SinHorarios)' (($pc2.Status -eq 200) -and ($pc2.Json.estaAbierto -eq $true) -and ($pc2.Json.motivo -eq 'SinHorarios')) $pc2.Body
    Check '{} -> 400 (faltante no equivale a false)' ((Call 'PATCH' '/api/Administrador/local/cierre-manual' -tok $tok -rawJson '{}').Status -eq 400)
    Check '{"cerrado":"si"} -> 400' ((Call 'PATCH' '/api/Administrador/local/cierre-manual' -tok $tok -rawJson '{"cerrado":"si"}').Status -eq 400)
    Check 'BD: sigue reabierto tras los 400' ((Sql "SELECT CerradoManualmente FROM Administradores WHERE Id=$adminId") -eq '0')

    # ================= 12. EsActivo ya no se escribe desde el panel =================
    "`n[12] PATCH api/Administrador/{id}/local con esActivo=false"
    $pl = Call 'PATCH' "/api/Administrador/$adminId/local" -tok $tok -rawJson '{"esActivo":false,"nombreLocal":"Verif Renombrado","horarios":"Lun a vie 10 a 14"}'
    Check 'PATCH -> 200 (el campo desconocido se ignora)' ($pl.Status -eq 200) $pl.Body
    Check 'EsActivo sigue en 1' ((Sql "SELECT CAST(EsActivo AS int) FROM Administradores WHERE Id=$adminId") -eq '1')
    Check 'el resto del PATCH si se aplico (nombreLocal)' ((Sql "SELECT NombreLocal FROM Administradores WHERE Id=$adminId") -eq 'Verif Renombrado')
    Check 'respuesta sigue informando esActivo (solo lectura)' ($pl.Json.esActivo -eq $true)
    Check 'PATCH local no pisa zona/cierre manual' (((Sql "SELECT ZonaHoraria FROM Administradores WHERE Id=$adminId") -eq 'America/Argentina/Buenos_Aires') -and ((Sql "SELECT CerradoManualmente FROM Administradores WHERE Id=$adminId") -eq '0'))
    Check 'login sigue funcionando' ((Call 'POST' '/api/Auth/login' -rawJson $loginBody).Status -eq 200)

    # ================= 13. Auditoria =================
    "`n[13] Auditoria: que puede hacer un admin sobre otro Administrador (token del admin $adminId, objetivo admin $otro)"
    $fotoOtro = Sql "SELECT CONCAT(EsActivo,':',NombreLocal,':',Telefono,':',ZonaHoraria,':',CerradoManualmente,':',LEN(PasswordHash)) FROM Administradores WHERE Id=$otro"
    $cuantos = Sql "SELECT COUNT(*) FROM Administradores"
    $aud = @(@('GET', "/api/Administrador/$otro"), @('PATCH', "/api/Administrador/$otro/local"), @('GET', '/api/Administrador'), @('POST', '/api/Administrador'),
        @('PUT', "/api/Administrador/$otro"), @('DELETE', "/api/Administrador/$otro"), @('PUT', "/api/Administrador/$adminId"), @('DELETE', "/api/Administrador/$adminId"), @('GET', "/api/Administrador/$adminId"))
    foreach ($e in $aud) {
        $x = if ($e[0] -in 'PATCH', 'POST', 'PUT') { Call $e[0] $e[1] -tok $tok -rawJson '{"esActivo":false,"nombreLocal":"hackeado"}' } else { Call $e[0] $e[1] -tok $tok }
        "    {0,-7} {1,-34} -> {2}" -f $e[0], $e[1], $x.Status
    }
    Check "el admin $otro quedo exactamente igual tras esos intentos" ((Sql "SELECT CONCAT(EsActivo,':',NombreLocal,':',Telefono,':',ZonaHoraria,':',CerradoManualmente,':',LEN(PasswordHash)) FROM Administradores WHERE Id=$otro") -eq $fotoOtro)
    Check 'ningun admin fue creado ni borrado por esos intentos' ((Sql "SELECT COUNT(*) FROM Administradores") -eq $cuantos)

    # ================= 14. Aislamiento entre tenants =================
    "`n[14] Aislamiento entre dos admins descartables (A y B)"
    $mailB = "verif-api-$([guid]::NewGuid().ToString('N').Substring(0,8))@example.test"
    $regB = @{ nombre = 'Verificacion B'; email = $mailB; password = $pwd; nombreLocal = "Verif B $([guid]::NewGuid().ToString('N').Substring(0,6))"; telefono = '3510000001'; direccion = 'Test 456' } | ConvertTo-Json -Compress
    $null = Invoke-WebRequest -UseBasicParsing -Uri "$base/api/Auth/register" -Method Post -Body $regB -ContentType 'application/json' -Headers @{ 'X-Admin-Key' = $AdminKey }
    $tokB = (Call 'POST' '/api/Auth/login' -rawJson ('{"email":"' + $mailB + '","contrase\u00f1a":"' + $pwd + '"}')).Json.token
    $null = PutHorarios $semanaA 'America/Argentina/Buenos_Aires'
    $gB = Call 'GET' '/api/Administrador/local/horarios' -tok $tokB
    Check 'B no ve los horarios de A (dias vacio, SinHorarios)' (($gB.Status -eq 200) -and (@($gB.Json.dias).Count -eq 0) -and ($gB.Json.estado.motivo -eq 'SinHorarios')) $gB.Body
    $null = Call 'PUT' '/api/Administrador/local/horarios' @{ zonaHoraria = 'UTC'; dias = $semanaB } -tok $tokB
    $null = Call 'PATCH' '/api/Administrador/local/cierre-manual' @{ cerrado = $true } -tok $tokB
    $gA = Call 'GET' '/api/Administrador/local/horarios' -tok $tok
    Check 'A conserva su semana (7 dias, sabado 20:00-01:00) tras los cambios de B' ((@($gA.Json.dias).Count -eq 7) -and ($gA.Json.dias[6].turno1Apertura -eq '20:00')) $gA.Body
    Check 'A conserva su zona y no quedo cerrado por el cierre de B' (($gA.Json.zonaHoraria -eq 'America/Argentina/Buenos_Aires') -and ($gA.Json.cerradoManualmente -eq $false))
    $gB2 = Call 'GET' '/api/Administrador/local/horarios' -tok $tokB
    Check 'B tiene lo suyo (UTC, cerrado manual, semana B)' (($gB2.Json.zonaHoraria -eq 'UTC') -and ($gB2.Json.cerradoManualmente -eq $true) -and ($gB2.Json.dias[1].modo -eq '24h'))

    # ======================================================================
    #  PASO 2d: menu publico y pedidos
    # ======================================================================

    # ================= 15. Menu publico: estado, horario y compatibilidad de esActivo =================
    "`n[15] Menu publico: estado, horario y compatibilidad de local.esActivo"
    $cat = Call 'POST' '/api/Categorias' @{ nombre = 'Verif Cat' } -tok $tok
    $prod = Call 'POST' '/api/Productos' @{ nombre = 'Verif Prod'; descripcion = 'x'; precio = 500; imagenUrl = 'https://example.test/x.png'; disponible = $true; categoriaId = $cat.Json.id } -tok $tok
    $prodId = $prod.Json.id
    Check "categoria y producto de prueba creados (producto $prodId)" (($cat.Status -in 200, 201) -and ($prod.Status -in 200, 201) -and $prodId) "$($cat.Body) $($prod.Body)"

    $null = PutHorarios @()
    $mA = Menu
    Check 'sin horarios: 200, estado abierto/SinHorarios, local.esActivo=true' (($mA.Status -eq 200) -and ($mA.Json.estado.estaAbierto -eq $true) -and ($mA.Json.estado.motivo -eq 'SinHorarios') -and ($mA.Json.local.esActivo -eq $true)) $mA.Body
    Check 'sin horarios: horario.zonaHoraria presente y dias vacio' (($mA.Json.horario.zonaHoraria -eq 'America/Argentina/Buenos_Aires') -and (@($mA.Json.horario.dias).Count -eq 0))
    Check 'el menu trae sus categorias y productos' ((@($mA.Json.categorias).Count -eq 1) -and (@($mA.Json.categorias[0].productos).Count -eq 1))
    Check 'local.horarios (texto libre, legado) se devuelve tal cual' ($mA.Json.local.horarios -eq 'Lun a vie 10 a 14')

    $null = PutHorarios (SemanaCerrada)
    $mB = Menu
    Check 'todo Cerrado: el menu NO da 404, sigue siendo 200' ($mB.Status -eq 200) $mB.Body
    Check 'todo Cerrado: estado cerrado/Horario/proximoCambio null' (($mB.Json.estado.estaAbierto -eq $false) -and ($mB.Json.estado.motivo -eq 'Horario') -and ($null -eq $mB.Json.estado.proximoCambio)) $mB.Body
    Check 'COMPAT: local.esActivo = estado.estaAbierto = false' (($mB.Json.local.esActivo -eq $false) -and ($mB.Json.local.esActivo -eq $mB.Json.estado.estaAbierto))
    Check 'todo Cerrado: categorias y horario (7 dias) siguen en la respuesta' ((@($mB.Json.categorias).Count -eq 1) -and (@($mB.Json.horario.dias).Count -eq 7))
    Check 'local.horarios (texto) intacto tambien cerrado' ($mB.Json.local.horarios -eq 'Lun a vie 10 a 14')

    $null = PutHorarios (SemanaAbiertaAhora 2)
    $mC = Menu
    $cierreMenu = [DateTimeOffset]$mC.Json.estado.proximoCambio
    Check 'turno que cubre ahora: abierto/Horario y local.esActivo=true' (($mC.Json.estado.estaAbierto -eq $true) -and ($mC.Json.estado.motivo -eq 'Horario') -and ($mC.Json.local.esActivo -eq $true)) $mC.Body
    Check 'estado.proximoCambio ~ ahora+2h (con reloj real)' ([math]::Abs(($cierreMenu - [DateTimeOffset]::UtcNow.AddHours(2)).TotalMinutes) -lt 2)
    $hoyIdx = [int](AhoraAr).DayOfWeek
    Check 'horario.dias trae el turno de hoy en HH:mm' ($mC.Json.horario.dias[$hoyIdx].turno1Apertura -match '^\d\d:\d\d$')

    $pcm = Cierre $true
    $mD = Menu
    Check 'cierre manual: se ve al toque (MISS) como CerradoManualmente' (($mD.Headers['X-Cache'] -eq 'MISS') -and ($mD.Json.estado.estaAbierto -eq $false) -and ($mD.Json.estado.motivo -eq 'CerradoManualmente') -and ($null -eq $mD.Json.estado.proximoCambio)) $mD.Body
    Check 'cierre manual: COMPAT local.esActivo=false y el horario sigue publicado' (($mD.Json.local.esActivo -eq $false) -and (@($mD.Json.horario.dias).Count -eq 7))
    $null = Cierre $false
    $mE = Menu
    Check 'se reabre: abierto de nuevo y local.esActivo=true' (($mE.Json.estado.estaAbierto -eq $true) -and ($mE.Json.local.esActivo -eq $true)) $mE.Body

    # ================= 16. El estado cambia bajo el cache =================
    "`n[16] El estado se calcula en cada request: cambia con el menu en cache (HIT)"
    if ($SinEspera) { "    (omitido por -SinEspera)" }
    else {
        # Se espera a que el segundo actual quede entre 20 y 38: el proximo cambio de minuto queda a 22-40 s
        # y la entrada del cache (TTL 60 s) todavia esta viva cuando se vuelve a pedir.
        do { $seg = (Get-Date).Second; if ($seg -lt 20 -or $seg -gt 38) { Start-Sleep -Milliseconds 400 } } while ($seg -lt 20 -or $seg -gt 38)
        $ar = AhoraAr
        $desde = if ($ar.Hour -lt 1) { '00:00' } else { $ar.AddHours(-1).ToString('HH:mm') }
        $cierraA = $ar.AddMinutes(1).ToString('HH:mm')
        $semanaCorta = @(0..6 | ForEach-Object { if ($_ -eq [int]$ar.DayOfWeek) { Dia $_ 'Turnos' $desde $cierraA } else { Dia $_ 'Cerrado' } })
        $null = PutHorarios $semanaCorta
        $limite = [DateTimeOffset]::UtcNow.AddMinutes(1); $limite = [DateTimeOffset]::new($limite.Year, $limite.Month, $limite.Day, $limite.Hour, $limite.Minute, 0, [TimeSpan]::Zero)
        $antesDeCerrar = Menu
        Check "antes del cierre ($cierraA): MISS y abierto" (($antesDeCerrar.Headers['X-Cache'] -eq 'MISS') -and ($antesDeCerrar.Json.estado.estaAbierto -eq $true) -and ($antesDeCerrar.Json.local.esActivo -eq $true)) $antesDeCerrar.Body
        $espera = ($limite.AddSeconds(1.5) - [DateTimeOffset]::UtcNow).TotalMilliseconds
        "    esperando $([math]::Round($espera / 1000, 0)) s a que pase el cierre de las $cierraA..."
        if ($espera -gt 0) { Start-Sleep -Milliseconds ([int]$espera) }
        $despues = Menu
        Check 'despues del cierre: sigue siendo HIT (no hubo invalidacion)' ($despues.Headers['X-Cache'] -eq 'HIT') "X-Cache=$($despues.Headers['X-Cache'])"
        Check 'despues del cierre: estado cerrado, local.esActivo=false (calculado fuera del cache)' (($despues.Json.estado.estaAbierto -eq $false) -and ($despues.Json.local.esActivo -eq $false) -and ($despues.Json.estado.motivo -eq 'Horario')) $despues.Body
        Check 'despues del cierre: informa la proxima apertura' ([bool]$despues.Json.estado.proximoCambio)
    }

    # ================= 17. Pedido publico =================
    "`n[17] POST /api/public/locales/{slug}/pedidos"
    $null = PutHorarios @()
    $p0 = CantPedidos; $mv0 = CantMovimientos
    $ok1 = NuevoPedido
    Check 'control: con el local abierto el pedido se crea (200)' (($ok1.Status -eq 200) -and ($ok1.Json.pedidoId -gt 0)) $ok1.Body
    Check 'control: se creo exactamente 1 pedido' ((CantPedidos) -eq ($p0 + 1))
    $pedidoControl = $ok1.Json.pedidoId; $codigoControl = $ok1.Json.codigoSeguimiento
    $p1 = CantPedidos; $mv1 = CantMovimientos

    $null = Cierre $true
    $rc = NuevoPedido
    Check 'cierre manual: 409' ($rc.Status -eq 409) $rc.Body
    Check 'cierre manual: codigo LocalCerrado, mensaje util, sin proximoCambio' (($rc.Json.codigo -eq 'LocalCerrado') -and ($rc.Json.mensaje -eq (U 'El local est\u00e1 cerrado por el momento.')) -and ($null -eq $rc.Json.proximoCambio)) $rc.Body
    Check 'cierre manual: no se creo nada (pedidos ni movimientos de stock)' (((CantPedidos) -eq $p1) -and ((CantMovimientos) -eq $mv1))
    $null = Cierre $false

    # Cerrado por horario con apertura futura: hoy si falta menos de una hora para medianoche local de margen; si no, manana a las 10.
    $ar = AhoraAr
    if ($ar.Hour -lt 21) {
        $ini = $ar.AddHours(1); $fin = $ini.AddHours(2)
        $diaApertura = [int]$ar.DayOfWeek; $iniTxt = $ini.ToString('HH:mm'); $finTxt = $fin.ToString('HH:mm')
        $cuando = 'hoy'; $aperturaEsperada = [DateTimeOffset]::UtcNow.AddHours(1)
        $articulo = if ($ini.Hour -eq 1) { 'a la' } else { 'a las' }   # "a la 01:00"
    }
    else {
        $diaApertura = ([int]$ar.DayOfWeek + 1) % 7; $iniTxt = '10:00'; $finTxt = '12:00'
        $cuando = U 'ma\u00f1ana'; $articulo = 'a las'
        # $ar tiene Kind=Utc (viene de UtcNow): se lo pasa a Unspecified para poder darle el offset -3.
        $manana10 = [DateTime]::SpecifyKind($ar.Date.AddDays(1).AddHours(10), [DateTimeKind]::Unspecified)
        $aperturaEsperada = [DateTimeOffset]::new($manana10, [TimeSpan]::FromHours(-3))
    }
    $semanaFutura = @(0..6 | ForEach-Object { if ($_ -eq $diaApertura) { Dia $_ 'Turnos' $iniTxt $finTxt } else { Dia $_ 'Cerrado' } })
    $null = PutHorarios $semanaFutura
    $rh = NuevoPedido
    $esperado = (U 'El local est\u00e1 cerrado. Abre ') + $cuando + " $articulo $iniTxt."
    Check 'cerrado por horario: 409' ($rh.Status -eq 409) $rh.Body
    Check "cerrado por horario: mensaje '$esperado'" (($rh.Json.codigo -eq 'LocalCerrado') -and ($rh.Json.mensaje -eq $esperado)) $rh.Body
    Check 'cerrado por horario: proximoCambio = la apertura (en UTC)' (($rh.Body -match '"proximoCambio":"[^"]*(Z|\+00:00)"') -and ([math]::Abs(([DateTimeOffset]$rh.Json.proximoCambio - $aperturaEsperada).TotalMinutes) -lt 2)) $rh.Body
    Check 'cerrado por horario: no se creo nada' (((CantPedidos) -eq $p1) -and ((CantMovimientos) -eq $mv1))

    $null = PutHorarios (SemanaCerrada)
    $rt = NuevoPedido
    Check 'todo Cerrado: 409 sin promesa de hora' (($rt.Status -eq 409) -and ($rt.Json.mensaje -eq (U 'El local est\u00e1 cerrado por el momento.')) -and ($null -eq $rt.Json.proximoCambio)) $rt.Body

    $null = PutHorarios @()
    $ok2 = NuevoPedido
    Check 'al reabrir (sin horarios) el pedido vuelve a crearse: el cierre no queda pegado' ($ok2.Status -eq 200) $ok2.Body

    # ================= 18. Otros flujos publicos con el local cerrado =================
    "`n[18] Otros flujos publicos con el local cerrado (no deben bloquearse)"
    $null = Cierre $true
    $cup = Call 'POST' "/api/public/locales/$slug/cupones/validar" @{ codigo = 'NOEXISTE'; subtotalPostDescuentos = 1000 }
    Check 'validar cupon (cotizacion de solo lectura): NO se bloquea, responde 200 valido=false' (($cup.Status -eq 200) -and ($cup.Json.valido -eq $false)) $cup.Body
    $mp = Call 'POST' "/api/public/locales/$slug/pedidos/$pedidoControl/preferencia-mp" @{ codigoSeguimiento = $codigoControl }
    Check 'preferencia de pago de un pedido ya creado: NO la bloquea el cierre (llega a su validacion normal)' (($mp.Status -ne 409) -and ($mp.Body -notmatch 'LocalCerrado') -and ($mp.Body -match 'MercadoPago')) "$($mp.Status) $($mp.Body)"
    $ep = Call 'GET' "/api/public/pedidos/$codigoControl/estado-pago"
    Check 'estado-pago del pedido ya creado: sigue respondiendo 200' ($ep.Status -eq 200) $ep.Body
    $null = Cierre $false

    # ================= 19. Fallar abierto con zona invalida =================
    "`n[19] Zona horaria invalida guardada: el menu y el pedido fallan ABIERTOS"
    $null = PutHorarios (SemanaCerrada)   # con horarios todo-cerrado: si se evaluaran, el local estaria cerrado
    $null = Sql "UPDATE Administradores SET ZonaHoraria='No/Existe' WHERE Id=$adminId"
    $null = Cierre $true; $null = Cierre $false   # dos cambios reales => invalida el cache (el UPDATE por SQL no pasa por EF)
    $mF = Menu
    Check 'menu 200 (nunca se cae por la zona)' ($mF.Status -eq 200) $mF.Body
    Check 'falla abierto: estado abierto y COMPAT local.esActivo=true' (($mF.Json.estado.estaAbierto -eq $true) -and ($mF.Json.local.esActivo -eq $true)) $mF.Body
    if ($ApiLog -and (Test-Path $ApiLog)) {
        $logTxt = Get-Content $ApiLog -Raw
        Check 'el log de la API tiene el error con la zona rota' (($logTxt -match 'fail: Vinto\.Api\.Controllers\.PublicController') -and ($logTxt -match 'No/Existe') -and ($logTxt -match 'se lo trata como abierto'))
    }
    else { "    (sin log de la API: se omite la verificacion de la linea de error; usar -IniciarApi o -ApiLog)" }
    $pF = CantPedidos
    $rF = NuevoPedido
    Check 'el pedido tampoco se rechaza (falla abierto) y se crea' (($rF.Status -eq 200) -and ((CantPedidos) -eq ($pF + 1))) $rF.Body
    $gRota = Call 'GET' '/api/Administrador/local/horarios' -tok $tok
    "    informativo: el GET del panel con la zona rota responde $($gRota.Status) (el panel es estricto a proposito; el PUT permite corregirla)"
    $arreglo = PutHorarios (SemanaCerrada)
    Check 'el PUT del panel corrige la zona rota' (($arreglo.Status -eq 200) -and ((Sql "SELECT ZonaHoraria FROM Administradores WHERE Id=$adminId") -eq 'America/Argentina/Buenos_Aires'))
    $mG = Menu
    Check 'con la zona corregida el local vuelve a figurar cerrado (todo Cerrado)' (($mG.Json.estado.estaAbierto -eq $false) -and ($mG.Json.local.esActivo -eq $false)) $mG.Body

    # ================= 20. EsActivo de la cuenta sigue definiendo el 404 =================
    "`n[20] EsActivo de la cuenta: sigue siendo lo que define el 404 (no el horario)"
    $null = Sql "UPDATE Administradores SET EsActivo=0 WHERE Id=$adminId"
    $null = Call 'PATCH' "/api/Administrador/$adminId/local" -tok $tok -rawJson '{"telefono":"3510000000"}'   # fuerza una invalidacion real del cache
    $m404 = Menu
    Check 'cuenta deshabilitada: el menu da 404' ($m404.Status -eq 404) $m404.Body
    Check 'cuenta deshabilitada: el pedido da 404 (Local no encontrado), no 409' ((NuevoPedido).Status -eq 404)
    Check 'cuenta deshabilitada: el login rechaza (401)' ((Call 'POST' '/api/Auth/login' -rawJson $loginBody).Status -eq 401)
    $null = Sql "UPDATE Administradores SET EsActivo=1 WHERE Id=$adminId"
    Check 'cuenta habilitada de nuevo: el menu vuelve (200)' ((Menu).Status -eq 200)
}
finally {
    "`n[limpieza]"
    Limpiar
    "    descartables que quedan: " + (Sql "SELECT COUNT(*) FROM Administradores WHERE Email LIKE '$patronEmail'")
    "    filas huerfanas en HorariosLocal (admin inexistente): " + (Sql "SELECT COUNT(*) FROM HorariosLocal h WHERE NOT EXISTS (SELECT 1 FROM Administradores a WHERE a.Id=h.AdministradorId)")
    $despues = Sql "SELECT STRING_AGG(CONCAT(Id,':',EsActivo,':',CerradoManualmente,':',ZonaHoraria), ' | ') WITHIN GROUP (ORDER BY Id) FROM Administradores"
    "    admins antes : $antes"
    "    admins despues: $despues  -> " + $(if ($antes -eq $despues) { 'IGUAL' } else { 'DISTINTO' })
    "    filas HorariosLocal: antes $antesHor, despues " + (Sql "SELECT COUNT(*) FROM HorariosLocal")
    if ($apiProc -and -not $apiProc.HasExited) { Stop-Process -Id $apiProc.Id -Force -ErrorAction SilentlyContinue; "    API iniciada por el script: detenida" }
}

"`nRESULTADO: $($script:pasos - $script:fallos) de $($script:pasos) verificaciones OK, $($script:fallos) con falla"
if ($script:fallos -gt 0) { exit 1 }

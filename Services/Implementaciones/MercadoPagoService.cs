using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MercadoPago.Client.Common;
using MercadoPago.Client.Preference;
using Microsoft.AspNetCore.SignalR;
using Vinto.Api.Hubs;
using Vinto.Api.Models;
using MercadoPago.Config;
using MercadoPago.Error;
using MercadoPago.Resource.Preference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class MercadoPagoService : IMercadoPagoService
    {
        private readonly IAdministradorRepository _administradorRepository;
        private readonly IEncryptionHelper _encryptionHelper;
        private readonly IMemoryCache _memoryCache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _context;
        private readonly IMercadoPagoSignatureValidator _signatureValidator;
        private readonly IHubContext<PedidosHub> _hubContext;
        private readonly ILogger<MercadoPagoService> _logger;

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly string _authBaseUrl;
        private readonly string _apiBaseUrl;
        private readonly string _frontendClientUrl;
        private readonly string _backendUrl;

        private const string StateCachePrefix = "mp_oauth_state:";
        private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

        public MercadoPagoService(
            IAdministradorRepository administradorRepository,
            IEncryptionHelper encryptionHelper,
            IMemoryCache memoryCache,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            AppDbContext context,
            IMercadoPagoSignatureValidator signatureValidator,
            IHubContext<PedidosHub> hubContext,
            ILogger<MercadoPagoService> logger)
        {
            _administradorRepository = administradorRepository;
            _encryptionHelper = encryptionHelper;
            _memoryCache = memoryCache;
            _httpClientFactory = httpClientFactory;
            _context = context;
            _signatureValidator = signatureValidator;
            _hubContext = hubContext;
            _logger = logger;

            _clientId = configuration["MercadoPago:ClientId"]
                ?? throw new InvalidOperationException("MercadoPago:ClientId no configurado");
            _clientSecret = configuration["MercadoPago:ClientSecret"]
                ?? throw new InvalidOperationException("MercadoPago:ClientSecret no configurado");
            _redirectUri = configuration["MercadoPago:RedirectUri"]
                ?? throw new InvalidOperationException("MercadoPago:RedirectUri no configurado");
            _authBaseUrl = configuration["MercadoPago:AuthBaseUrl"]
                ?? throw new InvalidOperationException("MercadoPago:AuthBaseUrl no configurado");
            _apiBaseUrl = configuration["MercadoPago:ApiBaseUrl"]
                ?? throw new InvalidOperationException("MercadoPago:ApiBaseUrl no configurado");
            _frontendClientUrl = configuration["MercadoPago:FrontendClientUrl"]
                ?? throw new InvalidOperationException("MercadoPago:FrontendClientUrl no configurado");
            _backendUrl = configuration["MercadoPago:BackendUrl"] ?? "";
        }

        public Task<OAuthUrlResponseDTO> GenerarUrlAutorizacion(int adminId)
        {
            // Generar state aleatorio (32 bytes en base64url)
            var stateBytes = new byte[32];
            RandomNumberGenerator.Fill(stateBytes);
            var state = Convert.ToBase64String(stateBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            // Guardar en cache: state -> adminId, con TTL
            _memoryCache.Set(StateCachePrefix + state, adminId, StateTtl);

            // Construir URL de autorización
            var url = $"{_authBaseUrl}/authorization" +
                      $"?client_id={_clientId}" +
                      $"&response_type=code" +
                      $"&platform_id=mp" +
                      $"&state={state}" +
                      $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}";

            return Task.FromResult(new OAuthUrlResponseDTO
            {
                Url = url,
                State = state
            });
        }

        public async Task<int> ProcesarCallback(string code, string state)
        {
            // 1. Validar state contra cache
            if (string.IsNullOrEmpty(state) ||
                !_memoryCache.TryGetValue<int>(StateCachePrefix + state, out var adminId))
            {
                throw new ValidacionException("State de OAuth inválido o expirado. Reintentá la conexión.");
            }

            // 2. Eliminar state (one-time use)
            _memoryCache.Remove(StateCachePrefix + state);

            // 3. Verificar admin
            var admin = await _administradorRepository.ObtenerPorId(adminId);
            if (admin == null)
            {
                throw new ValidacionException("Administrador no encontrado");
            }

            // 4. POST a /oauth/token de MP
            var httpClient = _httpClientFactory.CreateClient();
            var requestBody = new
            {
                client_id = _clientId,
                client_secret = _clientSecret,
                code = code,
                grant_type = "authorization_code",
                redirect_uri = _redirectUri
            };

            var response = await httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/oauth/token",
                requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"MercadoPago rechazó el intercambio de token. Status: {response.StatusCode}. Detalle: {errorContent}");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<MercadoPagoTokenResponse>();
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                throw new InvalidOperationException("MercadoPago devolvió una respuesta vacía o inválida.");
            }

            // 5. Guardar en admin (tokens cifrados)
            admin.MercadoPagoUserId = tokenResponse.UserId.ToString();
            admin.MercadoPagoAccessToken = _encryptionHelper.Encrypt(tokenResponse.AccessToken);
            admin.MercadoPagoRefreshToken = _encryptionHelper.Encrypt(tokenResponse.RefreshToken);
            admin.MercadoPagoPublicKey = tokenResponse.PublicKey;
            admin.MercadoPagoTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            admin.MercadoPagoConectado = true;

            await _administradorRepository.Actualizar(admin);

            return adminId;
        }

        public async Task Desconectar(int adminId)
        {
            var admin = await _administradorRepository.ObtenerPorId(adminId);
            if (admin == null)
            {
                throw new ValidacionException("Administrador no encontrado");
            }

            admin.MercadoPagoUserId = null;
            admin.MercadoPagoAccessToken = null;
            admin.MercadoPagoRefreshToken = null;
            admin.MercadoPagoPublicKey = null;
            admin.MercadoPagoTokenExpiresAt = null;
            admin.MercadoPagoConectado = false;

            await _administradorRepository.Actualizar(admin);
        }

        public async Task<EstadoConexionMpResponseDTO> ObtenerEstado(int adminId)
        {
            var admin = await _administradorRepository.ObtenerPorId(adminId);
            if (admin == null)
            {
                throw new ValidacionException("Administrador no encontrado");
            }

            return new EstadoConexionMpResponseDTO
            {
                Conectado = admin.MercadoPagoConectado,
                MercadoPagoUserId = admin.MercadoPagoUserId,
                TokenExpiraEn = admin.MercadoPagoTokenExpiresAt
            };
        }

        public async Task<CrearPreferenciaResponseDTO> CrearPreferenciaPago(string slug, int pedidoId, string codigoSeguimiento)
        {
            // 1. Buscar admin por slug (mismo patrón que PublicController)
            var admin = await _context.Administradores
                .FirstOrDefaultAsync(a => a.NombreLocal.ToLower().Replace(" ", "-") == slug);

            if (admin == null)
                throw new ValidacionException("Local no encontrado.");

            // 2. Validar que MP esté conectado
            if (!admin.MercadoPagoConectado || string.IsNullOrEmpty(admin.MercadoPagoAccessToken))
                throw new ValidacionException("Este local no tiene MercadoPago conectado.");

            // 3. Buscar el pedido
            var pedido = await _context.Pedidos
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.Id == pedidoId && p.AdministradorId == admin.Id);

            if (pedido == null)
                throw new ValidacionException("Pedido no encontrado.");

            // 4. Validar codigoSeguimiento
            if (string.IsNullOrEmpty(codigoSeguimiento) || pedido.CodigoSeguimiento != codigoSeguimiento)
                throw new ValidacionException("Código de seguimiento inválido.");

            // 5. Validar estado del pedido (solo se paga si está Pendiente)
            if (pedido.Estado != "Pendiente")
                throw new ValidacionException($"No se puede generar pago para un pedido en estado '{pedido.Estado}'.");

            // 6. Validar forma de pago
            if (!string.Equals(pedido.FormaPago, "MercadoPago", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pedido.FormaPago, "Tarjeta", StringComparison.OrdinalIgnoreCase))
                throw new ValidacionException($"El pedido no usa MercadoPago como forma de pago (FormaPago actual: {pedido.FormaPago}).");

            // 7. Descifrar el access_token del admin
            string accessTokenAdmin;
            try
            {
                accessTokenAdmin = _encryptionHelper.Decrypt(admin.MercadoPagoAccessToken);
            }
            catch (Exception)
            {
                throw new InvalidOperationException("No se pudo descifrar el token de MercadoPago del local. Reconectá MP.");
            }

            // 8. Construir items de la preferencia
            var items = new List<PreferenceItemRequest>();
            bool tieneDescuentos = pedido.MontoDescuentoProductos > 0 || pedido.MontoDescuentoCupon > 0;

            if (!tieneDescuentos)
            {
                foreach (var detalle in pedido.Detalles)
                {
                    var nombreProducto = detalle.Producto?.Nombre ?? "Producto";
                    items.Add(new PreferenceItemRequest
                    {
                        Id = detalle.Producto?.Id.ToString() ?? "0",
                        Title = nombreProducto,
                        Description = nombreProducto,
                        CategoryId = "food",
                        Quantity = detalle.Cantidad,
                        UnitPrice = detalle.PrecioUnitario,
                        CurrencyId = "ARS",
                    });
                }
            }
            else
            {
                var descripcionProductos = string.Join(", ", pedido.Detalles
                    .Select(d => $"{d.Cantidad}x {d.Producto?.Nombre ?? "Producto"}"));

                items.Add(new PreferenceItemRequest
                {
                    Id = $"pedido-{pedido.Id}",
                    Title = $"Pedido #{pedido.Id} - {admin.NombreLocal}",
                    Description = descripcionProductos.Length > 250
                        ? descripcionProductos.Substring(0, 247) + "..."
                        : descripcionProductos,
                    CategoryId = "food",
                    Quantity = 1,
                    UnitPrice = pedido.Total,
                    CurrencyId = "ARS",
                });
            }

            // 9. Construir la preferencia completa
            var preferenceRequest = new PreferenceRequest
            {
                Items = items,
                Payer = new PreferencePayerRequest
                {
                    Name = pedido.NombreCliente ?? "Cliente",
                    Phone = !string.IsNullOrEmpty(pedido.TelefonoCliente)
                        ? new PhoneRequest
                        {
                            AreaCode = "",
                            Number = pedido.TelefonoCliente
                        }
                        : null,
                },
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{_frontendClientUrl}/{slug}/pago/success?codigo={pedido.CodigoSeguimiento}",
                    Failure = $"{_frontendClientUrl}/{slug}/pago/failure?codigo={pedido.CodigoSeguimiento}",
                    Pending = $"{_frontendClientUrl}/{slug}/pago/pending?codigo={pedido.CodigoSeguimiento}"
                },
                PaymentMethods = new PreferencePaymentMethodsRequest
                {
                    ExcludedPaymentTypes = new List<PreferencePaymentTypeRequest>
                    {
                        new PreferencePaymentTypeRequest { Id = "credit_card" },
                        new PreferencePaymentTypeRequest { Id = "ticket" }
                    },
                    Installments = 1
                },
                NotificationUrl = !string.IsNullOrEmpty(_backendUrl)
                    ? $"{_backendUrl}/api/MercadoPago/webhook"
                    : null,
                ExternalReference = pedido.CodigoSeguimiento,
                StatementDescriptor = TruncarStatementDescriptor(admin.NombreLocal),
                // NOTA: AutoReturn requiere HTTPS, lo dejamos sin setear para desarrollo.
                // En producción podríamos agregar: AutoReturn = "approved"
            };

            // 9. Crear la preferencia llamando a MP con el access_token del local
            var requestOptions = new MercadoPago.Client.RequestOptions
            {
                AccessToken = accessTokenAdmin
            };

            var client = new PreferenceClient();
            Preference preference;
            try
            {
                preference = await client.CreateAsync(preferenceRequest, requestOptions);
            }
            catch (MercadoPagoApiException ex)
            {
                var detalle = ex.ApiError?.Message;

                if (string.IsNullOrEmpty(detalle) && ex.ApiError?.Cause != null && ex.ApiError.Cause.Count > 0)
                {
                    detalle = ex.ApiError.Cause[0].Description;
                }

                detalle ??= ex.Message;

                throw new InvalidOperationException(
                    $"MercadoPago rechazó la preferencia (status {ex.StatusCode}): {detalle}");
            }

            // 10. Guardar el preference_id en el pedido
            pedido.MercadoPagoPreferenceId = preference.Id;
            pedido.MercadoPagoStatus = "pending";
            await _context.SaveChangesAsync();

            // 11. Devolver lo que necesita el frontend
            return new CrearPreferenciaResponseDTO
            {
                PreferenceId = preference.Id ?? string.Empty,
                InitPoint = preference.InitPoint ?? string.Empty,
                SandboxInitPoint = preference.SandboxInitPoint ?? string.Empty
            };
        }

        public async Task ProcesarWebhookPago(string paymentId, string requestId, string xSignature)
        {
            if (string.IsNullOrEmpty(paymentId))
            {
                // Webhook sin paymentId: no hay nada que procesar. Salimos silenciosamente.
                return;
            }

            // 1. VALIDAR FIRMA HMAC
            var firmaValida = _signatureValidator.Validar(paymentId, requestId, xSignature);
            if (!firmaValida)
            {
                _logger.LogWarning("Firma de webhook inválida para paymentId {PaymentId}. Webhook ignorado.", paymentId);
                return;
            }

            // 2. CONSULTAR EL PAGO REAL A MP
            // Primero necesitamos saber a qué local pertenece este pago.
            // Para eso consultamos el pago: el response tiene "external_reference" = codigoSeguimiento del pedido.
            // Pero para consultar el pago necesitamos un access_token... y el access_token está en el admin del pedido.
            // Circular. Solución: hacemos la consulta con un access_token genérico de "marketplace" — NO existe en MP.
            // En realidad: hacemos la consulta con el access_token de CUALQUIER admin que tenga MP conectado y MP nos devuelve
            // el pago si "el pago fue hecho en nombre de algún seller bajo este client_id/marketplace".
            // En la práctica, MP devuelve los pagos asociados a tu app si consultás con cualquier access_token vinculado.
            //
            // Approach robusto: necesitamos saber primero el pedido. Para eso podemos:
            //   - opción A: usar el access_token del primer admin con MP conectado (puede fallar)
            //   - opción B: parsear external_reference del payload original — pero MP no nos lo manda en el body del webhook
            //   - opción C: consultar con el access_token de la app (client_credentials)
            //
            // Vamos por C: solicitar un access_token de la app via client_credentials para hacer la consulta inicial.

            var httpClient = _httpClientFactory.CreateClient();

            // 2a) Obtener access_token de marketplace usando client_credentials
            string appAccessToken;
            try
            {
                var clientCredsRequest = new
                {
                    client_id = _clientId,
                    client_secret = _clientSecret,
                    grant_type = "client_credentials"
                };

                var clientCredsResponse = await httpClient.PostAsJsonAsync(
                    $"{_apiBaseUrl}/oauth/token",
                    clientCredsRequest);

                if (!clientCredsResponse.IsSuccessStatusCode)
                {
                    var errorBody = await clientCredsResponse.Content.ReadAsStringAsync();
                    _logger.LogError("No se pudo obtener app_access_token de MercadoPago. Status: {Status}. Body: {Body}", clientCredsResponse.StatusCode, errorBody);
                    throw new InvalidOperationException("No se pudo autenticar contra MercadoPago.");
                }

                var tokenResp = await clientCredsResponse.Content.ReadFromJsonAsync<MercadoPagoTokenResponse>();
                if (tokenResp == null || string.IsNullOrEmpty(tokenResp.AccessToken))
                {
                    throw new InvalidOperationException("MP devolvió token vacío.");
                }

                appAccessToken = tokenResp.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo app_access_token de MercadoPago.");
                throw;
            }

            // 2b) Consultar el detalle del pago
            var paymentRequest = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/v1/payments/{paymentId}");
            paymentRequest.Headers.Add("Authorization", $"Bearer {appAccessToken}");

            var paymentResponse = await httpClient.SendAsync(paymentRequest);

            if (!paymentResponse.IsSuccessStatusCode)
            {
                var errorBody = await paymentResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("No se pudo consultar el pago {PaymentId} en MP. Status: {Status}. Body: {Body}", paymentId, paymentResponse.StatusCode, errorBody);

                // Si el pago no existe (404), no es un error transitorio — no tiene sentido reintentar.
                // Salimos silenciosamente para que MP no nos vuelva a mandar el mismo webhook.
                if (paymentResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return;
                }

                // Para otros errores (500, timeout, etc) sí lanzamos para que MP reintente más tarde.
                throw new InvalidOperationException($"No se pudo obtener detalle del pago {paymentId}");
            }

            var pagoJson = await paymentResponse.Content.ReadAsStringAsync();
            var pagoData = JsonDocument.Parse(pagoJson).RootElement;

            // Extraer campos clave del pago
            var status = pagoData.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
            var statusDetail = pagoData.TryGetProperty("status_detail", out var sdEl) ? sdEl.GetString() : null;
            var externalReference = pagoData.TryGetProperty("external_reference", out var erEl) ? erEl.GetString() : null;
            var transactionAmount = pagoData.TryGetProperty("transaction_amount", out var taEl) ? taEl.GetDecimal() : 0m;

            if (string.IsNullOrEmpty(externalReference))
            {
                _logger.LogWarning("Pago {PaymentId} sin external_reference. No se puede asociar a un pedido.", paymentId);
                return;
            }

            if (string.IsNullOrEmpty(status))
            {
                _logger.LogWarning("Pago {PaymentId} sin status. Webhook ignorado.", paymentId);
                return;
            }

            // 3. CHEQUEAR IDEMPOTENCIA: ¿ya procesamos este (paymentId, status) antes?
            var yaExiste = await _context.PagosMercadoPago
                .AnyAsync(p => p.PaymentId == paymentId && p.Status == status);

            if (yaExiste)
            {
                _logger.LogInformation("Pago {PaymentId} con status {Status} ya procesado (idempotencia). Skipping.", paymentId, status);
                return;
            }

            // 4. BUSCAR EL PEDIDO POR codigoSeguimiento (external_reference)
            var pedido = await _context.Pedidos
                .FirstOrDefaultAsync(p => p.CodigoSeguimiento == externalReference);

            if (pedido == null)
            {
                _logger.LogWarning("No se encontró pedido con codigoSeguimiento {ExternalReference}. Webhook ignorado.", externalReference);
                return;
            }

            // 5. ACTUALIZAR EL PEDIDO
            pedido.MercadoPagoPaymentId = paymentId;
            pedido.MercadoPagoStatus = status;
            pedido.MercadoPagoStatusDetail = statusDetail;

            if (status == "approved")
            {
                pedido.MercadoPagoFechaPago = DateTime.UtcNow;
                pedido.Estado = "Confirmado";
            }
            // si status == "rejected" → dejamos pedido en "Pendiente" (decisión del Sprint)

            // 6. REGISTRAR EN AUDITORÍA
            var pagoMp = new PagoMercadoPago
            {
                PedidoId = pedido.Id,
                PaymentId = paymentId,
                Status = status,
                StatusDetail = statusDetail,
                Monto = transactionAmount,
                FechaEvento = DateTime.UtcNow,
                RawWebhookData = pagoJson,
                ProcesadoConExito = true
            };

            try
            {
                _context.PagosMercadoPago.Add(pagoMp);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_PagosMercadoPago_PaymentId_Status") == true)
            {
                // Race condition: otro proceso insertó el mismo (paymentId, status) al mismo tiempo.
                // Es seguro ignorar: el índice único garantiza que solo uno gana.
                _logger.LogInformation("Race condition detectada para PaymentId={PaymentId}, Status={Status}. Otro proceso lo insertó primero (idempotencia OK).", paymentId, status);
                return;
            }

            // 7. NOTIFICAR AL ADMIN VIA SIGNALR (solo si está confirmado)
            if (status == "approved")
            {
                await _hubContext.Clients
                    .Group(pedido.AdministradorId.ToString())
                    .SendAsync("PagoConfirmado", new
                    {
                        pedidoId = pedido.Id,
                        codigoSeguimiento = pedido.CodigoSeguimiento,
                        paymentId = paymentId,
                        monto = transactionAmount,
                        nombreCliente = pedido.NombreCliente,
                        fechaPago = pedido.MercadoPagoFechaPago
                    });
            }

            _logger.LogInformation("Pago {PaymentId} procesado correctamente. Pedido {PedidoId} → {Estado}.", paymentId, pedido.Id, pedido.Estado);
        }

        public async Task<object> SimularWebhookAprobadoDev(int pedidoId, string paymentIdSimulado)
        {
            var pedido = await _context.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId);
            if (pedido == null)
                throw new ValidacionException($"Pedido {pedidoId} no encontrado.");

            if (string.IsNullOrEmpty(pedido.CodigoSeguimiento))
                throw new ValidacionException($"Pedido {pedidoId} no tiene CodigoSeguimiento (probablemente es un pedido viejo).");

            var yaExiste = await _context.PagosMercadoPago
                .AnyAsync(p => p.PaymentId == paymentIdSimulado && p.Status == "approved");

            if (yaExiste)
            {
                return new
                {
                    mensaje = "Ya procesado anteriormente (idempotencia OK)",
                    pedidoId = pedido.Id,
                    estado = pedido.Estado
                };
            }

            pedido.MercadoPagoPaymentId = paymentIdSimulado;
            pedido.MercadoPagoStatus = "approved";
            pedido.MercadoPagoStatusDetail = "accredited";
            pedido.MercadoPagoFechaPago = DateTime.UtcNow;
            pedido.Estado = "Confirmado";

            var pagoMp = new PagoMercadoPago
            {
                PedidoId = pedido.Id,
                PaymentId = paymentIdSimulado,
                Status = "approved",
                StatusDetail = "accredited",
                Monto = pedido.Total,
                FechaEvento = DateTime.UtcNow,
                RawWebhookData = "{ \"simulated\": true, \"dev\": true }",
                ProcesadoConExito = true
            };

            _context.PagosMercadoPago.Add(pagoMp);
            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group(pedido.AdministradorId.ToString())
                .SendAsync("PagoConfirmado", new
                {
                    pedidoId = pedido.Id,
                    codigoSeguimiento = pedido.CodigoSeguimiento,
                    paymentId = paymentIdSimulado,
                    monto = pedido.Total,
                    nombreCliente = pedido.NombreCliente,
                    fechaPago = pedido.MercadoPagoFechaPago
                });

            _logger.LogInformation("[DEV] Webhook simulado para Pedido {PedidoId} → Confirmado.", pedido.Id);

            return new
            {
                mensaje = "Webhook simulado procesado correctamente",
                pedidoId = pedido.Id,
                estado = pedido.Estado,
                paymentId = paymentIdSimulado,
                signalRNotificado = true
            };
        }

        public async Task<MercadoPagoDiagnosticoResponseDTO> ObtenerDiagnostico(int adminId)
        {
            var admin = await _context.Administradores.FirstOrDefaultAsync(a => a.Id == adminId);
            if (admin == null)
                throw new ValidacionException("Administrador no encontrado.");

            var conectado = admin.MercadoPagoConectado;

            var tokenExpirado = conectado
                && admin.MercadoPagoTokenExpiresAt.HasValue
                && admin.MercadoPagoTokenExpiresAt.Value < DateTime.UtcNow;

            var pedidosPendientes = await _context.Pedidos
                .CountAsync(p =>
                    p.AdministradorId == adminId
                    && p.Estado == "Pendiente"
                    && (p.FormaPago == "Tarjeta" || p.FormaPago == "MercadoPago"));

            return new MercadoPagoDiagnosticoResponseDTO
            {
                Conectado = conectado,
                TokenExpirado = tokenExpirado,
                PedidosPendientesConMP = pedidosPendientes
            };
        }

        private static string TruncarStatementDescriptor(string? nombreLocal)
        {
            if (string.IsNullOrEmpty(nombreLocal)) return "VINTO";

            var limpio = new string(nombreLocal
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => char.IsLetterOrDigit(c) || c == ' ')
                .ToArray())
                .ToUpper()
                .Trim();

            if (string.IsNullOrEmpty(limpio)) return "VINTO";

            return limpio.Length > 22 ? limpio.Substring(0, 22) : limpio;
        }

        // Clase interna para deserializar la respuesta de /oauth/token de MP
        private class MercadoPagoTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; } = string.Empty;

            [JsonPropertyName("expires_in")]
            public long ExpiresIn { get; set; }

            [JsonPropertyName("scope")]
            public string Scope { get; set; } = string.Empty;

            [JsonPropertyName("user_id")]
            public long UserId { get; set; }

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; } = string.Empty;

            [JsonPropertyName("public_key")]
            public string? PublicKey { get; set; }

            [JsonPropertyName("live_mode")]
            public bool LiveMode { get; set; }
        }
    }
}

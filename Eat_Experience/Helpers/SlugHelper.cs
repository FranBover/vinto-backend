using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vinto.Api.Helpers
{
    // Único lugar donde se genera y valida el slug de un local (Administrador.SlugLocal).
    public static class SlugHelper
    {
        public const int MaxLength = 60;

        // Slugs que no puede usar un local: colisionan con rutas del frontend o con rutas futuras.
        // Única definición; la validan Register, el PATCH y la generación automática.
        public static readonly IReadOnlySet<string> Reservados = new HashSet<string>(StringComparer.Ordinal)
        {
            "admin", "assets", "api", "www", "static", "public", "login", "health", "status",
            "blog", "help", "soporte", "terminos", "privacidad", "well-known"
        };

        public static bool EsReservado(string? slug) => slug != null && Reservados.Contains(slug);

        private static readonly Regex FormatoValido = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

        // Minúsculas, sin acentos (ñ -> n), sin puntuación; espacios, '_' y '-' son separadores
        // y se colapsan en un solo guión, sin guiones al inicio ni al final.
        // Devuelve "" si no queda ningún carácter alfanumérico.
        public static string Slugify(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder();
            var separadorPendiente = false;

            foreach (var ch in value.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
                {
                    if (separadorPendiente && sb.Length > 0)
                        sb.Append('-');
                    separadorPendiente = false;
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (char.IsWhiteSpace(ch) || ch == '_' || ch == '-')
                {
                    separadorPendiente = true;
                }
                // Cualquier otro carácter (puntuación, símbolos) se descarta sin separar.
            }

            var slug = sb.ToString();
            if (slug.Length > MaxLength)
                slug = slug[..MaxLength].TrimEnd('-');

            return slug;
        }

        // Forma en que se compara un slug que viene del request o de un input manual.
        public static string Normalizar(string? slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();

        public static bool EsFormatoValido(string? slug) =>
            !string.IsNullOrEmpty(slug) && slug.Length <= MaxLength && FormatoValido.IsMatch(slug);

        // baseSlug + "-2", "-3", ... recortando la base para no pasar de MaxLength.
        public static string ConSufijo(string baseSlug, int numero)
        {
            var sufijo = $"-{numero}";
            var baseRecortada = baseSlug.Length + sufijo.Length > MaxLength
                ? baseSlug[..(MaxLength - sufijo.Length)].TrimEnd('-')
                : baseSlug;
            return baseRecortada + sufijo;
        }
    }
}

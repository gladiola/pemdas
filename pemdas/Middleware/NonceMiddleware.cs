using System.Security.Cryptography;
using System.Text;

namespace pemdas.Middleware
{
    /// <summary>
    /// Per-request middleware that generates a cryptographically random CSP nonce,
    /// stores it in <see cref="HttpContext.Items"/> under the key "Nonce", and
    /// appends a Content-Security-Policy header that uses that nonce for script-src.
    ///
    /// The nonce is generated fresh for every request so that an attacker who
    /// observes one nonce cannot reuse it for a different request.
    ///
    /// Views access the nonce via <c>@Context.Items["Nonce"]</c> and must add a
    /// <c>nonce="..."</c> attribute to every <c>&lt;script&gt;</c> tag they render.
    /// </summary>
    public class NonceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<NonceMiddleware> _logger;

        public NonceMiddleware(RequestDelegate next, ILogger<NonceMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Generate a 16-byte (128-bit) cryptographically random nonce.
            // Do NOT log the nonce value — logging it in plaintext would let
            // anyone with log access inject inline scripts using the known nonce.
            var nonceBytes = new byte[16];
            RandomNumberGenerator.Fill(nonceBytes);
            var nonce = Convert.ToBase64String(nonceBytes);

            // Make the nonce available to Razor views.
            context.Items["Nonce"] = nonce;

            // Register the CSP header to be written just before the response headers
            // are flushed so that it is set even when downstream middleware short-circuits.
            context.Response.OnStarting(state =>
            {
                var ctx = (HttpContext)state;
                if (!ctx.Response.Headers.ContainsKey("Content-Security-Policy"))
                {
                    ctx.Response.Headers["Content-Security-Policy"] = BuildCspHeader(nonce);
                }
                return Task.CompletedTask;
            }, context);

            await _next(context);
        }

        /// <summary>
        /// Builds the CSP header value for this application.
        /// Allows scripts only from 'self' and those carrying the per-request nonce.
        /// Inline styles are permitted (required for Bootstrap collapse/tooltip animation).
        /// </summary>
        private static string BuildCspHeader(string nonce)
        {
            var sb = new StringBuilder();
            sb.Append("default-src 'self'; ");
            sb.Append($"script-src 'self' 'nonce-{nonce}'; ");
            sb.Append("style-src 'self' 'unsafe-inline'; ");
            sb.Append("img-src 'self' data:; ");
            sb.Append("font-src 'self'; ");
            sb.Append("connect-src 'self'; ");
            sb.Append("frame-ancestors 'none'; ");
            sb.Append("form-action 'self';");
            return sb.ToString();
        }
    }
}

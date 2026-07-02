namespace pemdas.Middleware
{
    /// <summary>
    /// Middleware that appends standard security headers to every HTTP response.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityHeadersMiddleware> _logger;

        public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(state =>
            {
                var ctx = (HttpContext)state;
                var headers = ctx.Response.Headers;

                // Clickjacking: disallow embedding in any frame
                headers["X-Frame-Options"] = "DENY";

                // Disable legacy XSS filter (can introduce vulnerabilities in older IE)
                headers["X-XSS-Protection"] = "0";

                // Prevent MIME-type sniffing
                headers["X-Content-Type-Options"] = "nosniff";

                // Referrer policy — only send origin on cross-origin requests
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

                // HSTS — enforce HTTPS for one year including sub-domains
                if (!headers.ContainsKey("Strict-Transport-Security"))
                    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

                // Cross-origin isolation
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Cross-Origin-Resource-Policy"] = "same-site";

                // Disable browser features not needed by this application
                headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), interest-cohort=()";

                // Suppress server identity headers
                headers.Remove("Server");
                headers["Server"] = "webserver";
                headers.Remove("X-Powered-By");
                headers.Remove("X-AspNetMvc-Version");

                return Task.CompletedTask;
            }, context);

            await _next(context);
        }
    }
}

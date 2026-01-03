public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        // Only log for the specific API path or all POST requests
        if (context.Request.Method == "POST")
        {
            _logger.LogInformation("--- Incoming POST Request ---");
            _logger.LogInformation($"Path: {context.Request.Path}");

            // Log all Headers
            foreach (var header in context.Request.Headers)
            {
                _logger.LogInformation($"Header: {header.Key} = {header.Value}");
            }

            // Enable buffering so the body can be read here and again by the controller
            context.Request.EnableBuffering();
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            _logger.LogInformation($"Body: {body}");

            // Reset position so the controller can read the body
            context.Request.Body.Position = 0;
        }

        // 1. Listen for the response to start
        context.Response.OnStarting(() =>
        {
            _logger.LogInformation("--- CORS Response Headers ---");

            // Log key CORS headers
            var corsHeaders = new[] {
            "Access-Control-Allow-Origin",
            "Access-Control-Allow-Methods",
            "Access-Control-Allow-Headers"
        };

            foreach (var header in corsHeaders)
            {
                if (context.Response.Headers.TryGetValue(header, out var value))
                {
                    _logger.LogInformation($"{header}: {value}");
                }
                else
                {
                    _logger.LogWarning($"{header}: NOT PRESENT");
                }
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
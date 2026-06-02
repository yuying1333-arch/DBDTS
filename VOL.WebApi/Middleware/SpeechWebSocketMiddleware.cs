using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VOL.WebApi.Services.SpeechRealtime;

namespace VOL.WebApi.Middleware
{
    public class SpeechWebSocketMiddleware
    {
        private readonly RequestDelegate _next;

        public SpeechWebSocketMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, SpeechWebSocketHandler webSocketHandler)
        {
            if (!context.Request.Path.Equals("/ws/speech", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("WebSocket endpoint only.");
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await webSocketHandler.HandleAsync(socket, context.RequestAborted);
        }
    }
}

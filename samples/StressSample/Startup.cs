﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace ANCMStressTestSite
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
            app.Map("/HelloWorld", HelloWorld);
            app.Map("/ConnectionClose", ConnectionClose);
            app.Map("/EchoPostData", EchoPostData);
            app.Map("/LargeResponseBody", LargeResponseBody);
            app.Map("/ResponseHeaders", ResponseHeaders);
            app.Map("/EnvironmentVariables", EnvironmentVariables);
            app.Map("/RequestInformation", RequestInformation);
            app.Map("/WebSocket", WebSocket);

            app.Run(async context =>
            {
                await context.Response.WriteAsync("Default Page");
            });
        }

        private void HelloWorld(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                await context.Response.WriteAsync("Hello World");
            });
        }

        private void ConnectionClose(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                context.Response.Headers[HeaderNames.Connection] = "close";
                await context.Response.WriteAsync("Connnection Close");
                await context.Response.Body.FlushAsync();
            });
        }

        private void EchoPostData(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                string responseBody = string.Empty;

                if (string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    using (StreamReader reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                    {
                        responseBody = await reader.ReadToEndAsync();
                    }
                }
                else
                {
                    responseBody = "NoAction";
                }

                await context.Response.WriteAsync(responseBody);
            });
        }

        private void LargeResponseBody(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                if (int.TryParse(context.Request.Query["length"], out var length))
                {
                    await context.Response.WriteAsync(new string('a', length));
                }
            });
        }

        private void ResponseHeaders(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                context.Response.Headers["UnknownHeader"] = "test123=foo";
                context.Response.ContentType = "text/plain";
                context.Response.Headers["MultiHeader"] = new StringValues(new string[] { "1", "2" });
                await context.Response.WriteAsync("Request Complete");
            });
        }

        private void EnvironmentVariables(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Environment Variables:" + Environment.NewLine);
                var vars = Environment.GetEnvironmentVariables();
                foreach (var key in vars.Keys.Cast<string>().OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                {
                    var value = vars[key];
                    await context.Response.WriteAsync(key + ": " + value + Environment.NewLine);
                }
                await context.Response.WriteAsync(Environment.NewLine);
            });
        }

        private void RequestInformation(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                context.Response.ContentType = "text/plain";

                await context.Response.WriteAsync("Address:" + Environment.NewLine);
                await context.Response.WriteAsync("Scheme: " + context.Request.Scheme + Environment.NewLine);
                await context.Response.WriteAsync("Host: " + context.Request.Headers["Host"] + Environment.NewLine);
                await context.Response.WriteAsync("PathBase: " + context.Request.PathBase.Value + Environment.NewLine);
                await context.Response.WriteAsync("Path: " + context.Request.Path.Value + Environment.NewLine);
                await context.Response.WriteAsync("Query: " + context.Request.QueryString.Value + Environment.NewLine);
                await context.Response.WriteAsync(Environment.NewLine);

                await context.Response.WriteAsync("Connection:" + Environment.NewLine);
                await context.Response.WriteAsync("RemoteIp: " + context.Connection.RemoteIpAddress + Environment.NewLine);
                await context.Response.WriteAsync("RemotePort: " + context.Connection.RemotePort + Environment.NewLine);
                await context.Response.WriteAsync("LocalIp: " + context.Connection.LocalIpAddress + Environment.NewLine);
                await context.Response.WriteAsync("LocalPort: " + context.Connection.LocalPort + Environment.NewLine);
                await context.Response.WriteAsync(Environment.NewLine);

                await context.Response.WriteAsync("Headers:" + Environment.NewLine);
                foreach (var header in context.Request.Headers)
                {
                    await context.Response.WriteAsync(header.Key + ": " + header.Value + Environment.NewLine);
                }
                await context.Response.WriteAsync(Environment.NewLine);
            });
        }

        private void WebSocket(IApplicationBuilder app)
        {
            app.UseWebSockets(new WebSocketOptions
            {
            });

            app.Use(async (context, next) =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await Echo(webSocket);
                }
                else
                {
                    var wsScheme = context.Request.IsHttps ? "wss" : "ws";
                    var wsUrl = $"{wsScheme}://{context.Request.Host.Host}:{context.Request.Host.Port}{context.Request.Path}";
                    await context.Response.WriteAsync($"Ready to accept a WebSocket request at: {wsUrl}");
                }
            });
        }

        private async Task Echo(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            bool closeFromServer = false;
            string closeFromServerCmd = "CloseFromServer";
            int closeFromServerLength = closeFromServerCmd.Length;

            bool echoBack = true;
            int repeatCount = 1;

            while (!result.CloseStatus.HasValue)
            {
                if ((result.Count == closeFromServerLength && System.Text.Encoding.ASCII.GetString(buffer).Substring(0, result.Count) == closeFromServerCmd)
                    || Program.AppLifetimeStopping == true)
                {
                    // start closing handshake from backend process when client send "CloseFromServer" text message 
                    // or when any message is sent from client during the graceful shutdown.
                    closeFromServer = true;
                    await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, closeFromServerCmd, CancellationToken.None);
                }
                else
                {

                    if (buffer[0] == '_')
                    {
                        string tempString = System.Text.Encoding.ASCII.GetString(buffer).Substring(0, result.Count).ToLower();
                        switch (tempString)
                        {
                            case "_donotecho":
                                echoBack = false;
                                break;
                            case "_1":
                                repeatCount = 1;
                                break;
                            case "_10":
                                repeatCount = 10;
                                break;
                            case "_100":
                                repeatCount = 100;
                                break;
                            default:
                                break;
                        }
                    }
                    if (echoBack)
                    {
                        for (int i = 0; i < repeatCount; i++)
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                        }
                    }
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            if (closeFromServer)
            {
                webSocket.Dispose();
                return;
            }

            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            webSocket.Dispose();
        }
    }
}
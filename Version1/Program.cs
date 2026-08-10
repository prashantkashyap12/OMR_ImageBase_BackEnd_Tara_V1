using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Version1.Data;
using Version1.Services;
using SQCScanner.Services;
using SQCScanner.websoketManager;
using System.Net.WebSockets;
using Microsoft.Extensions.FileProviders;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SixLabors.ImageSharp;
using SQCScanner.Modal;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
builder.Host.UseSerilog();  
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("dbc")));
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(7400);
});
builder.Services.AddScoped<OmrProcessingService>();
builder.Services.AddScoped<JwtAuth>();
builder.Services.AddScoped<EncptDcript>();
builder.Services.AddScoped<RecordDBClass>();
builder.Services.AddScoped<RecordSave>();
builder.Services.AddScoped<table_gen>();
builder.Services.AddScoped<ImgSave>();
builder.Services.AddScoped<FindCordinationClass>();
builder.Services.AddScoped<BarCodeScaning>();
builder.Services.AddScoped<RealtimeCSV_Rec>();
builder.Services.AddSingleton<OmrProcessingControlService>();
builder.Services.AddSingleton<WebSocketConnectionManager>();
builder.Services.AddSingleton<WebSoketHandler>();
builder.Services.AddScoped<MargeGenSerivce>();
builder.Services.AddScoped<EmailSendClass>();
builder.Services.AddScoped<BravoServices>();
builder.Services.AddHttpClient();
var configuration = builder.Configuration;
builder.Services.AddJwtAuthentication(configuration);
builder.Services.AddControllers();            // Add Base controller.
builder.Services.AddEndpointsApiExplorer();   // Make meta data for get/post for swagger
builder.Services.AddSwaggerGen();             // Gen UI Swagger
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAnyOrigin", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


// Syncfusion Key Add
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NCaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXhdcHRVQmVeV0F3Wks=\r\n");

// Add Authentication with JWT
var app = builder.Build();

// Http Routing Redirection
app.UseHttpsRedirection();

// Cross sharing - not specifically
app.UseCors("AllowAnyOrigin");

// Auth JWT valid / New
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    c.RoutePrefix = "swagger"; // Optional, default is "swagger"
});

// Permission Folder
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wFileManager")),
    RequestPath = "/wFileManager",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
    }
});

// wwwroot ke liye CORS header
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
    }
});
app.UseStaticFiles();

// web Soket Middleware
app.UseWebSockets();
app.Use(async (context, next) =>
{
    // make path, client ne header k sath req. bheji hai / ni.
    if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest)
    {
        // Check if the request is a WebSocket request
        var socket = await context.WebSockets.AcceptWebSocketAsync();

        // 2. Extract token from query string
        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Token missing");
            return;
        }

        // 3. Parse JWT token to get userId   
        string userId = null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid")?.Value;
        }
        catch
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsync("Invalid token");
            return;
        }

        if (string.IsNullOrEmpty(userId))
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsync("UserId not found in token");
            return;
        }

        // 4. Get services
        var connectionManager = context.RequestServices.GetRequiredService<WebSocketConnectionManager>();
        var handlerService = context.RequestServices.GetRequiredService<WebSoketHandler>();
        connectionManager.RemoveSocket(userId);  // Remove socket before adding new using UserId (f5, newTab etc)

        // 5. Add socket with userId
        connectionManager.AddSocket(userId, socket);
        var buffer = new byte[1024 * 4];                // kb buffer i/c voice msg
        var cts = new CancellationTokenSource();
        var appStopping = context.RequestAborted;       // Cancels automatically when the client disconnects

        // F5 Refresh, stop internet me case me auto cancel ho jaye.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, appStopping);
        try
        {
            while (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                // WebSocket se continuously messages receive ke liye banaya gaya hai
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), linkedCts.Token);

                // agr clinet ne msg close ka diya hai to loop BREAK hoga.
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (WebSocketException wsex)
        {
            Console.WriteLine($"WebSocket disconnected unexpectedly: {wsex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
        finally
        {
            //Remove Soket
            connectionManager.RemoveSocket(userId);

            if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
            {
                try
                {
                    //  Yahan cleanup karo: jaise file close, socket close, memory free
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch (Exception closeEx)
                {
                    Console.WriteLine($"Error while closing socket: {closeEx.Message}");
                }
            }
        }
    }
    else
    {
        await next();
    }
});

app.MapControllers();

try
{
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
    Console.WriteLine(ex.StackTrace);
    throw;
}

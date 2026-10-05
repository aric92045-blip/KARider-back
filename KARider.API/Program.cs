var builder = WebApplication.CreateBuilder(args);

var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(o => o.AddPolicy("web", p =>
    p.WithOrigins(allowedOrigin).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors("web");

app.MapGet("/api/saludo", () => Results.Ok(new { mensaje = "KARider API v1" }));

app.MapGet("/api/secreto", (IConfiguration c) =>
    Results.Ok(new { configurado = !string.IsNullOrEmpty(c["Demo:Secreto"]) }));

app.Run();
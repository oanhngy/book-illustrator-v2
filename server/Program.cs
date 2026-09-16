using server.Endpoints;
using server.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<UserStore>(); //AuthEndpoints
builder.Services.AddSingleton<ProjectStore>(); //ProjectEndpoints

var app = builder.Build();

app.UseCors("AllowFrontendDev"); //UseCors phải đứng trước MApGet

app.MapGet("/api/health", () => Results.Ok(new
{
    status="ok",
    service="server",
    time=DateTime.UtcNow
}));

app.MapAuthEndpoints();
app.MapProjectEndpoints();

app.Run();

using server.Endpoints;
using server.Gemini;
using Microsoft.Extensions.Options;
using server.Storage;
using System.Threading.Channels;
using server.Pipeline;

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

builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName)); //bind các filed k nhạy cảm từ appsettings section Gemini nếu cóm field k có torng appsetting-->giữ default
builder.Services.PostConfigure<GeminiOptions>(o => 
    o.ApiKey=builder.Configuration["GEMINI_API_KEY"] ?? string.Empty);

var useFakeGemini=builder.Configuration.GetValue<bool?>("USE_FAKE_GEMINI") ?? true; //default true=use fake
if(useFakeGemini)
{
    builder.Services.AddSingleton<IGeminiClient, FakeGeminiClient>();
}
else
{
    //fail fast: k key mà gọi thật -->fail khi khởi động
    var apiKey=builder.Configuration["GEMINI_API_KEY"];
    if(string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("USE_FAKE_GEMINI=false (real call) missing field GEMINI_API_KEY");

    //typed client: HttpClient do IHttpClientFactory manage, tự inject vào contructor của GeminiClient
    builder.Services.AddHttpClient<IGeminiClient, GeminiClient>((sp, client) =>
    {
        var options=sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
        client.BaseAddress=new Uri("https://generativelanguage.googleapis.com");
        client.Timeout=TimeSpan.FromSeconds(options.TimeoutSeconds);    
    });
}

builder.Services.AddSingleton<UserStore>(); //AuthEndpoints
builder.Services.AddSingleton<ProjectStore>(); //ProjectEndpoints
builder.Services.AddSingleton(Channel.CreateUnbounded<StepJob>(new UnboundedChannelOptions {SingleReader=true}));
builder.Services.AddScoped<PipelineService>();
builder.Services.AddHostedService<PipelineWorker>();

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

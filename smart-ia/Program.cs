using Microsoft.SemanticKernel;
using smart_ia.Services;
using smart_ia.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<DataProcessService>();
builder.Services.AddScoped<AiInsightService>();
builder.Services.AddScoped<ChartAiService>();
builder.Services.AddSingleton<IDataSanitizerService, DataSanitizerService>();

var llmConfig = builder.Configuration.GetSection("LLM");
var apiKey = llmConfig["ApiKey"];
var modelId = llmConfig["ModelId"];
var endpoint = llmConfig["Endpoint"];

// llm kernel
var kernelBuilder = builder.Services.AddKernel();

if (!string.IsNullOrEmpty(apiKey))
    kernelBuilder.AddOpenAIChatCompletion(
        modelId,
        new Uri(endpoint),
        apiKey
    );

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();

app.Run();
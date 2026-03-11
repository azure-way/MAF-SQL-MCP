using MafDb.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables();

builder.Services.AddSingleton<DatabaseAgent>();

builder.AddAIAgent("SqlAgent", (sp, _) => (AIAgent)sp.GetRequiredService<DatabaseAgent>().Agent);
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();
builder.AddDevUI();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/devui"));
app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();

app.Run();

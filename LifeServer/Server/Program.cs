using Server;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddScoped<DatabaseService>();

// Add background service for match cleanup (disconnect detection)
builder.Services.AddHostedService<MatchCleanupService>();

var app = builder.Build();

// Run database migrations
using (var conn = SqlFunctions.CreateConnection()) {
    SqlFunctions.RunMigrations(conn);
}

// Fail fast if any card JSON is malformed or contains misspelled properties
Utils.ValidateAllCards();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
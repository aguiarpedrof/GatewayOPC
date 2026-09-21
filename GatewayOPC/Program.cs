using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GatewayOPC.Data;
using GatewayOPC.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configuração do DbContext do Entity Framework com PostgreSQL e log dos comandos SQL
var connectionString = builder.Configuration.GetConnectionString("l2mConnection");
builder.Services.AddDbContext<L2mContext>(options =>
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);

        // Habilita a exibição dos comandos SQL reais gerados pelo Entity Framework Core no console
        options.LogTo(message =>
        {
            if (message.Contains("SELECT") || message.Contains("UPDATE") || message.Contains("INSERT"))
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"[SQL Query] {message.Trim()}");
                Console.ResetColor();
            }
        }, LogLevel.Information);
        options.EnableSensitiveDataLogging();
    }
});

// Registro dos serviços
builder.Services.AddSingleton<DatabaseHealthService>();
builder.Services.AddHostedService<GatewayOpcWorker>();


var host = builder.Build();

Console.WriteLine("==========================================================");
Console.WriteLine("🚀 GatewayOPC - Servidor Industrial OPC UA (.NET 10)");
Console.WriteLine("==========================================================");

await host.RunAsync();

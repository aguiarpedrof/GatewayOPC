using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opc.Ua.Configuration;
using GatewayOPC.Data;
using GatewayOPC.OpcServer;

namespace GatewayOPC.Services
{
    public class GatewayOpcWorker : BackgroundService
    {
        private readonly ILogger<GatewayOpcWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private GatewayOpcServer? _opcServer;
        private ApplicationInstance? _application;

        public GatewayOpcWorker(
            ILogger<GatewayOpcWorker> logger,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                string serverName = _configuration.GetValue<string>("OpcUaServer:ServerName") ?? "GatewayOPC Server";
                string appName = _configuration.GetValue<string>("OpcUaServer:ApplicationName") ?? "GatewayOPC";
                string appUri = _configuration.GetValue<string>("OpcUaServer:ApplicationUri") ?? "urn:localhost:GatewayOPC";
                int port = _configuration.GetValue<int>("OpcUaServer:Port", 4840);
                string endpointPath = _configuration.GetValue<string>("OpcUaServer:EndpointPath") ?? "/GatewayOPC";
                int intervalMs = _configuration.GetValue<int>("OpcUaServer:UpdateIntervalMs", 5000);

                _logger.LogInformation("==================================================");
                _logger.LogInformation("Iniciando Servidor OPC UA: {ServerName}", serverName);
                _logger.LogInformation("Endpoint: opc.tcp://0.0.0.0:{Port}{EndpointPath}", port, endpointPath);
                _logger.LogInformation("==================================================");

                // Criação da configuração OPC UA oficial
                var opcConfig = await OpcServerConfig.CreateAsync(appName, appUri, port, endpointPath);

                // Inicialização do servidor através do ApplicationInstance
                _application = new ApplicationInstance
                {
                    ApplicationName = appName,
                    ApplicationType = Opc.Ua.ApplicationType.Server,
                    ApplicationConfiguration = opcConfig
                };

                await _application.CheckApplicationInstanceCertificatesAsync(false, 0);

                _opcServer = new GatewayOpcServer();
                await _application.StartAsync(_opcServer);

                _logger.LogInformation("Servidor OPC UA iniciado e aguardando conexoes de clientes (UaExpert / TMS Server)!");

                // Handler para escrita de variáveis (Comandos vindos do cliente OPC UA para o Banco)
                if (_opcServer.NodeManager != null)
                {
                    _opcServer.NodeManager.VariableWritten += HandleVariableWrittenAsync;
                }

                // Loop de polling do banco de dados para sincronização das variáveis OPC UA
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<L2mContext>();

                        // Consulta dados mais recentes do PostgreSQL
                        var gateway = await dbContext.Gateways.AsNoTracking().OrderBy(g => g.id).FirstOrDefaultAsync(stoppingToken);
                        var trackers = await dbContext.Trackers.AsNoTracking().OrderBy(t => t.id).ToListAsync(stoppingToken);


                        if (_opcServer.NodeManager != null)
                        {
                            _opcServer.NodeManager.UpdateData(gateway, trackers);
                        }

                        _logger.LogDebug("Dados sincronizados com o banco local: Gateway={GatewayFound}, Trackers={Count}",
                            gateway != null, trackers.Count);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Aviso ao sincronizar com banco de dados local: {Message}", ex.Message);
                    }

                    await Task.Delay(intervalMs, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro critico na execucao do servidor OPC UA");
            }
            finally
            {
                if (_application != null)
                {
                    _logger.LogInformation("Encerrando servidor OPC UA...");
                    await _application.StopAsync();
                }
                _opcServer?.Dispose();
            }
        }

        private async Task HandleVariableWrittenAsync(string nodeKey, object value)
        {
            try
            {
                _logger.LogInformation("Comando OPC UA recebido! Variavel: {NodeKey}, Novo Valor: {Value}", nodeKey, value);

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<L2mContext>();

                // Exemplo de escrita no banco: Modo do Gateway
                if (nodeKey == "Gateway_Modo" && short.TryParse(value.ToString(), out short novoModo))
                {
                    var gw = await dbContext.Gateways.FirstOrDefaultAsync();
                    if (gw != null)
                    {
                        gw.modo = novoModo;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Modo do Gateway atualizado no banco para {Modo}", novoModo);
                    }
                }
                // Exemplo de escrita no banco: Target Slope do Gateway
                else if (nodeKey == "Gateway_TargetSlope" && float.TryParse(value.ToString(), out float targetSlope))
                {
                    var gw = await dbContext.Gateways.FirstOrDefaultAsync();
                    if (gw != null)
                    {
                        gw.target_slope = targetSlope;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("TargetSlope do Gateway atualizado no banco para {Slope}", targetSlope);
                    }
                }
                // Exemplo de escrita no banco: Inclinacao Alvo de Tracker especifico
                else if (nodeKey.StartsWith("Tracker_") && nodeKey.EndsWith("_InclinacaoAlvo"))
                {
                    var parts = nodeKey.Split('_');
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int trackerId) && float.TryParse(value.ToString(), out float inclinacaoAlvo))
                    {
                        var tracker = await dbContext.Trackers.FirstOrDefaultAsync(t => t.id == trackerId);
                        if (tracker != null)
                        {
                            tracker.inclinacao_alvo = inclinacaoAlvo;
                            await dbContext.SaveChangesAsync();
                            _logger.LogInformation("InclinacaoAlvo do Tracker {Id} atualizada no banco para {Inclinacao}", trackerId, inclinacaoAlvo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao persistir comando OPC UA no banco de dados para {NodeKey}", nodeKey);
            }
        }
    }
}

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
        private readonly DatabaseHealthService _dbHealthService;
        private GatewayOpcServer? _opcServer;
        private ApplicationInstance? _application;
        private SolarPlantSimulator? _simulator;

        public GatewayOpcWorker(
            ILogger<GatewayOpcWorker> logger,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            DatabaseHealthService dbHealthService)
        {
            _logger = logger;
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _dbHealthService = dbHealthService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                using (var initialScope = _scopeFactory.CreateScope())
                {
                    var initialDbContext = initialScope.ServiceProvider.GetRequiredService<L2mContext>();
                    await _dbHealthService.LogDatabaseStatusAsync(initialDbContext, stoppingToken);
                }

                string serverName = _configuration.GetValue<string>("OpcUaServer:ServerName") ?? "GatewayOPC Server";

                string appName = _configuration.GetValue<string>("OpcUaServer:ApplicationName") ?? "GatewayOPC";
                string appUri = _configuration.GetValue<string>("OpcUaServer:ApplicationUri") ?? "urn:localhost:GatewayOPC";
                int port = _configuration.GetValue<int>("OpcUaServer:Port", 4840);
                string endpointPath = _configuration.GetValue<string>("OpcUaServer:EndpointPath") ?? "/GatewayOPC";
                int intervalMs = _configuration.GetValue<int>("OpcUaServer:UpdateIntervalMs", 2000);
                bool enableSimulation = _configuration.GetValue<bool>("OpcUaServer:EnableSimulation", false);

                _logger.LogInformation("==================================================");
                _logger.LogInformation("Iniciando Servidor OPC UA: {ServerName}", serverName);
                _logger.LogInformation("Endpoint: opc.tcp://0.0.0.0:{Port}{EndpointPath}", port, endpointPath);
                _logger.LogInformation("Modo Simulador de Usina Solar: {Status}", enableSimulation ? "ATIVADO (5 Trackers + 2 Anemômetros)" : "DESATIVADO (Apenas Banco)");
                _logger.LogInformation("==================================================");

                if (enableSimulation)
                {
                    _simulator = new SolarPlantSimulator();
                }

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

                // Handler para escrita de variáveis (Comandos vindos do cliente OPC UA)
                if (_opcServer.NodeManager != null)
                {
                    _opcServer.NodeManager.VariableWritten += HandleVariableWrittenAsync;
                }

                // Loop de sincronização periódica (Simulador / Banco de Dados)
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (enableSimulation && _simulator != null)
                        {
                            var (simGateway, simTrackers, simAnemometers) = _simulator.Step();
                            if (_opcServer.NodeManager != null)
                            {
                                _opcServer.NodeManager.UpdateData(simGateway, simTrackers, simAnemometers);
                            }

                            _logger.LogDebug("Telemetria simulada atualizada: Inclinacao Alvo={Slope}°, Trackers={Count}, Anemometros={AnemoCount}",
                                simGateway.target_slope, simTrackers.Count, simAnemometers.Count);
                        }
                        else
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<L2mContext>();

                            var gateway = await dbContext.Gateways.AsNoTracking().OrderBy(g => g.id).FirstOrDefaultAsync(stoppingToken);
                            var trackers = await dbContext.Trackers.AsNoTracking().OrderBy(t => t.id).ToListAsync(stoppingToken);
                            var anemometros = await dbContext.Anemometros.AsNoTracking().OrderBy(a => a.id).ToListAsync(stoppingToken);

                            if (_opcServer.NodeManager != null)
                            {
                                _opcServer.NodeManager.UpdateData(gateway, trackers, anemometros);
                            }

                            _logger.LogInformation("🔄 PostgreSQL Sincronizado: Gateway ID={GatewayId} (TargetSlope={Slope}°, Modo={Modo}), Trackers={TrackersCount}, Anemometros={AnemoCount}",
                                gateway?.id, gateway?.target_slope, gateway?.modo, trackers.Count, anemometros.Count);
                        }

                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Aviso na atualizacao de dados: {Message}", ex.Message);
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
                _logger.LogInformation("🎯 Comando OPC UA recebido! Variavel: {NodeKey}, Novo Valor: {Value}", nodeKey, value);

                // Aplica comando no simulador em tempo real se ativo
                _simulator?.ApplyCommand(nodeKey, value);

                // Grava no banco de dados local via Entity Framework
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<L2mContext>();

                if (nodeKey == "Gateway_Modo" && short.TryParse(value.ToString(), out short novoModo))
                {
                    var gw = await dbContext.Gateways.FirstOrDefaultAsync();
                    if (gw != null)
                    {
                        gw.modo = novoModo;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Modo do Gateway atualizado no banco PostgreSQL para {Modo}", novoModo);
                    }
                }
                else if (nodeKey == "Gateway_TargetSlope" && float.TryParse(value.ToString(), out float targetSlope))
                {
                    var gw = await dbContext.Gateways.FirstOrDefaultAsync();
                    if (gw != null)
                    {
                        gw.target_slope = targetSlope;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("TargetSlope do Gateway atualizado no banco PostgreSQL para {Slope}°", targetSlope);
                    }
                }
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
                            _logger.LogInformation("InclinacaoAlvo do Tracker {Id} atualizada no banco para {Inclinacao}°", trackerId, inclinacaoAlvo);
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

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GatewayOPC.Data;

namespace GatewayOPC.Services
{
    public class DatabaseHealthService
    {
        private readonly ILogger<DatabaseHealthService> _logger;

        public DatabaseHealthService(ILogger<DatabaseHealthService> logger)
        {
            _logger = logger;
        }

        public async Task LogDatabaseStatusAsync(L2mContext dbContext, CancellationToken cancellationToken = default)
        {
            try
            {
                var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
                if (!canConnect)
                {
                    _logger.LogWarning("⚠️ Não foi possível conectar ao banco de dados PostgreSQL especificado.");
                    return;
                }

                var totalGateways = await dbContext.Gateways.CountAsync(cancellationToken);
                var totalTrackers = await dbContext.Trackers.CountAsync(cancellationToken);
                var totalAnemometros = await dbContext.Anemometros.CountAsync(cancellationToken);

                _logger.LogInformation("==========================================================");
                _logger.LogInformation("📦 Conexão PostgreSQL Verificada com Sucesso:");
                _logger.LogInformation("   • Gateways Cadastrados: {Count}", totalGateways);
                _logger.LogInformation("   • Trackers Cadastrados: {Count}", totalTrackers);
                _logger.LogInformation("   • Anemômetros Cadastrados: {Count}", totalAnemometros);
                _logger.LogInformation("==========================================================");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar tabelas do PostgreSQL local");
            }
        }
    }
}

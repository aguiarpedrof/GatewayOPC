using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GatewayOPC.Data;

namespace GatewayOPC.Services
{
    public static class DatabaseVerificationRunner
    {
        public static async Task RunVerificationAsync(L2mContext dbContext, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("🔍 Verificando leitura de dados reais do PostgreSQL (l2m):");
            Console.WriteLine("----------------------------------------------------------");

            // 1. Gateway
            var gateway = await dbContext.Gateways.AsNoTracking().OrderBy(g => g.id).FirstOrDefaultAsync(cancellationToken);
            if (gateway != null)
            {
                Console.WriteLine($"[Gateway] ID: {gateway.id} | EUI: {gateway.eui} | Modo: {gateway.modo} | TargetSlope: {gateway.target_slope}° | Leitura: {gateway.leitura}");
            }
            else
            {
                Console.WriteLine("[Gateway] Nenhum gateway encontrado.");
            }

            // 2. Trackers
            var trackers = await dbContext.Trackers.AsNoTracking().OrderBy(t => t.id).ToListAsync(cancellationToken);
            Console.WriteLine($"[Trackers] Encontrados {trackers.Count} trackers no banco:");
            foreach (var t in trackers)
            {
                Console.WriteLine($"   • Tracker ID: {t.id} | EUI: {t.eui} | Modo: {t.modo} | Inclinação: {t.inclinacao_atual}° / Alvo: {t.inclinacao_alvo}° | Bateria: {t.tensao_bateria}V | Leitura: {t.leitura}");
            }

            // 3. Anemômetros
            var anemometros = await dbContext.Anemometros.AsNoTracking().OrderBy(a => a.id).ToListAsync(cancellationToken);
            Console.WriteLine($"[Anemômetros] Encontrados {anemometros.Count} anemômetros no banco:");
            foreach (var a in anemometros)
            {
                Console.WriteLine($"   • Anemômetro ID: {a.id} | EUI: {a.eui} | Vento: {a.velocidade_vento} m/s | Direção: {a.direcao_vento}° | Temp: {a.temperatura}°C | Leitura: {a.leitura}");
            }

            // 4. Históricos (Tracker e Anemômetro)
            var trackerHistCount = await dbContext.Tracker_historicos.CountAsync(cancellationToken);
            var anemoHistCount = await dbContext.Anemometro_historicos.CountAsync(cancellationToken);
            Console.WriteLine($"[Histórico] Registros no PostgreSQL: {trackerHistCount} de Trackers | {anemoHistCount} de Anemômetros");
            Console.WriteLine("----------------------------------------------------------");
        }
    }
}

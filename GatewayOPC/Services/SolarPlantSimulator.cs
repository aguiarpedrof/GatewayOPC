using System;
using System.Collections.Generic;
using GatewayOPC.Models;

namespace GatewayOPC.Services
{
    public class SolarPlantSimulator
    {
        private readonly Random _random = new();
        private double _angleStep = -45.0; // Inicia em -45° (Leste)
        private bool _movingEastToWest = true;
        private double _baseWindSpeed = 4.5; // m/s

        public Gateway SimulatedGateway { get; private set; }
        public List<Tracker> SimulatedTrackers { get; private set; } = new();
        public List<Anemometro> SimulatedAnemometers { get; private set; } = new();

        public SolarPlantSimulator()
        {
            SimulatedGateway = new Gateway
            {
                id = 1,
                eui = "GW_SOLAR_PLANT_01",
                id_usuario = "admin",
                status = 0,
                modo = 0, // 0 = Tracking
                target_slope = 0.0f,
                numero_trackers = 5,
                trackers_tracking_state = 5,
                latitude = -23.5505m,
                longitude = -46.6333m,
                ip = "192.168.1.100",
                porta = 4840,
                ativo = true,
                leitura = DateTime.UtcNow
            };

            for (int i = 1; i <= 5; i++)
            {
                SimulatedTrackers.Add(new Tracker
                {
                    id = i,
                    eui = $"TRK_SOLAR_{i:D3}",
                    id_usuario = "admin",
                    gateway_id = 1,
                    status = 0,
                    modo = 0,
                    inclinacao_atual = (float)_angleStep,
                    inclinacao_alvo = (float)_angleStep,
                    tipo_movimento = 1,
                    soc = 95,
                    erro = 0,
                    tensao_bateria = 26.4f,
                    tensao_painel = 39.8f,
                    corrente_bateria = 1.2f,
                    corrente_painel = 8.5f,
                    corrente_motor = 0.0f,
                    temperatura_bateria = 30.5f,
                    temperatura_painel = 42.0f,
                    umidade_bateria = 45.0f,
                    latitude = -23.5505m,
                    longitude = -46.6333m,
                    installation_azimuth = 0.0f,
                    installation_tilt = 0.0f,
                    inverter = "INVERTER_CENTRAL_01",
                    backtracking_distance = 6.0f,
                    slope_east = 0.0f,
                    slope_west = 0.0f,
                    module_width = 2.0f,
                    automatico = true,
                    ativo = true,
                    leitura = DateTime.UtcNow
                });
            }

            for (int i = 1; i <= 2; i++)
            {
                SimulatedAnemometers.Add(new Anemometro
                {
                    id = i,
                    eui = $"ANEMO_SOLAR_{i:D3}",
                    id_usuario = "admin",
                    latitude = -23.5505m,
                    longitude = -46.6333m,
                    ip = $"192.168.1.{150 + i}",
                    porta = 502,
                    id_subcampo = (short)i,
                    periodo_coleta = 10,
                    holding = true,
                    registrador_velocidade = 0,
                    status = 0,
                    velocidade_vento = 4.5f,
                    direcao_vento = 135.0f,
                    temperatura = 28.5f,
                    pressao = 1013.2f,
                    umidade = 55,
                    ativo = true,
                    leitura = DateTime.UtcNow
                });
            }
        }

        public (Gateway gateway, List<Tracker> trackers, List<Anemometro> anemometers) Step()
        {
            var now = DateTime.UtcNow;

            // Simula o movimento do sol ao longo do ciclo (-45° a +45°)
            if (_movingEastToWest)
            {
                _angleStep += 0.5;
                if (_angleStep >= 45.0) _movingEastToWest = false;
            }
            else
            {
                _angleStep -= 0.5;
                if (_angleStep <= -45.0) _movingEastToWest = true;
            }

            // Atualiza Gateway
            SimulatedGateway.leitura = now;
            SimulatedGateway.target_slope = (float)Math.Round(_angleStep, 1);

            // Atualiza cada Tracker com variações realistas de sensores
            for (int i = 0; i < SimulatedTrackers.Count; i++)
            {
                var trk = SimulatedTrackers[i];
                trk.leitura = now;

                // Suave ruído de leitura de acelerômetro / inclinômetro
                double noise = (_random.NextDouble() - 0.5) * 0.2;
                trk.inclinacao_alvo = SimulatedGateway.target_slope;
                trk.inclinacao_atual = (float)Math.Round(_angleStep + noise, 2);

                // Telemetria elétrica realista
                trk.tensao_painel = (float)Math.Round(38.0 + (_random.NextDouble() * 3.5), 1); // 38.0V a 41.5V
                trk.tensao_bateria = (float)Math.Round(25.8 + (_random.NextDouble() * 1.2), 1); // 25.8V a 27.0V
                trk.corrente_painel = (float)Math.Round(7.5 + (_random.NextDouble() * 2.0), 2); // 7.5A a 9.5A
                trk.corrente_bateria = (float)Math.Round(1.0 + (_random.NextDouble() * 0.8), 2); // 1.0A a 1.8A
                trk.corrente_motor = Math.Abs(trk.inclinacao_alvo.Value - trk.inclinacao_atual.Value) > 0.1f ? 1.8f : 0.0f;
                trk.temperatura_bateria = (float)Math.Round(29.0 + (_random.NextDouble() * 3.0), 1); // 29°C a 32°C
                trk.temperatura_painel = (float)Math.Round(40.0 + (_random.NextDouble() * 5.0), 1); // 40°C a 45°C
                trk.tipo_movimento = (short)(_movingEastToWest ? 2 : 1); // 1=Leste, 2=Oeste
            }

            // Simula dinâmica do vento e variáveis climáticas para Anemômetros
            double windNoise = (_random.NextDouble() - 0.5) * 1.5;
            _baseWindSpeed = Math.Clamp(_baseWindSpeed + windNoise * 0.2, 2.0, 15.0);

            for (int i = 0; i < SimulatedAnemometers.Count; i++)
            {
                var anemo = SimulatedAnemometers[i];
                anemo.leitura = now;
                anemo.velocidade_vento = (float)Math.Round(_baseWindSpeed + ((_random.NextDouble() - 0.5) * 1.0), 1);
                anemo.direcao_vento = (float)Math.Round(135.0 + ((_random.NextDouble() - 0.5) * 20.0), 1); // 125° a 145° (Sudeste)
                anemo.temperatura = (float)Math.Round(27.0 + (_random.NextDouble() * 3.0), 1); // 27°C a 30°C
                anemo.pressao = (float)Math.Round(1012.0 + (_random.NextDouble() * 2.0), 1); // 1012 a 1014 hPa
                anemo.umidade = (short)(50 + _random.Next(0, 15)); // 50% a 65%
            }

            return (SimulatedGateway, SimulatedTrackers, SimulatedAnemometers);
        }

        public void ApplyCommand(string nodeKey, object value)
        {
            if (nodeKey == "Gateway_Modo" && short.TryParse(value.ToString(), out short modo))
            {
                SimulatedGateway.modo = modo;
                foreach (var t in SimulatedTrackers) t.modo = modo;
            }
            else if (nodeKey == "Gateway_TargetSlope" && float.TryParse(value.ToString(), out float slope))
            {
                SimulatedGateway.target_slope = slope;
                _angleStep = slope;
            }
            else if (nodeKey.StartsWith("Tracker_") && nodeKey.EndsWith("_InclinacaoAlvo"))
            {
                var parts = nodeKey.Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[1], out int id) && float.TryParse(value.ToString(), out float alvo))
                {
                    var trk = SimulatedTrackers.Find(t => t.id == id);
                    if (trk != null) trk.inclinacao_alvo = alvo;
                }
            }
        }
    }
}

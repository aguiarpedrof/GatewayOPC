# 🚀 GatewayOPC — Servidor OPC UA Industrial (.NET 10)

O **GatewayOPC** é um middleware industrial de comunicação baseado no padrão **OPC UA (Unified Architecture)** desenvolvido em **C# .NET 10**.

Ele atua como uma ponte bidirecional entre o banco de dados local (**PostgreSQL**) do Gateway e sistemas de supervisão central (**TMS Server**), expondo dados de telemetria em tempo real e recebendo comandos de operação de rastreadores solares (*trackers*).

---

## 🏗️ Arquitetura e Fluxo de Dados

```
┌─────────────────┐           OPC UA           ┌─────────────────┐
│   TMS Server    │ ◄────────────────────────► │   GatewayOPC    │
│  (Central TMS)  │  (opc.tcp://host:4840)     │  (OPC UA Server)│
└─────────────────┘                            └────────┬────────┘
                                                        │ Entity Framework Core
                                                        │ (Npgsql - PostgreSQL)
                                                        ▼
                                               ┌─────────────────┐
                                               │ Banco de Dados  │
                                               │     (PostgreSQL)│
                                               └────────┬────────┘
                                                        │
                                                        ▼
                                               ┌─────────────────┐
                                               │ GatewayControl  │
                                               │(Controle Físico)│
                                               └─────────────────┘
```

### 1. Fluxo de Leitura (Telemetria)
- O `GatewayOpcWorker` realiza consultas periódicas ao banco PostgreSQL via `L2mContext` (Entity Framework Core).
- Os dados de telemetria (inclinação atual, tensão da bateria, tensão dos painéis solares, correntes, status, etc.) são atualizados nos nós do **AddressSpace** OPC UA em tempo real.
- Clientes OPC UA inscritos recebem as atualizações instantaneamente.

### 2. Fluxo de Escrita (Comandos)
- Ao receber comandos de escrita via OPC UA (como alteração de `Modo`, `TargetSlope` ou `InclinacaoAlvo`), o servidor intercepta o evento em `GatewayNodeManager`.
- A alteração é validada e persistida no banco de dados local via Entity Framework Core.
- O serviço físico (`GatewayControl`) lê a alteração no banco e executa o movimento nos motores dos rastreadores solares.

---

## 🌲 Estrutura do AddressSpace (Árvore OPC UA)

O servidor publica a hierarquia dentro de `Objects/GatewaySystem`:

```text
Root
 └── Objects
       └── GatewaySystem
             ├── GatewayInfo
             │     ├── Status (Leitura)
             │     ├── DescricaoStatus (Leitura)
             │     ├── Modo (Leitura/Escrita)
             │     ├── DescricaoModo (Leitura)
             │     ├── TargetSlope (Leitura/Escrita)
             │     ├── NumeroTrackers (Leitura)
             │     ├── TrackersTrackingState (Leitura)
             │     ├── Conectado (Leitura)
             │     ├── Ativo (Leitura)
             │     ├── Automatico (Leitura)
             │     ├── EUI (Leitura)
             │     ├── IP (Leitura)
             │     ├── Porta (Leitura)
             │     ├── VDAL (Leitura)
             │     ├── VSTOW (Leitura)
             │     ├── CleaningSlope (Leitura)
             │     ├── NightSlope (Leitura)
             │     └── UltimaLeitura (Leitura)
             ├── Trackers
             │     └── Tracker_{id} ({eui})
             │           ├── Id, EUI, Conectado, Ativo, Automatico (Leitura)
             │           ├── InclinacaoAtual (Leitura), InclinacaoAlvo (Leitura/Escrita)
             │           ├── Pitch, TipoMovimento, DescricaoMovimento (Leitura)
             │           ├── Modo (Leitura/Escrita), DescricaoModo (Leitura)
             │           ├── Status, DescricaoStatus, Erro, ComErro (Leitura)
             │           ├── TensaoBateria, CorrenteBateria, SOC (Leitura)
             │           ├── TensaoPainel, CorrentePainel, CorrenteMotor (Leitura)
             │           ├── TemperaturaBateria, UmidadeBateria, TemperaturaPainel (Leitura)
             │           ├── RSSI, SNR, UltimaLeitura (Leitura)
             │           └── Historico
             │                 ├── TotalRegistros (Leitura)
             │                 ├── UltimaLeitura (Leitura)
             │                 ├── Inclinacao, InclinacaoAlvo (Leitura)
             │                 ├── TensaoBateria, CorrenteBateria, SOC (Leitura)
             │                 ├── TensaoPainel, CorrentePainel, CorrenteMotor (Leitura)
             │                 ├── TemperaturaBateria, TemperaturaPainel (Leitura)
             │                 └── Modo, Status, Erro (Leitura)
             └── Anemometros
                   └── Anemometro_{id} ({eui})
                         ├── Id, EUI, Ativo, IP, Porta, PeriodoColeta (Leitura)
                         ├── VelocidadeVento, DirecaoVento (Leitura)
                         ├── Temperatura, Pressao, Umidade (Leitura)
                         ├── Status, DescricaoStatus, UltimaLeitura (Leitura)
                         └── Historico
                               ├── TotalRegistros (Leitura)
                               ├── UltimaLeitura (Leitura)
                               ├── VelocidadeVento, DirecaoVento (Leitura)
                               ├── Temperatura, Pressao, Umidade (Leitura)
                               └── Status (Leitura)
```

> **Nota sobre Históricos:** No modelo industrial do sistema, apenas os **Trackers** (`tracker.tracker_historico`) e **Anemômetros** (`tracker.anemometro_historico`) mantêm registros temporais contínuos de telemetria no banco de dados. O **Gateway** não possui tabela de histórico pois representa a unidade de coordenação e configuração central em tempo real da usina.

---

## 🛠️ Tecnologias Utilizadas

- **.NET 10** (C# 13)
- **OPC Foundation UA .NET Standard SDK** (`OPCFoundation.NetStandard.Opc.Ua.Server` v1.5.x)
- **Entity Framework Core 10** (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Microsoft Extensions Hosting & Configuration**
- **Docker** (Containerização Linux multiplataforma)

---

## ⚙️ Configuração (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "l2mConnection": "Host=localhost;User ID=postgres;Password=@L2m2025;Timeout=30;Database=l2m"
  },
  "OpcUaServer": {
    "ServerName": "GatewayOPC Server",
    "ApplicationName": "GatewayOPC",
    "ApplicationUri": "urn:localhost:GatewayOPC",
    "Port": 4840,
    "EndpointPath": "/GatewayOPC",
    "UpdateIntervalMs": 2000,
    "EnableSimulation": true
  }
}
```

### ☀️ Modo Simulador de Usina Solar (`EnableSimulation: true`)
Para testes e demonstrações sem necessidade de hardware físico:
- Gera dados dinâmicos em tempo real para **5 Trackers solares** e **2 Anemômetros**.
- Simula a curva de rastreamento solar (inclinação de `-45°` Leste a `+45°` Oeste).
- Simula telemetria elétrica (tensão de 38V a 42V, corrente de carga, temperatura da bateria).
- Simula telemetria ambiental e dinâmica de vento (velocidade de 3 a 15 m/s, direção, pressão e umidade).
- Responde a comandos de escrita (`Modo`, `TargetSlope`, `InclinacaoAlvo`) em tempo real.



---

## 🚀 Como Executar

### 1. Execução Local
Certifique-se de que o .NET 10 SDK está instalado e o PostgreSQL está em execução:

```bash
cd GatewayOPC
dotnet run
```

### 2. Execução com Docker

Construa a imagem:
```bash
docker build -t gateway-opc -f GatewayOPC/Dockerfile .
```

Execute o container expondo a porta industrial `4840`:
```bash
docker run -d -p 4840:4840 --name gateway-opc gateway-opc
```

---

## 🧪 Testando com Clientes OPC UA

Utilize qualquer cliente OPC UA padrão da indústria (ex: **UaExpert** ou **Prosys OPC UA Client**):

- **Endpoint URL**: `opc.tcp://localhost:4840/GatewayOPC`
- **Security Policy**: `None` ou `Basic256Sha256`
- **Authentication**: `Anonymous`

---

## 🗄️ Integração com Banco de Dados e Tradução SQL (LINQ to Entities)

O **GatewayOPC** utiliza o **Entity Framework Core 10** como Object-Relational Mapper (ORM). Isso significa que, em vez de escrever strings SQL puras espalhadas pelo código, utilizamos expressões tipadas em C# (LINQ), que o provedor `Npgsql` traduz automaticamente para SQL nativo do PostgreSQL em tempo de execução:

### 1. Consulta do Gateway:
- **Código C# (LINQ):**
  ```csharp
  var gateway = await dbContext.Gateways.AsNoTracking().OrderBy(g => g.id).FirstOrDefaultAsync();
  ```
- **SQL Gerado pelo EF Core:**
  ```sql
  SELECT g.id, g.applicationid, g.ativo, g.atmosrefract, g.automatico, g.axis_missalign, 
         g.azmrotation, g.cleaning_slope, g.deltat, g.deltaut1, g.elevation, g.eui, 
         g.id_subcampo, g.id_usuario, g.ip, g.latitude, g.leitura, g.longitude, 
         g.max_slope_east, g.max_slope_west, g.modo, g.night_slope, g.numero_trackers, 
         g.porta, g.restore_time, g.safe_position_dal_e, g.safe_position_dal_w, 
         g.safe_position_stow, g.senha, g.slope, g.status, g.target_slope, 
         g.trackers_tracking_state, g.usuario, g.vdal, g.vstow
  FROM tracker.gateway AS g
  ORDER BY g.id
  LIMIT 1;
  ```

### 2. Consulta dos Trackers:
- **Código C# (LINQ):**
  ```csharp
  var trackers = await dbContext.Trackers.AsNoTracking().OrderBy(t => t.id).ToListAsync();
  ```
- **SQL Gerado pelo EF Core:**
  ```sql
  SELECT t.id, t.installation_azimuth, t.backtracking_distance, t.corrente_bateria, 
         t.corrente_motor, t.corrente_painel, t.erro, t.eui, t.gateway_id, 
         t.inclinacao_alvo, t.inclinacao_atual, t.inverter, t.latitude, t.leitura, 
         t.longitude, t.modo, t.module_width, t.pitch, t.rssi, t.slope_east, 
         t.slope_west, t.snr, t.soc, t.status, t.temperatura_bateria, 
         t.temperatura_painel, t.tensao_bateria, t.tensao_painel, t.tipo_movimento, 
         t.umidade_bateria
  FROM tracker.tracker AS t
  ORDER BY t.id;
  ```

### 3. Consulta dos Anemômetros:
- **Código C# (LINQ):**
  ```csharp
  var anemometros = await dbContext.Anemometros.AsNoTracking().OrderBy(a => a.id).ToListAsync();
  ```
- **SQL Gerado pelo EF Core:**
  ```sql
  SELECT a.id, a.ativo, a.direcao_vento, a.eui, a.holding, a.id_subcampo, 
         a.id_usuario, a.ip, a.latitude, a.leitura, a.longitude, a.periodo_coleta, 
         a.porta, a.pressao, a.registrador_direcao, a.registrador_pressao, 
         a.registrador_temperatura, a.registrador_umidade, a.registrador_velocidade, 
         a.status, a.temperatura, a.umidade, a.velocidade_vento
  FROM tracker.anemometro AS a
  ORDER BY a.id;
  ```

### 4. Gravação de Comandos (OPC UA -> PostgreSQL):
- **Código C#:**
  ```csharp
  var gw = await dbContext.Gateways.FirstOrDefaultAsync();
  gw.modo = novoModo;
  await dbContext.SaveChangesAsync();
  ```
- **SQL Gerado pelo EF Core:**
  ```sql
  UPDATE tracker.gateway 
  SET modo = @novoModo 
  WHERE id = @id;
  ```


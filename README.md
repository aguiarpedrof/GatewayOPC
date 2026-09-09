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
             │     ├── Modo (Leitura/Escrita)
             │     ├── TargetSlope (Leitura/Escrita)
             │     ├── NumeroTrackers (Leitura)
             │     ├── UltimaLeitura (Leitura)
             │     └── DescricaoStatus (Leitura)
             └── Trackers
                   └── Tracker_{id} ({eui})
                         ├── InclinacaoAtual (Leitura)
                         ├── InclinacaoAlvo (Leitura/Escrita)
                         ├── Modo (Leitura/Escrita)
                         ├── TensaoBateria (Leitura)
                         ├── TensaoPainel (Leitura)
                         ├── CorrenteMotor (Leitura)
                         ├── CorrenteBateria (Leitura)
                         ├── TemperaturaBateria (Leitura)
                         ├── Status (Leitura)
                         ├── DescricaoStatus (Leitura)
                         └── UltimaLeitura (Leitura)
```

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
    "UpdateIntervalMs": 5000
  }
}
```

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

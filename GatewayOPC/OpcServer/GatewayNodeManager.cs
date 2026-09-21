using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Server;
using GatewayOPC.Models;

namespace GatewayOPC.OpcServer
{
    public class GatewayNodeManager : CustomNodeManager2
    {
        public const string NamespaceUri = "http://opcfoundation.org/GatewayOPC";

        // Dicionários para manter referência direta aos nós criados no AddressSpace
        private readonly Dictionary<string, BaseDataVariableState> _variables = new();
        private readonly HashSet<int> _registeredTrackerIds = new();
        private readonly HashSet<int> _registeredAnemometerIds = new();
        private readonly object _lock = new();
        private FolderState? _trackersFolder;
        private FolderState? _anemometersFolder;

        // Evento disparado quando um cliente OPC UA escreve em uma variável
        public event Func<string, object, Task>? VariableWritten;

        public GatewayNodeManager(IServerInternal server, ApplicationConfiguration configuration)
            : base(server, configuration, NamespaceUri)
        {
            SystemContext.NodeIdFactory = this;
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                base.CreateAddressSpace(externalReferences);

                ushort namespaceIndex = NamespaceIndexes[0];

                // 1. Cria a pasta raiz do sistema (GatewaySystem)
                var rootFolder = new FolderState(null)
                {
                    SymbolicName = "GatewaySystem",
                    ReferenceTypeId = ReferenceTypeIds.Organizes,
                    TypeDefinitionId = ObjectTypeIds.FolderType,
                    NodeId = new NodeId("GatewaySystem", namespaceIndex),
                    BrowseName = new QualifiedName("GatewaySystem", namespaceIndex),
                    DisplayName = new LocalizedText("GatewaySystem"),
                    WriteMask = AttributeWriteMask.None,
                    UserWriteMask = AttributeWriteMask.None,
                    EventNotifier = EventNotifiers.None
                };

                // Conecta a pasta raiz à pasta padrão 'Objects' do OPC UA via referências externas
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out IList<IReference>? references) || references == null)
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }
                references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, rootFolder.NodeId));
                rootFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);

                AddPredefinedNode(SystemContext, rootFolder);

                // 2. Cria pasta de informações do Gateway
                FolderState gatewayFolder = CreateFolder(rootFolder, "GatewayInfo", "GatewayInfo");
                CreateVariable(gatewayFolder, "Gateway_Status", "Status", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
                CreateVariable(gatewayFolder, "Gateway_DescricaoStatus", "DescricaoStatus", DataTypeIds.String, ValueRanks.Scalar, "Iniciando...");
                CreateVariable(gatewayFolder, "Gateway_Modo", "Modo", DataTypeIds.Int16, ValueRanks.Scalar, (short)0, isWritable: true);
                CreateVariable(gatewayFolder, "Gateway_DescricaoModo", "DescricaoModo", DataTypeIds.String, ValueRanks.Scalar, "Tracking");
                CreateVariable(gatewayFolder, "Gateway_TargetSlope", "TargetSlope", DataTypeIds.Float, ValueRanks.Scalar, 0.0f, isWritable: true);
                CreateVariable(gatewayFolder, "Gateway_NumeroTrackers", "NumeroTrackers", DataTypeIds.Int32, ValueRanks.Scalar, 0);
                CreateVariable(gatewayFolder, "Gateway_TrackersTrackingState", "TrackersTrackingState", DataTypeIds.Int32, ValueRanks.Scalar, 0);
                CreateVariable(gatewayFolder, "Gateway_Conectado", "Conectado", DataTypeIds.Boolean, ValueRanks.Scalar, false);
                CreateVariable(gatewayFolder, "Gateway_Ativo", "Ativo", DataTypeIds.Boolean, ValueRanks.Scalar, true);
                CreateVariable(gatewayFolder, "Gateway_Automatico", "Automatico", DataTypeIds.Boolean, ValueRanks.Scalar, false);
                CreateVariable(gatewayFolder, "Gateway_EUI", "EUI", DataTypeIds.String, ValueRanks.Scalar, "");
                CreateVariable(gatewayFolder, "Gateway_IP", "IP", DataTypeIds.String, ValueRanks.Scalar, "");
                CreateVariable(gatewayFolder, "Gateway_Porta", "Porta", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
                CreateVariable(gatewayFolder, "Gateway_VDAL", "VDAL", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
                CreateVariable(gatewayFolder, "Gateway_VSTOW", "VSTOW", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
                CreateVariable(gatewayFolder, "Gateway_CleaningSlope", "CleaningSlope", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
                CreateVariable(gatewayFolder, "Gateway_NightSlope", "NightSlope", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
                CreateVariable(gatewayFolder, "Gateway_UltimaLeitura", "UltimaLeitura", DataTypeIds.DateTime, ValueRanks.Scalar, DateTime.UtcNow);

                // 3. Cria pasta para os Trackers
                _trackersFolder = CreateFolder(rootFolder, "Trackers", "Trackers");
                CreateTrackerNodes(_trackersFolder, 1, "TRACKER_01");

                // 4. Cria pasta para os Anemômetros
                _anemometersFolder = CreateFolder(rootFolder, "Anemometros", "Anemometros");
                CreateAnemometerNodes(_anemometersFolder, 1, "ANEMOMETRO_01");

                AddRootNotifier(rootFolder);

            }
        }

        private FolderState CreateFolder(NodeState parent, string path, string name)
        {
            ushort namespaceIndex = NamespaceIndexes[0];
            var folder = new FolderState(parent)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypeIds.Organizes,
                TypeDefinitionId = ObjectTypeIds.FolderType,
                NodeId = new NodeId(path, namespaceIndex),
                BrowseName = new QualifiedName(name, namespaceIndex),
                DisplayName = new LocalizedText(name),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                EventNotifier = EventNotifiers.None
            };

            parent.AddChild(folder);
            AddPredefinedNode(SystemContext, folder);
            return folder;
        }

        public BaseDataVariableState CreateVariable(
            NodeState parent,
            string path,
            string name,
            NodeId dataType,
            int valueRank,
            object initialValue,
            bool isWritable = false)
        {
            ushort namespaceIndex = NamespaceIndexes[0];
            var variable = new BaseDataVariableState(parent)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypeIds.Organizes,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                NodeId = new NodeId(path, namespaceIndex),
                BrowseName = new QualifiedName(name, namespaceIndex),
                DisplayName = new LocalizedText(name),
                WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
                UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
                DataType = dataType,
                ValueRank = valueRank,
                AccessLevel = isWritable ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                UserAccessLevel = isWritable ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                Historizing = false,
                Value = initialValue,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow
            };

            if (isWritable)
            {
                variable.OnSimpleWriteValue = OnVariableWritten;
            }

            parent.AddChild(variable);
            AddPredefinedNode(SystemContext, variable);

            lock (_lock)
            {
                _variables[path] = variable;
            }

            return variable;
        }

        private ServiceResult OnVariableWritten(ISystemContext context, NodeState node, ref object value)
        {
            string nodeKey = node.NodeId.Identifier?.ToString() ?? "";
            object val = value;
            _ = Task.Run(async () =>
            {
                if (VariableWritten != null)
                {
                    await VariableWritten.Invoke(nodeKey, val);
                }
            });
            return ServiceResult.Good;
        }

        private void CreateTrackerNodes(FolderState parent, int trackerId, string eui)
        {
            string prefix = $"Tracker_{trackerId}";
            var trackerFolder = CreateFolder(parent, $"{prefix}_Folder", $"Tracker_{trackerId} ({eui})");

            // Identificação e Conectividade
            CreateVariable(trackerFolder, $"{prefix}_Id", "Id", DataTypeIds.Int32, ValueRanks.Scalar, trackerId);
            CreateVariable(trackerFolder, $"{prefix}_EUI", "EUI", DataTypeIds.String, ValueRanks.Scalar, eui);
            CreateVariable(trackerFolder, $"{prefix}_Conectado", "Conectado", DataTypeIds.Boolean, ValueRanks.Scalar, false);
            CreateVariable(trackerFolder, $"{prefix}_Ativo", "Ativo", DataTypeIds.Boolean, ValueRanks.Scalar, true);
            CreateVariable(trackerFolder, $"{prefix}_Automatico", "Automatico", DataTypeIds.Boolean, ValueRanks.Scalar, false);

            // Posicionamento e Movimentação
            CreateVariable(trackerFolder, $"{prefix}_InclinacaoAtual", "InclinacaoAtual", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_InclinacaoAlvo", "InclinacaoAlvo", DataTypeIds.Float, ValueRanks.Scalar, 0.0f, isWritable: true);
            CreateVariable(trackerFolder, $"{prefix}_Pitch", "Pitch", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_TipoMovimento", "TipoMovimento", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(trackerFolder, $"{prefix}_DescricaoMovimento", "DescricaoMovimento", DataTypeIds.String, ValueRanks.Scalar, "Parado");

            // Modos e Status Operacional
            CreateVariable(trackerFolder, $"{prefix}_Modo", "Modo", DataTypeIds.Int16, ValueRanks.Scalar, (short)0, isWritable: true);
            CreateVariable(trackerFolder, $"{prefix}_DescricaoModo", "DescricaoModo", DataTypeIds.String, ValueRanks.Scalar, "Tracking");
            CreateVariable(trackerFolder, $"{prefix}_Status", "Status", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(trackerFolder, $"{prefix}_DescricaoStatus", "DescricaoStatus", DataTypeIds.String, ValueRanks.Scalar, "Normal");
            CreateVariable(trackerFolder, $"{prefix}_Erro", "Erro", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(trackerFolder, $"{prefix}_ComErro", "ComErro", DataTypeIds.Boolean, ValueRanks.Scalar, false);

            // Grandezas Elétricas e Bateria
            CreateVariable(trackerFolder, $"{prefix}_TensaoBateria", "TensaoBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_CorrenteBateria", "CorrenteBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_SOC", "SOC", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(trackerFolder, $"{prefix}_TensaoPainel", "TensaoPainel", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_CorrentePainel", "CorrentePainel", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_CorrenteMotor", "CorrenteMotor", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);

            // Temperatura e Sensores
            CreateVariable(trackerFolder, $"{prefix}_TemperaturaBateria", "TemperaturaBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_UmidadeBateria", "UmidadeBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_TemperaturaPainel", "TemperaturaPainel", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_RSSI", "RSSI", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(trackerFolder, $"{prefix}_SNR", "SNR", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(trackerFolder, $"{prefix}_UltimaLeitura", "UltimaLeitura", DataTypeIds.DateTime, ValueRanks.Scalar, DateTime.UtcNow);

            // Subpasta de Histórico do Tracker
            var historicoFolder = CreateFolder(trackerFolder, $"{prefix}_Historico_Folder", "Historico");
            CreateVariable(historicoFolder, $"{prefix}_Historico_TotalRegistros", "TotalRegistros", DataTypeIds.Int32, ValueRanks.Scalar, 0);
            CreateVariable(historicoFolder, $"{prefix}_Historico_UltimaLeitura", "UltimaLeitura", DataTypeIds.DateTime, ValueRanks.Scalar, DateTime.UtcNow);
            CreateVariable(historicoFolder, $"{prefix}_Historico_Inclinacao", "Inclinacao", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_InclinacaoAlvo", "InclinacaoAlvo", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_TensaoBateria", "TensaoBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_CorrenteBateria", "CorrenteBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_TensaoPainel", "TensaoPainel", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_CorrentePainel", "CorrentePainel", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_CorrenteMotor", "CorrenteMotor", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_SOC", "SOC", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(historicoFolder, $"{prefix}_Historico_TemperaturaBateria", "TemperaturaBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_TemperaturaPainel", "TemperaturaPainel", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_Modo", "Modo", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(historicoFolder, $"{prefix}_Historico_Status", "Status", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(historicoFolder, $"{prefix}_Historico_Erro", "Erro", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);

            _registeredTrackerIds.Add(trackerId);
        }

        private void CreateAnemometerNodes(FolderState parent, int anemometerId, string eui)
        {
            string prefix = $"Anemometro_{anemometerId}";
            var anemometerFolder = CreateFolder(parent, $"{prefix}_Folder", $"Anemometro_{anemometerId} ({eui})");

            // Identificação e Comunicação
            CreateVariable(anemometerFolder, $"{prefix}_Id", "Id", DataTypeIds.Int32, ValueRanks.Scalar, anemometerId);
            CreateVariable(anemometerFolder, $"{prefix}_EUI", "EUI", DataTypeIds.String, ValueRanks.Scalar, eui);
            CreateVariable(anemometerFolder, $"{prefix}_Ativo", "Ativo", DataTypeIds.Boolean, ValueRanks.Scalar, true);
            CreateVariable(anemometerFolder, $"{prefix}_IP", "IP", DataTypeIds.String, ValueRanks.Scalar, "");
            CreateVariable(anemometerFolder, $"{prefix}_Porta", "Porta", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(anemometerFolder, $"{prefix}_PeriodoColeta", "PeriodoColeta", DataTypeIds.Int32, ValueRanks.Scalar, 0);

            // Variáveis Ambientais
            CreateVariable(anemometerFolder, $"{prefix}_VelocidadeVento", "VelocidadeVento", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(anemometerFolder, $"{prefix}_DirecaoVento", "DirecaoVento", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(anemometerFolder, $"{prefix}_Temperatura", "Temperatura", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(anemometerFolder, $"{prefix}_Pressao", "Pressao", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(anemometerFolder, $"{prefix}_Umidade", "Umidade", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(anemometerFolder, $"{prefix}_Status", "Status", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(anemometerFolder, $"{prefix}_DescricaoStatus", "DescricaoStatus", DataTypeIds.String, ValueRanks.Scalar, "Normal");
            CreateVariable(anemometerFolder, $"{prefix}_UltimaLeitura", "UltimaLeitura", DataTypeIds.DateTime, ValueRanks.Scalar, DateTime.UtcNow);

            // Subpasta de Histórico do Anemômetro
            var historicoFolder = CreateFolder(anemometerFolder, $"{prefix}_Historico_Folder", "Historico");
            CreateVariable(historicoFolder, $"{prefix}_Historico_TotalRegistros", "TotalRegistros", DataTypeIds.Int32, ValueRanks.Scalar, 0);
            CreateVariable(historicoFolder, $"{prefix}_Historico_UltimaLeitura", "UltimaLeitura", DataTypeIds.DateTime, ValueRanks.Scalar, DateTime.UtcNow);
            CreateVariable(historicoFolder, $"{prefix}_Historico_VelocidadeVento", "VelocidadeVento", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_DirecaoVento", "DirecaoVento", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_Temperatura", "Temperatura", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_Pressao", "Pressao", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(historicoFolder, $"{prefix}_Historico_Umidade", "Umidade", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(historicoFolder, $"{prefix}_Historico_Status", "Status", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);

            _registeredAnemometerIds.Add(anemometerId);
        }

        // Método chamado pelo Worker para atualizar dados em tempo real no AddressSpace
        public void UpdateData(
            Gateway? gateway, 
            IEnumerable<Tracker>? trackers, 
            IEnumerable<Anemometro>? anemometros = null,
            IDictionary<int, TrackerHistorico>? latestTrackerHistories = null,
            IDictionary<int, int>? trackerHistoryCounts = null,
            IDictionary<int, AnemometroHistorico>? latestAnemoHistories = null,
            IDictionary<int, int>? anemoHistoryCounts = null)
        {
            lock (_lock)
            {
                if (gateway != null)
                {
                    UpdateVariableValue("Gateway_Status", gateway.status ?? 0);
                    UpdateVariableValue("Gateway_DescricaoStatus", gateway.descricaoStatus);
                    UpdateVariableValue("Gateway_Modo", gateway.modo);
                    UpdateVariableValue("Gateway_DescricaoModo", gateway.descricaoModo);
                    UpdateVariableValue("Gateway_TargetSlope", gateway.target_slope);
                    UpdateVariableValue("Gateway_NumeroTrackers", gateway.numero_trackers ?? 0);
                    UpdateVariableValue("Gateway_TrackersTrackingState", gateway.trackers_tracking_state ?? 0);
                    UpdateVariableValue("Gateway_Conectado", gateway.conectado);
                    UpdateVariableValue("Gateway_Ativo", gateway.ativo);
                    UpdateVariableValue("Gateway_Automatico", gateway.automatico);
                    UpdateVariableValue("Gateway_EUI", gateway.eui ?? "");
                    UpdateVariableValue("Gateway_IP", gateway.ip ?? "");
                    UpdateVariableValue("Gateway_Porta", gateway.porta);
                    UpdateVariableValue("Gateway_VDAL", gateway.vdal);
                    UpdateVariableValue("Gateway_VSTOW", gateway.vstow);
                    UpdateVariableValue("Gateway_CleaningSlope", gateway.cleaning_slope);
                    UpdateVariableValue("Gateway_NightSlope", gateway.night_slope);
                    UpdateVariableValue("Gateway_UltimaLeitura", gateway.leitura ?? DateTime.UtcNow);
                }

                if (trackers != null && _trackersFolder != null)
                {
                    foreach (var tracker in trackers)
                    {
                        // Se o tracker do banco ainda não tem nós criados no AddressSpace, cria agora
                        if (!_registeredTrackerIds.Contains(tracker.id))
                        {
                            CreateTrackerNodes(_trackersFolder, tracker.id, tracker.eui ?? $"ID_{tracker.id}");
                        }

                        string prefix = $"Tracker_{tracker.id}";
                        UpdateVariableValue($"{prefix}_Id", tracker.id);
                        UpdateVariableValue($"{prefix}_EUI", tracker.eui ?? "");
                        UpdateVariableValue($"{prefix}_Conectado", tracker.conectado);
                        UpdateVariableValue($"{prefix}_Ativo", tracker.ativo);
                        UpdateVariableValue($"{prefix}_Automatico", tracker.automatico);
                        UpdateVariableValue($"{prefix}_InclinacaoAtual", tracker.inclinacao_atual ?? 0.0f);
                        UpdateVariableValue($"{prefix}_InclinacaoAlvo", tracker.inclinacao_alvo ?? 0.0f);
                        UpdateVariableValue($"{prefix}_Pitch", tracker.pitch ?? 0.0f);
                        UpdateVariableValue($"{prefix}_TipoMovimento", tracker.tipo_movimento ?? (short)0);
                        UpdateVariableValue($"{prefix}_DescricaoMovimento", tracker.descricaoMovimento);
                        UpdateVariableValue($"{prefix}_Modo", tracker.modo ?? (short)0);
                        UpdateVariableValue($"{prefix}_DescricaoModo", tracker.descricaoModo);
                        UpdateVariableValue($"{prefix}_Status", tracker.status ?? (short)0);
                        UpdateVariableValue($"{prefix}_DescricaoStatus", tracker.descricaoStatus);
                        UpdateVariableValue($"{prefix}_Erro", tracker.erro ?? (short)0);
                        UpdateVariableValue($"{prefix}_ComErro", tracker.comErro);
                        UpdateVariableValue($"{prefix}_TensaoBateria", tracker.tensao_bateria ?? 0.0f);
                        UpdateVariableValue($"{prefix}_CorrenteBateria", tracker.corrente_bateria ?? 0.0f);
                        UpdateVariableValue($"{prefix}_SOC", tracker.soc ?? (short)0);
                        UpdateVariableValue($"{prefix}_TensaoPainel", tracker.tensao_painel ?? 0.0f);
                        UpdateVariableValue($"{prefix}_CorrentePainel", tracker.corrente_painel ?? 0.0f);
                        UpdateVariableValue($"{prefix}_CorrenteMotor", tracker.corrente_motor ?? 0.0f);
                        UpdateVariableValue($"{prefix}_TemperaturaBateria", tracker.temperatura_bateria ?? 0.0f);
                        UpdateVariableValue($"{prefix}_UmidadeBateria", tracker.umidade_bateria ?? 0.0f);
                        UpdateVariableValue($"{prefix}_TemperaturaPainel", tracker.temperatura_painel ?? 0.0f);
                        UpdateVariableValue($"{prefix}_RSSI", tracker.rssi ?? (short)0);
                        UpdateVariableValue($"{prefix}_SNR", tracker.snr ?? (short)0);
                        UpdateVariableValue($"{prefix}_UltimaLeitura", tracker.leitura ?? DateTime.UtcNow);

                        // Histórico do Tracker
                        int totalHist = trackerHistoryCounts != null && trackerHistoryCounts.TryGetValue(tracker.id, out int count) ? count : 0;
                        UpdateVariableValue($"{prefix}_Historico_TotalRegistros", totalHist);

                        if (latestTrackerHistories != null && latestTrackerHistories.TryGetValue(tracker.id, out var hist) && hist != null)
                        {
                            UpdateVariableValue($"{prefix}_Historico_UltimaLeitura", hist.leitura);
                            UpdateVariableValue($"{prefix}_Historico_Inclinacao", hist.inclinacao_atual);
                            UpdateVariableValue($"{prefix}_Historico_InclinacaoAlvo", hist.inclinacao_alvo);
                            UpdateVariableValue($"{prefix}_Historico_TensaoBateria", hist.tensao_bateria);
                            UpdateVariableValue($"{prefix}_Historico_CorrenteBateria", hist.corrente_bateria);
                            UpdateVariableValue($"{prefix}_Historico_TensaoPainel", hist.tensao_painel);
                            UpdateVariableValue($"{prefix}_Historico_CorrentePainel", hist.corrente_painel);
                            UpdateVariableValue($"{prefix}_Historico_CorrenteMotor", hist.corrente_motor);
                            UpdateVariableValue($"{prefix}_Historico_SOC", hist.soc);
                            UpdateVariableValue($"{prefix}_Historico_TemperaturaBateria", hist.temperatura_bateria ?? 0.0f);
                            UpdateVariableValue($"{prefix}_Historico_TemperaturaPainel", hist.temperatura_painel ?? 0.0f);
                            UpdateVariableValue($"{prefix}_Historico_Modo", hist.modo);
                            UpdateVariableValue($"{prefix}_Historico_Status", hist.status);
                            UpdateVariableValue($"{prefix}_Historico_Erro", hist.erro);
                        }
                    }
                }

                if (anemometros != null && _anemometersFolder != null)
                {
                    foreach (var anemometro in anemometros)
                    {
                        // Se o anemômetro ainda não tem nós criados no AddressSpace, cria agora
                        if (!_registeredAnemometerIds.Contains(anemometro.id))
                        {
                            CreateAnemometerNodes(_anemometersFolder, anemometro.id, anemometro.eui ?? $"ID_{anemometro.id}");
                        }

                        string prefix = $"Anemometro_{anemometro.id}";
                        UpdateVariableValue($"{prefix}_Id", anemometro.id);
                        UpdateVariableValue($"{prefix}_EUI", anemometro.eui ?? "");
                        UpdateVariableValue($"{prefix}_Ativo", anemometro.ativo);
                        UpdateVariableValue($"{prefix}_IP", anemometro.ip ?? "");
                        UpdateVariableValue($"{prefix}_Porta", anemometro.porta);
                        UpdateVariableValue($"{prefix}_PeriodoColeta", anemometro.periodo_coleta);
                        UpdateVariableValue($"{prefix}_VelocidadeVento", anemometro.velocidade_vento ?? 0.0f);
                        UpdateVariableValue($"{prefix}_DirecaoVento", anemometro.direcao_vento ?? 0.0f);
                        UpdateVariableValue($"{prefix}_Temperatura", anemometro.temperatura ?? 0.0f);
                        UpdateVariableValue($"{prefix}_Pressao", anemometro.pressao ?? 0.0f);
                        UpdateVariableValue($"{prefix}_Umidade", anemometro.umidade ?? (short)0);
                        UpdateVariableValue($"{prefix}_Status", anemometro.status ?? (short)0);
                        string descStatus = anemometro.status == 0 ? "Normal" : "Erro";
                        UpdateVariableValue($"{prefix}_DescricaoStatus", descStatus);
                        UpdateVariableValue($"{prefix}_UltimaLeitura", anemometro.leitura ?? DateTime.UtcNow);

                        // Histórico do Anemômetro
                        int totalHist = anemoHistoryCounts != null && anemoHistoryCounts.TryGetValue(anemometro.id, out int count) ? count : 0;
                        UpdateVariableValue($"{prefix}_Historico_TotalRegistros", totalHist);

                        if (latestAnemoHistories != null && latestAnemoHistories.TryGetValue(anemometro.id, out var hist) && hist != null)
                        {
                            UpdateVariableValue($"{prefix}_Historico_UltimaLeitura", hist.leitura);
                            UpdateVariableValue($"{prefix}_Historico_VelocidadeVento", hist.velocidade_vento);
                            UpdateVariableValue($"{prefix}_Historico_DirecaoVento", hist.direcao_vento);
                            UpdateVariableValue($"{prefix}_Historico_Temperatura", hist.temperatura);
                            UpdateVariableValue($"{prefix}_Historico_Pressao", hist.pressao);
                            UpdateVariableValue($"{prefix}_Historico_Umidade", hist.umidade);
                            UpdateVariableValue($"{prefix}_Historico_Status", hist.status);
                        }
                    }
                }
            }
        }

        public void UpdateVariableValue(string path, object value)
        {
            if (_variables.TryGetValue(path, out var variable))
            {
                variable.Value = value;
                variable.Timestamp = DateTime.UtcNow;
                variable.ClearChangeMasks(SystemContext, false);
            }
        }
    }
}

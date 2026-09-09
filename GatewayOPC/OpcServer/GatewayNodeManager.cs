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
        private readonly object _lock = new();
        private FolderState? _trackersFolder;

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
                CreateVariable(gatewayFolder, "Gateway_Modo", "Modo", DataTypeIds.Int16, ValueRanks.Scalar, (short)0, isWritable: true);
                CreateVariable(gatewayFolder, "Gateway_TargetSlope", "TargetSlope", DataTypeIds.Float, ValueRanks.Scalar, 0.0f, isWritable: true);
                CreateVariable(gatewayFolder, "Gateway_NumeroTrackers", "NumeroTrackers", DataTypeIds.Int32, ValueRanks.Scalar, 0);
                CreateVariable(gatewayFolder, "Gateway_UltimaLeitura", "UltimaLeitura", DataTypeIds.DateTime, ValueRanks.Scalar, DateTime.UtcNow);
                CreateVariable(gatewayFolder, "Gateway_DescricaoStatus", "DescricaoStatus", DataTypeIds.String, ValueRanks.Scalar, "Iniciando...");

                // 3. Cria pasta para os Trackers
                _trackersFolder = CreateFolder(rootFolder, "Trackers", "Trackers");

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

            CreateVariable(trackerFolder, $"{prefix}_InclinacaoAtual", "InclinacaoAtual", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_InclinacaoAlvo", "InclinacaoAlvo", DataTypeIds.Float, ValueRanks.Scalar, 0.0f, isWritable: true);
            CreateVariable(trackerFolder, $"{prefix}_Modo", "Modo", DataTypeIds.Int16, ValueRanks.Scalar, (short)0, isWritable: true);
            CreateVariable(trackerFolder, $"{prefix}_TensaoBateria", "TensaoBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_TensaoPainel", "TensaoPainel", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_CorrenteMotor", "CorrenteMotor", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_CorrenteBateria", "CorrenteBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_TemperaturaBateria", "TemperaturaBateria", DataTypeIds.Float, ValueRanks.Scalar, 0.0f);
            CreateVariable(trackerFolder, $"{prefix}_Status", "Status", DataTypeIds.Int16, ValueRanks.Scalar, (short)0);
            CreateVariable(trackerFolder, $"{prefix}_DescricaoStatus", "DescricaoStatus", DataTypeIds.String, ValueRanks.Scalar, "Normal");
            CreateVariable(trackerFolder, $"{prefix}_UltimaLeitura", "UltimaLeitura", DataTypeIds.DateTime, ValueRanks.Scalar, DateTime.UtcNow);

            _registeredTrackerIds.Add(trackerId);
        }

        // Método chamado pelo Worker para atualizar dados em tempo real no AddressSpace
        public void UpdateData(Gateway? gateway, IEnumerable<Tracker>? trackers)
        {
            lock (_lock)
            {
                if (gateway != null)
                {
                    UpdateVariableValue("Gateway_Status", gateway.status ?? 0);
                    UpdateVariableValue("Gateway_Modo", gateway.modo);
                    UpdateVariableValue("Gateway_TargetSlope", gateway.target_slope);
                    UpdateVariableValue("Gateway_NumeroTrackers", gateway.numero_trackers ?? 0);
                    UpdateVariableValue("Gateway_UltimaLeitura", gateway.leitura ?? DateTime.UtcNow);
                    UpdateVariableValue("Gateway_DescricaoStatus", gateway.descricaoStatus);
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
                        UpdateVariableValue($"{prefix}_InclinacaoAtual", tracker.inclinacao_atual ?? 0.0f);
                        UpdateVariableValue($"{prefix}_InclinacaoAlvo", tracker.inclinacao_alvo ?? 0.0f);
                        UpdateVariableValue($"{prefix}_Modo", tracker.modo ?? (short)0);
                        UpdateVariableValue($"{prefix}_TensaoBateria", tracker.tensao_bateria ?? 0.0f);
                        UpdateVariableValue($"{prefix}_TensaoPainel", tracker.tensao_painel ?? 0.0f);
                        UpdateVariableValue($"{prefix}_CorrenteMotor", tracker.corrente_motor ?? 0.0f);
                        UpdateVariableValue($"{prefix}_CorrenteBateria", tracker.corrente_bateria ?? 0.0f);
                        UpdateVariableValue($"{prefix}_TemperaturaBateria", tracker.temperatura_bateria ?? 0.0f);
                        UpdateVariableValue($"{prefix}_Status", tracker.status ?? (short)0);
                        UpdateVariableValue($"{prefix}_DescricaoStatus", tracker.descricaoStatus);
                        UpdateVariableValue($"{prefix}_UltimaLeitura", tracker.leitura ?? DateTime.UtcNow);
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

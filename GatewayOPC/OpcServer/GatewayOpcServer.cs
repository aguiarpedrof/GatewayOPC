using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Server;

namespace GatewayOPC.OpcServer
{
    public class GatewayOpcServer : StandardServer
    {
        public GatewayNodeManager? NodeManager { get; private set; }

        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            var nodeManagers = new List<INodeManager>();

            NodeManager = new GatewayNodeManager(server, configuration);
            nodeManagers.Add(NodeManager);

            return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
        }

        protected override ServerProperties LoadServerProperties()
        {
            var properties = new ServerProperties
            {
                ManufacturerName = "L2M / Solar Tracking",
                ProductName = "GatewayOPC Server",
                ProductUri = "http://opcfoundation.org/GatewayOPC",
                SoftwareVersion = "1.0.0",
                BuildNumber = "1.0",
                BuildDate = System.DateTime.UtcNow
            };

            return properties;
        }
    }
}

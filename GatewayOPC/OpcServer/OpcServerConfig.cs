using System;
using System.IO;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Security.Certificates;

namespace GatewayOPC.OpcServer
{
    public static class OpcServerConfig
    {
        public static async Task<ApplicationConfiguration> CreateAsync(
            string applicationName = "GatewayOPC",
            string applicationUri = "urn:localhost:GatewayOPC",
            int port = 4840,
            string endpointPath = "/GatewayOPC")
        {
            var config = new ApplicationConfiguration
            {
                ApplicationName = applicationName,
                ApplicationUri = applicationUri,
                ApplicationType = ApplicationType.Server,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(AppContext.BaseDirectory, "pki", "own"),
                        SubjectName = $"CN={applicationName}, O=L2M Solar, C=BR"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(AppContext.BaseDirectory, "pki", "trusted")
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(AppContext.BaseDirectory, "pki", "issuers")
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(AppContext.BaseDirectory, "pki", "rejected")
                    },
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 1024
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = new StringCollection
                    {
                        $"opc.tcp://0.0.0.0:{port}{endpointPath}"
                    },
                    SecurityPolicies = new ServerSecurityPolicyCollection
                    {
                        new ServerSecurityPolicy
                        {
                            SecurityMode = MessageSecurityMode.None,
                            SecurityPolicyUri = SecurityPolicies.None
                        },
                        new ServerSecurityPolicy
                        {
                            SecurityMode = MessageSecurityMode.SignAndEncrypt,
                            SecurityPolicyUri = SecurityPolicies.Basic256Sha256
                        }
                    },
                    UserTokenPolicies = new UserTokenPolicyCollection
                    {
                        new UserTokenPolicy(UserTokenType.Anonymous)
                    },
                    MinRequestThreadCount = 4,
                    MaxRequestThreadCount = 100,
                    MaxQueuedRequestCount = 200
                },
                TraceConfiguration = new TraceConfiguration
                {
                    OutputFilePath = Path.Combine(AppContext.BaseDirectory, "logs", "opcua_trace.log"),
                    DeleteOnLoad = true,
                    TraceMasks = 519
                }
            };

            await config.ValidateAsync(ApplicationType.Server);

            if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += (validator, eventArgs) =>
                {
                    eventArgs.Accept = true;
                };
            }

            return config;
        }
    }
}

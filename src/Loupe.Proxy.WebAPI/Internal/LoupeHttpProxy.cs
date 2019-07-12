using System;
using System.Dynamic;
using System.Net.Http;
using System.Text;
using Gibraltar.Agent;
using Gibraltar.Agent.Configuration;

namespace Loupe.Proxy.WebAPI.Internal
{

    /// <summary>
    /// Raw HTTP(s) proxy to a Loupe Server
    /// </summary>
    internal static class LoupeHttpProxy
    {
        /// <summary>
        /// The web request header to add for our hash
        /// </summary>
        public const string HeaderSHA1Hash = "X-Gibraltar-Hash";
        public const string HeaderRequestMethod = "X-Request-Method";
        public const string HeaderRequestTimestamp = "X-Request-Timestamp";
        public const string HeaderRequestAppProtocolVersion = "X-Request-App-Protocol";

        internal const string LogCategory = "Loupe.Proxy";
        public const string SdsServerName = "hub.gibraltarsoftware.com";
        private const string SdsEntryPath = "Customers/{0}/";

        private static HttpClient _serverClient;

        /// <summary>
        /// Initialize the proxy with the current agent configuration
        /// </summary>
        /// <param name="configuration"></param>
        public static void Initialize(AgentConfiguration configuration, ProxyConfiguration proxyConfiguration)
        {
            var serverConfig = configuration.Server;

            if (serverConfig.Enabled == false)
            {
                //log that we can't fire up because server configuration is disabled
                Log.Information(LogCategory, "Loupe Proxy is Disabled because Server configuration is disabled",
                    "The Loupe proxy uses the server configuration for the current application to connect" +
                    "to the server. Since that is disabled the proxy will not start.");
                return;
            }

            Configuration = proxyConfiguration;

            var hostName = serverConfig.UseGibraltarService ? SdsServerName : serverConfig.Server;

            hostName = hostName?.Trim();

            if (string.IsNullOrEmpty(hostName))
            {
                Log.Warning(LogCategory, "Loupe Proxy is Disabled because Server configuration is incomplete or incorrect",
                    "The Loupe proxy uses the server configuration for the current application to connect" +
                    "to the server. Since that is incomplete or incorrect the proxy will not start.\r\n\r\n{0}",
                    ServerConfigurationDescription(serverConfig));
                return;
            }

            //format up base directory in case we get something we can't use.  It has to have leading & trailing slashes.
            var effectiveRepository =
                serverConfig.UseGibraltarService ? serverConfig.CustomerName : serverConfig.Repository;
            var applicationBaseDirectory = EffectiveApplicationBaseDirectory(serverConfig.ApplicationBaseDirectory, effectiveRepository);

            bool useSsl = serverConfig.UseGibraltarService || serverConfig.UseSsl;

            var baseServerAddressRaw = CalculateBaseAddress(useSsl, hostName, applicationBaseDirectory, serverConfig);

            if (Uri.TryCreate(baseServerAddressRaw, UriKind.Absolute, out var baseServerAddress) == false)
            {
                Log.Warning(LogCategory, "Loupe Proxy is Disabled because Server configuration is incomplete or incorrect",
                    "The Loupe proxy uses the server configuration for the current application to connect" +
                    "to the server. Since that is incomplete or incorrect the proxy will not start.\r\n\r\n{0}",
                    ServerConfigurationDescription(serverConfig));
            }

            //now we can finally create our one true HTTP client.
            _serverClient = new HttpClient { BaseAddress = baseServerAddress };

            //and log that we're initialized.
            Log.Information(LogCategory, "Loupe Proxy is Enabled",
                "The Loupe proxy has started and will process requests sent to this application.\r\n\r\n{0}",
                ServerConfigurationDescription(serverConfig));

            Enabled = true;
        }

        public static bool Enabled { get; set; }

        /// <summary>
        /// Get the preconfigured server client which is null if we aren't enabled or initialized
        /// </summary>
        public static HttpClient ServerClient => Enabled ? _serverClient : null;

        /// <summary>
        /// The active proxy configuration (if initialized)
        /// </summary>
        public static ProxyConfiguration Configuration { get; private set; }


        public static string ServerConfigurationDescription(ServerConfiguration configuration)
        {
            var stringBuilder = new StringBuilder();

            if (configuration.UseGibraltarService)
            {
                stringBuilder.AppendLine("Server: Use Loupe Service");
                stringBuilder.AppendFormat("\tCustomer Name: {0}\r\n", configuration.CustomerName);
            }
            else
            {
                stringBuilder.AppendLine("Server: Using Loupe Self-Hosted Server");

                stringBuilder.AppendFormat("\tServer DNS Name: {0}\r\n", configuration.Server);

                if (configuration.UseSsl)
                {
                    stringBuilder.AppendFormat("\tUse SSL: {0}\r\n", configuration.UseSsl);
                }

                if (configuration.Port != 0)
                {
                    stringBuilder.AppendFormat("\tPort: {0}\r\n", configuration.Port);
                }

                if (!string.IsNullOrWhiteSpace(configuration.ApplicationBaseDirectory))
                {
                    stringBuilder.AppendFormat("\tApplication Base Directory: {0}\r\n", configuration.ApplicationBaseDirectory);
                }

                if (!string.IsNullOrWhiteSpace(configuration.Repository))
                {
                    stringBuilder.AppendFormat("\tRepository: {0}\r\n", configuration.Repository);
                }
            }

            return stringBuilder.ToString();
        }

        private static string CalculateBaseAddress(bool useSsl, string hostName, string applicationBaseDirectory,
            ServerConfiguration serverConfig)
        {
            bool usePort = true;
            if ((useSsl == false) && ((serverConfig.Port == 0) || (serverConfig.Port == 80)))
            {
                usePort = false;
            }
            else if (useSsl && ((serverConfig.Port == 0) || (serverConfig.Port == 443)))
            {
                usePort = false;
            }

            var baseAddress = new StringBuilder(1024);

            baseAddress.AppendFormat("{0}://{1}", useSsl ? "https" : "http", hostName);

            if (usePort)
            {
                baseAddress.AppendFormat(":{0}", serverConfig.Port);
            }

            if (string.IsNullOrEmpty(applicationBaseDirectory) == false)
            {
                baseAddress.Append(applicationBaseDirectory);
            }

            return baseAddress.ToString();
        }



        /// <summary>
        /// Combines application base directory (if not null) and repository (if not null) into one merged path.
        /// </summary>
        private static string EffectiveApplicationBaseDirectory(string applicationBaseDirectory, string repository)
        {
            string effectivePath = applicationBaseDirectory ?? String.Empty;

            if (String.IsNullOrEmpty(effectivePath) == false)
            {
                effectivePath = effectivePath.Trim();

                //normalize to a leading and trailing slash.
                if (effectivePath.StartsWith("/") == false)
                {
                    effectivePath = "/" + effectivePath;
                }
                if (effectivePath.EndsWith("/") == false)
                {
                    effectivePath += "/";
                }
            }
            else
            {
                effectivePath = "/";
            }

            if (String.IsNullOrEmpty(repository) == false)
            {
                //we want a specific repository - which was created for Loupe Service so it assumes everyone's a "customer".  Oh well.
                effectivePath += String.Format(SdsEntryPath, repository);
            }

            //finally, slap on Hub.
            return effectivePath += "Hub/";
        }

    }
}

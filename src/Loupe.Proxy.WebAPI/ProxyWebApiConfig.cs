using System;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Routing;
using Gibraltar.Agent;
using Gibraltar.Agent.Configuration;
using Loupe.Proxy.WebAPI;
using Loupe.Proxy.WebAPI.Internal;

[assembly: WebActivatorEx.PreApplicationStartMethod(typeof(ProxyWebApiConfig),
    "LoupePreStart")]
[assembly: WebActivatorEx.PostApplicationStartMethod(typeof(ProxyWebApiConfig), "LoupePostStart")]
namespace Loupe.Proxy.WebAPI
{
    public static class ProxyWebApiConfig
    {
        private static ProxyConfiguration _configuration;
        private static AgentConfiguration _agentConfiguration;

        public static void LoupePreStart()
        {
            _configuration = new ProxyConfiguration();

            //register for the Loupe initializing event so we get the final configuration.
            Log.Initializing += LogOnInitializing;

            //we do explicit routes because we can't risk that the host application is
            //properly set up for attribute based routing and we don't want to break anyone.
            GlobalConfiguration.Configuration.Routes.MapHttpRoute(
                name: "LoupeProxyConfiguration",
                routeTemplate: "loupe/hub/{fileName}",
                defaults: new { controller = "LoupeProxy", action = "GetHub" }
            );
            GlobalConfiguration.Configuration.Routes.MapHttpRoute(
                name: "LoupeProxyClientHost",
                routeTemplate: "loupe/hub/hosts/{clientId}/{fileName}",
                defaults: new { controller = "LoupeProxy", action = "GetClientHost" }
            );
            GlobalConfiguration.Configuration.Routes.MapHttpRoute(
                name: "LoupeProxySessionGet",
                routeTemplate: "loupe/hub/hosts/{clientId}/sessions/{sessionId}/{fileName}",
                defaults: new { controller = "LoupeProxy", action = "GetSession" },
            constraints: new { httpMethod = new HttpMethodConstraint(HttpMethod.Get) }
            );
            GlobalConfiguration.Configuration.Routes.MapHttpRoute(
                name: "LoupeProxySessionPost",
                routeTemplate: "loupe/hub/hosts/{clientId}/sessions/{sessionId}/{fileName}",
                defaults: new { controller = "LoupeProxy", action = "PostSession" },
                constraints: new { httpMethod = new HttpMethodConstraint(HttpMethod.Put, HttpMethod.Post, HttpMethod.Delete) }
            );
            GlobalConfiguration.Configuration.Routes.MapHttpRoute(
                name: "LoupeProxySessionFilePost",
                routeTemplate: "loupe/hub/hosts/{clientId}/sessions/{sessionId}/files/{fileName}",
                defaults: new { controller = "LoupeProxy", action = "PostSessionFile" },
                constraints: new { httpMethod = new HttpMethodConstraint(HttpMethod.Put, HttpMethod.Post, HttpMethod.Delete) }
            );
        }

        public static void LoupePostStart()
        {
            //Force Loupe init if it hasn't happened...
            Log.StartSession();

            LoupeHttpProxy.Initialize(_agentConfiguration, _configuration);
        }

        private static void LogOnInitializing(object sender, LogInitializingEventArgs e)
        {
            if (e.Cancel)
                return;

            //we really need a post-initializing event so we can get the final running
            //configuration but this is the closest we can get with the current Loupe agent.
            _agentConfiguration = e.Configuration;

            Log.Initializing -= LogOnInitializing;
        }
    }
}

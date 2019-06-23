using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Gibraltar.Agent;
using Gibraltar.Agent.Web.Mvc.Filters;

namespace Loupe.Proxy.Demo
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            Log.StartSession(); //Prompt the Loupe Agent to start immediately
            //Register the three filters
            GlobalConfiguration.Configuration.Filters.Add(new WebApiRequestMonitorAttribute());
            GlobalFilters.Filters.Add(new MvcRequestMonitorAttribute());
            GlobalFilters.Filters.Add(new UnhandledExceptionAttribute());
        }
    }
}

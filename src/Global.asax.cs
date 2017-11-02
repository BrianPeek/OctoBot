using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;
using Microsoft.ApplicationInsights.Extensibility;

namespace GitHubBot
{
	public class WebApiApplication : System.Web.HttpApplication
	{
		protected void Application_Start()
		{
			TelemetryConfiguration.Active.InstrumentationKey = ConfigurationManager.AppSettings[Constants.AppInsightsKey];
			GlobalConfiguration.Configure(WebApiConfig.Register);
		}
	}
}

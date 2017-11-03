using System.Configuration;
using System.Web.Http;
using Microsoft.ApplicationInsights.Extensibility;

namespace GitHubBot
{
	public class WebApiApplication : System.Web.HttpApplication
	{
		protected void Application_Start()
		{
			string key = ConfigurationManager.AppSettings[Constants.AppInsightsKey];
			if(!string.IsNullOrEmpty(key))
				TelemetryConfiguration.Active.InstrumentationKey = key;
			GlobalConfiguration.Configure(WebApiConfig.Register);
		}
	}
}

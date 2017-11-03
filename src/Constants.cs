using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GitHubBot
{
	public class Constants
	{
		public const string ProductHeader = "OctoBot";
		public const string AuthTokenKey = "AuthToken";
		public const string GitHubClientIdKey = "GitHubClientId";
		public const string GitHubClientSecretKey = "GitHubClientSecret";
		public const string RedirectUriKey = "RedirectUri";
		public const string StateKey = "CSRFState";
		public const string LuisModelIdKey = "LuisModelId";
		public const string LuisSubscriptionKey = "LuisSubscriptionKey";
		public const string AppInsightsKey = "AppInsightsKey";

		public static string DemoText = "**[OctoBot](https://docs.microsoft.com/en-us/sandbox/demos/githubbot)** is a sample application that demonstrates how to use " +
								 "[LUIS.ai](https://luis.ai/), [Bot Framework](https://dev.botframework.com/), and [Octokit.NET](https://github.com/octokit/octokit.net) to interact " +
								 "with your GitHub account, repos, issues, etc. from within a chat window. " +
								 "For more information on how it was made, please see its article on [The Sandbox](https://docs.microsoft.com/en-us/sandbox/demos/githubbot). " +
								 "Please note that this is strictly a **demo**, not a supported product, and is subject to [GitHub's rate limiting policy](https://developer.github.com/v3/#rate-limiting). " +
								 "Enjoy!" + Environment.NewLine + Environment.NewLine + "---" + Environment.NewLine + Environment.NewLine;

	}
}
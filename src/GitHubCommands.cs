using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Security;
using Microsoft.Bot.Builder.ConnectorEx;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using Octokit;

namespace GitHubBot
{
	public static class GitHubCommands
	{
		public static async Task LogIn(IDialogContext context)
		{
			GitHubClient client = new GitHubClient(new ProductHeaderValue(Constants.ProductHeader));

			ConversationReference cr = context.Activity.ToConversationReference();

			IMessageActivity reply = context.MakeMessage();

			string csrf = Membership.GeneratePassword(24, 1);
			context.UserData.SetValue(Constants.StateKey, csrf);

			OauthLoginRequest request = new OauthLoginRequest(ConfigurationManager.AppSettings[Constants.GitHubClientIdKey])
			{
				Scopes = {"repo"},
				State = csrf,
				RedirectUri = GetRedirectUri(cr)
			};

			Uri loginUrl = client.Oauth.GetGitHubLoginUrl(request);

			reply.Text = "Please login to GitHub using the button below.";

			SigninCard card = SigninCard.Create("Authorize me to access your GitHub account.", "Login to GitHub", loginUrl.ToString());
			reply.Attachments.Add(card.ToAttachment());

			await context.PostAsync(reply);
		}

		private static Uri GetRedirectUri(ConversationReference cr)
		{
			// TODO: GZipSerialize is broken
			JToken jToken = JToken.FromObject(cr);
			string token = jToken.ToString();
			string cookie = HttpServerUtility.UrlTokenEncode(Encoding.UTF8.GetBytes(token));
			UriBuilder ub = new UriBuilder(ConfigurationManager.AppSettings[Constants.RedirectUriKey])
			{
				Query = "cookie=" + cookie
			};
			return ub.Uri;
		}

		public static async Task<int> GetRepoCount(string token, string scope)
		{
			GitHubClient client = GetClient(token);

			var user = await client.User.Current();

			if(!string.IsNullOrEmpty(scope))
			{
				if(scope.Contains("private"))
					return user.TotalPrivateRepos;

				if(scope.Contains("public"))
					return user.PublicRepos;
			}
			return user.TotalPrivateRepos + user.PublicRepos;
		}

		public static async Task<int> GetIssueCount(string token)
		{
			GitHubClient client = GetClient(token);
			var issues = await client.Issue.GetAllForCurrent();
			return issues.Count;
		}

		public static async Task<int> GetPullRequestCount(string token, string repo)
		{
			GitHubClient client = GetClient(token);
			var user = await client.User.Current();
			var pr = await client.PullRequest.GetAllForRepository(user.Login, repo);
			return pr.Count;
		}

		public static async Task<IReadOnlyList<Repository>> GetRepoList(string token, string scope)
		{
			GitHubClient client = GetClient(token);
			RepositoryRequest rr = new RepositoryRequest { Affiliation = RepositoryAffiliation.Owner };

			switch(scope)
			{
				case "public":
					rr.Visibility = RepositoryVisibility.Public;
					break;
				case "private":
					rr.Visibility = RepositoryVisibility.Private;
					break;
				default:
					rr.Visibility = RepositoryVisibility.All;
					break;

			}

			IReadOnlyList<Repository> repos = await client.Repository.GetAllForCurrent(rr);
			return repos;
		}

		public static async Task<IReadOnlyList<Issue>> GetIssueList(string token, string repo, DateTime? date)
		{
			RepositoryIssueRequest req = new RepositoryIssueRequest();

			if(date != null)
				req.Since = DateTime.SpecifyKind(date.Value, DateTimeKind.Local);

			GitHubClient client = GetClient(token);

			User user = await client.User.Current();
			try
			{
				IReadOnlyList<Issue> issues = await client.Issue.GetAllForRepository(user.Login, repo, req);
				return issues;
			}
			catch(NotFoundException)
			{
				return null;
			}
		}

		public static async Task<Issue> GetIssue(string token, string repo, int number)
		{
			GitHubClient client = GetClient(token);

			User user = await client.User.Current();
			try
			{
				Issue issue = await client.Issue.Get(user.Login, repo, number);
				return issue;
			}
			catch(NotFoundException)
			{
				return null;
			}
		}

		public static async Task<bool> CloseIssue(string token, string repo, int number)
		{
			GitHubClient client = GetClient(token);

			User user = await client.User.Current();
			try
			{
				Issue issue = await client.Issue.Get(user.Login, repo, number);
				IssueUpdate updateIssue = issue.ToUpdate();
				updateIssue.State = ItemState.Closed;
				await client.Issue.Update(user.Login, repo, number, updateIssue);
				return true;
			}
			catch(NotFoundException)
			{
				return false;
			}
		}

		public static async Task<IssueComment> AddComment(string token, string repo, int number, string comment)
		{
			GitHubClient client = GetClient(token);
			IssueComment ic = null;

			User user = await client.User.Current();
			try
			{
				ic = await client.Issue.Comment.Create(user.Login, repo, number, comment);
			}
			catch(NotFoundException)
			{
			}

			return ic;
		}

		public static async Task<Issue> CreateIssue(string token, string repo, string title, string comment)
		{
			GitHubClient client = GetClient(token);
			Issue i = null;

			User user = await client.User.Current();
			try
			{
				NewIssue ni = new NewIssue(title) { Body = comment };
				i = await client.Issue.Create(user.Login, repo, ni);
			}
			catch(NotFoundException)
			{
			}

			return i;
		}

		private static GitHubClient GetClient(string token)
		{
			GitHubClient client = new GitHubClient(new ProductHeaderValue(Constants.ProductHeader))
			{
				Credentials = new Credentials(token)
			};
			return client;
		}
	}
}
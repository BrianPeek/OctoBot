using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using Octokit;

namespace GitHubBot.Dialogs
{
	[Serializable]
	public class GitHubLuisDialog : LuisDialog<object>
	{
		private string _lastRepo;
		private int _lastIssue;
		private DateTime? _lastDate;

		public GitHubLuisDialog(ILuisService service) : base(service)
		{
		}

		protected override async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
		{
			IMessageActivity message = await item;

			if(message.Text.ToLower().Contains("login") || message.Text.ToLower().Contains("log in"))
				await GitHubCommands.LogIn(context);
			else if(message.Text.ToLower().Contains("logout") || message.Text.ToLower().Contains("log out"))
				await Logout(context);
			else if(message.Text.ToLower().Contains("help"))
				await Help(context, null);
			else if(message.Text.ToLower().Contains("reset"))
				await Reset(context);
			else if(message.Text.ToLower().Contains("authenticated"))
				await Authenticated(context);
			else
			{
				string token;
				context.UserData.TryGetValue(Constants.AuthTokenKey, out token);
				await base.MessageReceived(context, item);
			}
		}

		public async Task Reset(IDialogContext context)
		{
			_lastRepo = string.Empty;
			_lastIssue = 0;
			await context.PostAsync("Your state has been reset.");
		}

		public async Task Logout(IDialogContext context)
		{
			context.UserData.Clear();
			await context.PostAsync("You have been logged out. Have a nice day!");
		}

		public async Task Authenticated(IDialogContext context)
		{
			string token;
			
			if(!context.UserData.TryGetValue(Constants.AuthTokenKey, out token) || string.IsNullOrEmpty(token))
				await context.PostAsync("Authorization token not found, you are not logged in yet.");
			else
			{
				_lastRepo = string.Empty;
				_lastIssue = 0;
				await context.PostAsync("You are now authenticated. What can I help you with?");
			}
		}

		[LuisIntent("None")]
		public async Task None(IDialogContext context, LuisResult result)
		{
			await context.PostAsync("I didn't catch that...try again?");
		}

		[LuisIntent("Help")]
		public async Task Help(IDialogContext context, LuisResult result)
		{
			string message = Constants.DemoText + 
					"You can ask me about your repos and issues. Try things like:" + Environment.NewLine + Environment.NewLine +
					"* How many public repos do I have?" + Environment.NewLine + Environment.NewLine +
					"* How many issues do I have?" + Environment.NewLine + Environment.NewLine +
					"* What are the issues in the [my repo] repo?" + Environment.NewLine + Environment.NewLine +
					"* What are the details for issue 7 in [my repo]?" + Environment.NewLine + Environment.NewLine +
					"* Close issue 1 in [my repo]" + Environment.NewLine + Environment.NewLine +
					"* Add a comment to issue 8 in [my repo]" + Environment.NewLine + Environment.NewLine +
					"You can also use shorter commands, such as:" + Environment.NewLine + Environment.NewLine +
					"* issues in [my repo]" + Environment.NewLine + Environment.NewLine +
					"* details for issue 3 in [my repo]" + Environment.NewLine + Environment.NewLine +
					"* add comment for issue 3 in [my repo]" + Environment.NewLine + Environment.NewLine +
					"* create issue in [my repo]" + Environment.NewLine + Environment.NewLine +
					"* issues since Wednesday in [my repo]" + Environment.NewLine + Environment.NewLine +
					"I'll also remember the last repo and issue you mentioned, so you can use even shorter commands like this:" + Environment.NewLine + Environment.NewLine +
					"* issue 3 detail" + Environment.NewLine + Environment.NewLine +
					"* close issue 1" + Environment.NewLine + Environment.NewLine +
					"* add comment" + Environment.NewLine + Environment.NewLine +
					"* create issue" + Environment.NewLine + Environment.NewLine
				;

			await context.PostAsync(message);
		}

		[LuisIntent("TypeCount")]
		public async Task TypeCount(IDialogContext context, LuisResult result)
		{
			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				EntityRecommendation eScope, eType;
				result.TryFindEntity("ScopeType::Scope", out eScope);
				result.TryFindEntity("ScopeType::Type", out eType);

				if(eType != null)
				{
					if(eType.Entity.ToLower().Contains("repo"))
					{
						int count = await GitHubCommands.GetRepoCount(token, eScope?.Entity);
						await context.PostAsync($"You have {count} {eScope?.Entity} repos.");
					}
					else if(eType.Entity.ToLower().Contains("issue"))
					{
						int count = await GitHubCommands.GetIssueCount(token);
						await context.PostAsync($"You have {count} issues.");
					}
					else if(eType.Entity.ToLower().Contains("pull") || eType.Entity.ToLower().Contains("pr"))
					{
						EntityRecommendation repoName;
						if(result.TryFindEntity("RepoName", out repoName))
						{
							int count = await GitHubCommands.GetPullRequestCount(token, repoName.Entity);
							await context.PostAsync($"You have {count} PRs in the {repoName.Entity} repo.");
						}
						else
							await None(context, result);
					}
				}
				else
					await None(context, result);
			}

			context.Wait(MessageReceived);
		}

		[LuisIntent("ListRepos")]
		public async Task ListRepos(IDialogContext context, LuisResult result)
		{
			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				EntityRecommendation eScope;
				result.TryFindEntity("ScopeType::Scope", out eScope);

				IReadOnlyList<Repository> repos = await GitHubCommands.GetRepoList(token, eScope?.Entity);

				string msg;
				if(!repos.Any())
					msg = "You don't own any repos on GitHub.";
				else
					msg = repos.Aggregate($"You own the following {eScope?.Entity} repos: ", (current, repo) => current + Environment.NewLine + $"* [{repo.Name}]({repo.HtmlUrl})");
				await context.PostAsync(msg);
			}

			context.Wait(MessageReceived);
		}

		[LuisIntent("ListIssues")]
		public async Task ListIssues(IDialogContext context, LuisResult result)
		{
			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				GetDetailsFromLuisResult(result);
				await GetRepo(context, token, ListIssuesResume);
			}
			else
				context.Wait(MessageReceived);
		}

		private async Task ListIssuesResume(IDialogContext context, IAwaitable<string> repo)
		{
			_lastRepo = await repo;

			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				IReadOnlyList<Issue> issues = await GitHubCommands.GetIssueList(token, _lastRepo, _lastDate);
				string msg;

				if(issues == null)
					msg = "That repo doesn't exist or you don't have access to it. Try another one?";
				else if(!issues.Any())
					msg = $"There are no issues assigned to you in the **{_lastRepo}** repo.";
				else
					msg = issues.Aggregate($"These issues from the **{_lastRepo}** repo are assigned to you:", (current, issue) => current + Environment.NewLine + $"* [{issue.Number}]({issue.HtmlUrl}): {issue.Title}");

				await context.PostAsync(msg);
			}

			_lastDate = null;
			context.Wait(MessageReceived);
		}

		[LuisIntent("IssueDetail")]
		public async Task IssueDetail(IDialogContext context, LuisResult result)
		{
			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				GetDetailsFromLuisResult(result);
				await GetRepo(context, token, IssueDetailRepoResume);
			}
			else
				context.Wait(MessageReceived);
		}

		private async Task IssueDetailRepoResume(IDialogContext context, IAwaitable<string> repo)
		{
			_lastRepo = await repo;

			await GetIssue(context, IssueDetailIssueResume);
		}

		private async Task IssueDetailIssueResume(IDialogContext context, IAwaitable<double> result)
		{
			_lastIssue = (int)await result;

			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				string msg;

				Issue issue = await GitHubCommands.GetIssue(token, _lastRepo, _lastIssue);
				if (issue == null)
					msg = $"That issue doesn't exist in the **{_lastRepo}** repo, the repo itself doesn't exist, or you don't have access to it.";
				else
				{
					msg = $"Here are some details for issue **{_lastIssue}** in the **{_lastRepo}** repo:" + Environment.NewLine + Environment.NewLine +
					      $"**Name:** [{issue.Title}]({issue.HtmlUrl})" + Environment.NewLine + Environment.NewLine +
					      $"**Opened by:** [{issue.User.Login}]({issue.User.HtmlUrl})" + Environment.NewLine + Environment.NewLine +
					      $"**State:** {issue.State}" + Environment.NewLine + Environment.NewLine +
					      $"---" + Environment.NewLine + Environment.NewLine +
					      $"{issue.Body}";
				}

				await context.PostAsync(msg);
			}
			context.Wait(MessageReceived);
		}

		[LuisIntent("AddIssueComment")]
		public async Task IssueComment(IDialogContext context, LuisResult result)
		{
			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				GetDetailsFromLuisResult(result);
				await GetRepo(context, token, IssueCommentRepoResume);
			}
			else
				context.Wait(MessageReceived);
		}

		private async Task IssueCommentRepoResume(IDialogContext context, IAwaitable<string> repo)
		{
			_lastRepo = await repo;

			await GetIssue(context, IssueCommentIssueResume);
		}

		private async Task IssueCommentIssueResume(IDialogContext context, IAwaitable<double> issue)
		{
			_lastIssue = (int)await issue;

			PromptDialog.Text(context, IssueCommentFinalResume, "What comment would you like to add?");
		}

		private async Task IssueCommentFinalResume(IDialogContext context, IAwaitable<string> comment)
		{
			string c = await comment;

			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				IssueComment ic = await GitHubCommands.AddComment(token, _lastRepo, _lastIssue, c);
				if(ic != null)
					await context.PostAsync($"I have added your comment to [issue {_lastIssue}]({ic.HtmlUrl}).");
				else
					await context.PostAsync($"I could not add your comment to issue {_lastIssue}.");
			}

			context.Wait(MessageReceived);
		}

		[LuisIntent("CloseIssue")]
		public async Task CloseIssue(IDialogContext context, LuisResult result)
		{
			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				GetDetailsFromLuisResult(result);
				await GetRepo(context, token, CloseIssueRepoResume);
			}
			else
				context.Wait(MessageReceived);
		}

		private async Task CloseIssueRepoResume(IDialogContext context, IAwaitable<string> repo)
		{
			_lastRepo = await repo;

			await GetIssue(context, CloseIssueIssueResume);
		}

		private async Task CloseIssueIssueResume(IDialogContext context, IAwaitable<double> issue)
		{
			_lastIssue = (int)await issue;

			PromptDialog.Confirm(context, CloseIssueConfirmResume, $"Are you sure you want to close issue **{_lastIssue}** in the **{_lastRepo}** repo?", "I didn't catch that...try again?", 3, PromptStyle.AutoText);
		}

		private async Task CloseIssueConfirmResume(IDialogContext context, IAwaitable<bool> result)
		{
			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				string msg;

				bool yesno = await result;
				if(yesno)
				{
					if(await GitHubCommands.CloseIssue(token, _lastRepo, _lastIssue))
						msg = $"I have closed issue {_lastIssue}.";
					else
						msg = "I could not locate the repo, the issue, or you don't have permission to close it.";
				}
				else
					msg = $"I have **not** closed issue {_lastIssue}.";

				await context.PostAsync(msg);
			}
			context.Wait(MessageReceived);
		}

		[LuisIntent("CreateIssue")]
		public async Task CreateIssue(IDialogContext context, LuisResult result)
		{
			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				GetDetailsFromLuisResult(result);
				await GetRepo(context, token, CreateIssueResume);
			}
			else
				context.Wait(MessageReceived);
		}

		private async Task CreateIssueResume(IDialogContext context, IAwaitable<string> repo)
		{
			_lastRepo = await repo;

			await context.Forward(FormDialog.FromForm(CreateIssueDialog.BuildForm), CreateIssueDialogResume, context.Activity.AsMessageActivity());
		}

		private async Task CreateIssueDialogResume(IDialogContext context, IAwaitable<CreateIssueDialog> result)
		{
			CreateIssueDialog issue = await result;

			string token = await ValidateLoggedInAsync(context);
			if(!string.IsNullOrEmpty(token))
			{
				string msg;

				Issue i = await GitHubCommands.CreateIssue(token, _lastRepo, issue.Title, issue.Comment);
				if(i != null)
					msg = $"I have created the [new issue]({i.HtmlUrl}).";
				else
					msg = "I could not locate the repo, or you don't have permission to create an issue in it.";

				await context.PostAsync(msg);
			}
			context.Wait(MessageReceived);		
		}

		private async Task<string> ValidateLoggedInAsync(IDialogContext context)
		{
			string token;
			if(context.UserData.TryGetValue(Constants.AuthTokenKey, out token))
				return token;

			await context.PostAsync("You have not yet logged in. Please type **login** to do so.");
			return null;
		}

		private void GetDetailsFromLuisResult(LuisResult result)
		{
			EntityRecommendation eRepoName;
			if(result.TryFindEntity("RepoName", out eRepoName))
				_lastRepo = eRepoName.Entity;

			EntityRecommendation eIssueNumber;
			if(result.TryFindEntity("builtin.number", out eIssueNumber))
			{
				int issueNumber;
				int.TryParse(eIssueNumber.Entity, out issueNumber);
				_lastIssue = issueNumber;
			}

			EntityRecommendation eDate;
			if(result.TryFindEntity("builtin.datetimeV2.date", out eDate))
			{
				JArray date = eDate.Resolution["values"] as JArray;
				if(date != null && date.Count > 0)
					_lastDate = DateTime.Parse(date[0]["value"].ToString());
			}
		}

		private async Task GetRepo(IDialogContext context, string token, ResumeAfter<string> resume)
		{
			if(string.IsNullOrEmpty(_lastRepo))
			{
				IReadOnlyList<Repository> list = await GitHubCommands.GetRepoList(token, string.Empty);
				IEnumerable<string> repos = list.Select(i => i.Name);
				PromptDialog.Choice(context, resume, repos, "Which repo are you asking about?", "I didn't catch that...try again?");
			}
			else
				await resume(context, Awaitable.FromItem(_lastRepo));
		}

		private async Task GetIssue(IDialogContext context, ResumeAfter<double> resume)
		{
			if(_lastIssue == 0)
				PromptDialog.Number(context, resume, "What issue are you referring to?", "I didn't catch that...try again?");
			else
				await resume(context, Awaitable.FromItem((double)_lastIssue));
		}
	}
}
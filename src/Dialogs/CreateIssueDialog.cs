using System;
using Microsoft.Bot.Builder.FormFlow;
using System.ComponentModel.DataAnnotations;

namespace GitHubBot.Dialogs
{
	[Serializable]
	[Template(TemplateUsage.Confirmation, "Are you sure you want to add this issue?")]
	[Template(TemplateUsage.Navigation, "Which item do you want to change?")] 
	[Template(TemplateUsage.NoPreference, "Don't change anything.")]
	public class CreateIssueDialog
	{
		[Required]
		[Prompt("What is the title of the issue?")]
		public string Title;

		[Optional]
		[Prompt("What comment would you like to add to the issue?")]
		public string Comment;

		public static IForm<CreateIssueDialog> BuildForm()
		{
			return new FormBuilder<CreateIssueDialog>().Build();
		}
	}
}
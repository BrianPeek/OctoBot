using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Autofac;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using Octokit;
using Activity = Microsoft.Bot.Connector.Activity;
using ProductHeaderValue = Octokit.ProductHeaderValue;

namespace GitHubBot
{
	public class OAuthCallbackController : ApiController
	{
		[HttpOptions]
		[HttpGet]
		[Route("api/OAuthCallback")]
		public async Task<HttpResponseMessage> OAuthCallback([FromUri] string code, [FromUri] string state, [FromUri] string cookie, CancellationToken cancellationToken)
		{
			GitHubClient client = new GitHubClient(new ProductHeaderValue(Constants.ProductHeader));

			OauthTokenRequest request = new OauthTokenRequest(ConfigurationManager.AppSettings[Constants.GitHubClientIdKey], ConfigurationManager.AppSettings[Constants.GitHubClientSecretKey], code);
			OauthToken token = await client.Oauth.CreateAccessToken(request);

			// Send a message to the conversation to resume the flow
			ConversationReference cr = GetConversationReference(cookie);
			Activity msg = cr.GetPostToBotMessage();
			msg.Text = "authenticated";

			using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, msg))
			{
				var dataBag = scope.Resolve<IBotData>();
				await dataBag.LoadAsync(cancellationToken);
				string csrf;
				if(dataBag.UserData.TryGetValue(Constants.StateKey, out csrf) && csrf == state)
				{
					// remove persisted cookie
					dataBag.UserData.RemoveValue(Constants.StateKey);
					dataBag.UserData.SetValue(Constants.AuthTokenKey, token.AccessToken);
					await dataBag.FlushAsync(cancellationToken);

					// Resume the conversation
					await Conversation.ResumeAsync(cr, msg);

					var response = Request.CreateResponse("You are now logged in! Please return to your Bot conversation to continue.");
					response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
					return response;
				}

				// no state or state doesn't match
				var responseError = Request.CreateResponse("Invalid state, please try again.");
				responseError.StatusCode = HttpStatusCode.BadRequest;
				responseError.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
				return responseError;
			}
		}

		private ConversationReference GetConversationReference(string cookie)
		{
			// TODO: GZipDeserialize is broken
			string decoded = Encoding.UTF8.GetString(HttpServerUtility.UrlTokenDecode(cookie));
			var jToken = JToken.Parse(decoded);
			ConversationReference conversationReference = jToken.ToObject<ConversationReference>();
			return conversationReference;
		}
	}
}

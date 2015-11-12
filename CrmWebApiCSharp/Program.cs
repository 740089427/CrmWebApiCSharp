using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;

namespace CrmWebApiCSharp
{
	class Program
	{
		//This was registered in Azure AD as a WEB APPLICATION AND/OR WEB API

		//Azure Application Client ID
		private const string _clientId = "00000000-0000-0000-0000-000000000000";
		// Azure Application REPLY URL - can be anything here but it must be registered ahead of time
		private const string _redirectUrl = "http://localhost/CrmWebApi";
		//CRM URL
		private const string _serviceUrl = "https://org.crm.dynamics.com";
		//O365 credentials for authentication w/o login prompt
		private const string _username = "administrator@org.onmicrosoft.com";
		private const string _password = "password";
		//Azure Directory OAUTH 2.0 AUTHORIZATION ENDPOINT
		private const string _authority = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000";

		private static AuthenticationResult _authResult;

		static void Main(string[] args)
		{
			AuthenticationContext authContext =
				new AuthenticationContext(_authority, false);

			//Prompt for credentials
			//_authResult = authContext.AcquireToken(
			//	_serviceUrl, _clientId, new Uri(_redirectUrl));

			//No prompt for credentials
			UserCredential credentials = new UserCredential(_username, _password);
			_authResult = authContext.AcquireToken(
				_serviceUrl, _clientId, credentials);

			Task.WaitAll(Task.Run(async () => await DoWork()));
		}

		private static async Task DoWork()
		{
			try
			{
				using (HttpClient httpClient = new HttpClient())
				{
					httpClient.BaseAddress = new Uri(_serviceUrl);
					httpClient.Timeout = new TimeSpan(0, 2, 0);
					httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
					httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
					httpClient.DefaultRequestHeaders.Accept.Add(
						new MediaTypeWithQualityHeaderValue("application/json"));
					httpClient.DefaultRequestHeaders.Authorization =
						new AuthenticationHeaderValue("Bearer", _authResult.AccessToken);

					//Unbound Function
					//The URL will change in 2016 to include the API version - api/data/v8.0/WhoAmI
					HttpResponseMessage whoAmIResponse =
						await httpClient.GetAsync("api/data/WhoAmI");
					Guid userId;
					if (whoAmIResponse.IsSuccessStatusCode)
					{
						JObject jWhoAmIResponse =
							JObject.Parse(whoAmIResponse.Content.ReadAsStringAsync().Result);
						userId = (Guid)jWhoAmIResponse["UserId"];
						Console.WriteLine("WhoAmI " + userId);
					}
					else
						return;

					//Retrieve 
					//The URL will change in 2016 to include the API version - api/data/v8.0/systemusers
					HttpResponseMessage retrieveResponse =
						await httpClient.GetAsync("api/data/systemusers(" +
						userId + ")?$select=fullname");
					if (retrieveResponse.IsSuccessStatusCode)
					{
						JObject jRetrieveResponse =
							JObject.Parse(retrieveResponse.Content.ReadAsStringAsync().Result);
						string fullname = jRetrieveResponse["fullname"].ToString();
						Console.WriteLine("Fullname " + fullname);
					}
					else
						return;

					//Create
					JObject newAccount = new JObject
					{
						{"name", "CSharp Test"},
						{"telephone1", "111-888-7777"}
					};

					//The URL will change in 2016 to include the API version - api/data/v8.0/accounts
					HttpResponseMessage createResponse =
						await httpClient.SendAsJsonAsync(HttpMethod.Post, "api/data/accounts", newAccount);

					Guid accountId = new Guid();
					if (createResponse.IsSuccessStatusCode)
					{
						string accountUri = createResponse.Headers.GetValues("OData-EntityId").FirstOrDefault();
						if (accountUri != null)
							accountId = Guid.Parse(accountUri.Split('(', ')')[1]);
						Console.WriteLine("Account '{0}' created.", newAccount["name"]);
					}
					else
						return;

					//Update 
					newAccount.Add("fax", "123-456-7890");

					//The URL will change in 2016 to include the API version - api/data/v8.0/accounts
					HttpResponseMessage updateResponse =
						await httpClient.SendAsJsonAsync(new HttpMethod("PATCH"), "api/data/accounts(" + accountId + ")", newAccount);
					if (updateResponse.IsSuccessStatusCode)
						Console.WriteLine("Account '{0}' updated", newAccount["name"]);

					//Delete
					//The URL will change in 2016 to include the API version - api/data/v8.0/accounts
					HttpResponseMessage deleteResponse =
						await httpClient.DeleteAsync("api/data/accounts(" + accountId + ")");

					if (deleteResponse.IsSuccessStatusCode)
						Console.WriteLine("Account '{0}' deleted", newAccount["name"]);

					Console.ReadLine();
				}
			}
			catch (Exception ex)
			{
				throw;
			}
		}
	}
}

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace CrmWebApiCSharpNoAdal
{
    class Program
    {
        //FOLLOW THE STEPS HERE TO REGISTER AN APPLICATION IN AZURE AD
        //AND CREATE AN APPLICATION USER IN CRM
        //https://msdn.microsoft.com/en-us/library/mt790171.aspx

        //This was registered in Azure AD as a WEB APPLICATION AND/OR WEB API

        //Azure Application / Client ID
        private const string ClientId = "00000000-0000-0000-0000-000000000000";
        //Azure Application Client Key / Secret
        private const string ClientSecret = "SECRET VALUE FROM AZURE";

        //Resource / CRM Url
        private const string Resource = "https://test.crm.dynamics.com";

        //Guid is your Azure Active Directory Tenant Id
        private const string Authority = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/oauth2/token";

        private static string _accessToken;

        static void Main(string[] args)
        {
            var getTokenTask = Task.Run(async () => await GetToken());
            Task.WaitAll(getTokenTask);

            if (getTokenTask.Result == null)
                return;

            //Deserialize the token response to get the access token
            TokenResponse tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(getTokenTask.Result);
            _accessToken = tokenResponse.access_token;

            Task.WaitAll(Task.Run(async () => await DoWork()));
        }

        private static async Task<string> GetToken()
        {
            using (HttpClient httpClient = new HttpClient())
            {
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("resource", Resource),
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("client_secret", ClientSecret),
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                HttpResponseMessage response = await httpClient.PostAsync(Authority, formContent);

                return !response.IsSuccessStatusCode ? null
                    : response.Content.ReadAsStringAsync().Result;
            }
        }

        private static async Task DoWork()
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(Resource);
                httpClient.Timeout = new TimeSpan(0, 2, 0);
                httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _accessToken);

                //Unbound Function
                HttpResponseMessage whoAmIResponse =
                    await httpClient.GetAsync("api/data/v8.2/WhoAmI");
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
                HttpResponseMessage retrieveResponse =
                    await httpClient.GetAsync("api/data/v8.2/systemusers(" +
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

                HttpResponseMessage createResponse =
                    await httpClient.SendAsJsonAsync(HttpMethod.Post, "api/data/v8.2/accounts", newAccount);

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

                HttpResponseMessage updateResponse =
                    await httpClient.SendAsJsonAsync(new HttpMethod("PATCH"), "api/data/v8.2/accounts(" + accountId + ")", newAccount);
                if (updateResponse.IsSuccessStatusCode)
                    Console.WriteLine("Account '{0}' updated", newAccount["name"]);

                //Delete
                HttpResponseMessage deleteResponse =
                    await httpClient.DeleteAsync("api/data/v8.2/accounts(" + accountId + ")");

                if (deleteResponse.IsSuccessStatusCode)
                    Console.WriteLine("Account '{0}' deleted", newAccount["name"]);

                Console.ReadLine();
            }
        }
    }
}
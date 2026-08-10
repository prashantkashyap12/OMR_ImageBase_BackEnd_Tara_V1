using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace SQCScanner.Services
{
    public class BravoServices(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {


        public async Task<string> sentEmail(string emailAdd, string emailOtp, int templateId)
        {

            dynamic resp;
            var apiPath = configuration["BravoEmail:ApiUrl"];
            var apiKey = configuration["BravoEmail:ApiKey"];

            // Part 1
            //HttpClient client = new HttpClient();
            //var response = await client.GetAsync(apiKey);

            // Part 2
            var httpRequestMsg = new HttpRequestMessage(HttpMethod.Get, apiPath);
            var HttpClient = httpClientFactory.CreateClient();
            var response = await HttpClient.SendAsync(httpRequestMsg);

            //Get Response is Successfull/Un_successfull
            if (response.IsSuccessStatusCode)
            {
                resp = new
                {
                    dat = await response.Content.ReadAsStreamAsync(),
                    code = response.StatusCode,
                    state = true
                };
            }
            else
            {
                resp = new
                {
                    code = response.StatusCode,
                    state = false
                };
            }

            // I want to know how can i work with bravo
          

            return "";
        }
    }
}

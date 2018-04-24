using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace VeeamApi
{
    /// <summary>
    /// Throw an exception if the status code of the response is not in the range 200-299
    /// </summary>
    internal class EnsureSuccessHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
           HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                //if (!response.IsSuccessStatusCode)
                //Log :
                //- response.StatusCode
                //- request.RequestUri
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch (Exception e) { throw e; }
        }
    }
}
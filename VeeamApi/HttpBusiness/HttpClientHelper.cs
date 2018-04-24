using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace VeeamApi
{
    public class HttpClientHelper : DelegatingHandler, IHttpClientHelper
    {
        private IList<MediaTypeFormatter> formatters;
        public HttpClient Client { get; private set; }
        public string HeaderTokenName { get; set; }
        public string BaseUrl { get; private set; }
        public string Token { get; private set; }

        public event UnauthorizedEvent.OnUnauthorized OnUnauthorizeEvent;

        public HttpClientHelper()
        {
            this.formatters = new List<MediaTypeFormatter>()
            {
                new XmlMediaTypeFormatter(){ UseXmlSerializer=true } // Veeam API uses XmlSerializer
            };
            Client = HttpClientFactory.Create(new EnsureSuccessHandler(), this);
            Client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xml"));
        }

        /// <summary>
        /// Gives the HttpClient the base address for all requests
        /// </summary>
        /// <param name="url"></param>
        public void SetBaseAddress(string url)
        {
            Client.BaseAddress = new Uri(BaseUrl = url);
            SetLeaseTimeout(url);
        }

        /// <summary>
        /// Allows Lookup for DNS change
        /// </summary>
        private void SetLeaseTimeout(string url, int timeout = 60_000) =>
            ServicePointManager.FindServicePoint(new Uri(url)).ConnectionLeaseTimeout = timeout;

        #region Read

        public async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
            await response.Content.ReadAsAsync<T>(formatters);

        public async Task<T> ReadTask<T>(HttpResponseMessage response)
        {
            var task = await FinishTask(response);
            var linkToEntity = task.Links.SingleOrDefault(l => l.Rel == nameof(Rel.Related))?.Href;
            if (null == linkToEntity) return default(T);
            return await GetAsync<T>(GetRequestUri(linkToEntity));
        }

        public async Task<bool> IsSuccess(HttpResponseMessage response)
        {
            await FinishTask(response);
            return true;
        }

        private async Task<TaskType> FinishTask(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.NoContent) return new TaskType() { Result = new TaskResultInfoType() { Success = true } };
            var task = await ReadAsync<TaskType>(response);
            var taskUri = GetRequestUri(task.Links.Single(l => l.Rel == nameof(Rel.Delete)).Href);

            /*
             * Here we are supposed to get the related entity back, but sometimes the API takes time 
             * so we lookup for 10 seconds to let it a chance to finish the current tak
             */
            var counter = 0;
            do //every second
            {
                await Task.Delay(counter);
                counter += 1_000;
                task = await GetAsync<TaskType>(taskUri);
            }
            while (task.State != "Finished" && counter < 60_000); // 1 minute try
            if (task.State != "Finished")
                throw new Exception($"The task {taskUri} is taking too long to respond");
            if (!task.Result.Success)
                throw new Exception(task.Result.Item);
            return task;
        }

        #endregion

        #region Get

        public async Task<T> GetAsync<T>(string uri) =>
            await ReadAsync<T>(
                await Client.GetAsync(uri).ConfigureAwait(false));

        public async Task<T> GetEntityAsync<T>(EntityReferenceListType entities, string name, string type)
        {
            var link = entities.Items?.SingleOrDefault(e => e.Name == name)?.Links?.SingleOrDefault(l => l.Type == type)?.Href;
            if (null == link) throw new Exception($"The {type} entity {name} does not exist or the name is not valid");
            return await GetAsync<T>(GetRequestUri(link));
        }

        #endregion

        #region Post

        public async Task<T> PostAsync<T>(string url, T entity) =>
            await ReadTask<T>(
                await Client.PostAsync(url, entity, formatters.First()).ConfigureAwait(false));

        public async Task<T> PostAsync<T, E>(string url, E entity) =>
            await ReadTask<T>(
                await Client.PostAsync(url, entity, formatters.First()).ConfigureAwait(false));

        #endregion

        #region Put

        public async Task<bool> PutAsync<T>(string url, T entity) =>
            await IsSuccess(
                await Client.PutAsync<T>(url, entity, formatters.First()).ConfigureAwait(false));

        #endregion

        #region Delete

        public async Task<bool> DeleteAsync(string url) =>
            await IsSuccess(
                await Client.DeleteAsync(url).ConfigureAwait(false));

        #endregion

        #region Authenticate

        public void SetBasicAuthenticationHeader(string basicAuthHeader)
        {
            Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuthHeader);
        }

        public async Task<HttpResponseMessage> AuthenticateBasic(string basicAuthHeader, string url)
        {
            SetBasicAuthenticationHeader(basicAuthHeader);
            var response = await Client.PostAsync(GetRequestUri(url), null).ConfigureAwait(false);
            //Create authentication header
            Client.DefaultRequestHeaders.Remove("Authorization");
            Client.DefaultRequestHeaders.Add(HeaderTokenName,
                    Token = response.Headers.Single(h => h.Key == HeaderTokenName).Value.Single());
            return response;
        }

        #endregion

        #region Authorization Handler

        /// <summary>
        /// If token has expired, get a new one and retry
        /// </summary>
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var error = await ReadAsync<ErrorType>(response);
                if (error.Message == "Authentication token has expired")
                    return await RetryOnUnauthorized(request, cancellationToken);
            }
            return response;
        }

        private async Task<HttpResponseMessage> RetryOnUnauthorized(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.Headers.Contains(HeaderTokenName))
                throw new HttpException("No session token sent with request");
            await OnUnauthorizeEvent(this, null);
            request.Headers.Remove(HeaderTokenName);
            request.Headers.Add(HeaderTokenName, Client.DefaultRequestHeaders.Single(h => h.Key == HeaderTokenName).Value);
            return await base.SendAsync(request, cancellationToken);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Extract uri pattern from a full Veeam link
        /// </summary>
        public string GetRequestUri(string url) => url.Substring(BaseUrl.Length);

        #endregion
    }
}

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace VeeamApi
{
    public interface IHttpClientHelper
    {
        #region Authentication

        event UnauthorizedEvent.OnUnauthorized OnUnauthorizeEvent;
        string HeaderTokenName { get; set; }
        string Token { get; }
        void SetBasicAuthenticationHeader(string basicAuthHeader);
        Task<HttpResponseMessage> AuthenticateBasic(string basicAuthHeader, string url);

        #endregion

        HttpClient Client { get; }
        string BaseUrl { get; }

        void SetBaseAddress(string url);
        Task<bool> DeleteAsync(string url);
        Task<T> GetAsync<T>(string uri);
        Task<T> GetEntityAsync<T>(EntityReferenceListType entities, string name, string type);
        Task<bool> IsSuccess(HttpResponseMessage response);
        Task<T> PostAsync<T, E>(string url, E entity);
        Task<T> PostAsync<T>(string url, T entity);
        Task<bool> PutAsync<T>(string url, T entity);
        Task<T> ReadAsync<T>(HttpResponseMessage response);
        Task<T> ReadTask<T>(HttpResponseMessage response);
        string GetRequestUri(string url);
    }
}
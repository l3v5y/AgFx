// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace AgFx
{
    /// <summary>
    ///  Default LoadRequest for URI-based loads.  This will be the 
    ///  return type from most GetLoadRequest calls.  By default it handles GET 
    ///  loads over HTTP, but can be configured to do others.
    /// </summary>
    public class WebLoadRequest : LoadRequest
    {
        /// <summary>
        /// The Uri to request
        /// </summary>
        public Uri Uri { get; private set; }

        /// <summary>
        /// The method to use - GET or POST.
        /// </summary>
        public HttpMethod Method { get; private set; }

        /// <summary>
        /// The string data to send as part of a POST.
        /// </summary>
        public HttpContent Data { get; private set; }

        /// <summary>
        /// The content type of the data being sent.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Create a WebLoadRequest
        /// </summary>
        /// <param name="loadContext"></param>
        /// <param name="uri"></param>
        public WebLoadRequest(LoadContext loadContext, Uri uri)
            : base(loadContext)
        {
            Uri = uri;
            Method = HttpMethod.Get;
        }

        /// <summary>
        /// Create a WebLoadRequest for posting the given HttpContent
        /// </summary>
        /// <param name="loadContext"></param>
        /// <param name="uri">The URI to request</param>
        /// <param name="data">The data for a POST request</param>
        public WebLoadRequest(LoadContext loadContext, Uri uri, HttpContent data)
            : this(loadContext, uri)
        {
            Method = HttpMethod.Post;
            Data = data;
        }

        /// <summary>
        /// Performs the actual HTTP get for this request.
        /// </summary>
        public override async Task<LoadRequestResult> Execute()
        {
            try
            {
                var httpClient = new HttpClient();
                HttpResponseMessage response = null;
                if (Method == HttpMethod.Get)
                {
                    response = await httpClient.GetAsync(Uri);
                }
                else
                {
                    response = await httpClient.PostAsync(Uri, Data);
                }

                if (response.IsSuccessStatusCode)
                {
                    var resultStream = await response.Content.ReadAsStreamAsync();
                    resultStream.Seek(0, SeekOrigin.Begin);
                    return new LoadRequestResult(resultStream);
                }
                return new LoadRequestResult(new WebException("Bad web response, StatusCode=" + response.StatusCode));
            }
            catch (Exception e)
            {
                return new LoadRequestResult(e);
            }
        }
    }
}
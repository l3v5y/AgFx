﻿// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.


using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace AgFx
{
    /// <summary>
    ///     Default LoadRequest for URI-based loads.  This will be the
    ///     return type from most GetLoadRequest calls.  By default it handles GET
    ///     loads over HTTP, but can be configured to do others.
    /// </summary>
    public class WebLoadRequest : LoadRequest
    {
        /// <summary>
        ///     Create a WebLoadRequest
        /// </summary>
        /// <param name="loadContext"></param>
        /// <param name="uri"></param>
        public WebLoadRequest(LoadContext loadContext, Uri uri)
            : base(loadContext)
        {
            Uri = uri;
            Method = "GET";
        }

        /// <summary>
        ///     Create a WebLoadRequest
        /// </summary>
        /// <param name="loadContext"></param>
        /// <param name="uri">The URI to request</param>
        /// <param name="method">The method to request - GET or POST</param>
        /// <param name="data">The data for a POST request</param>
        public WebLoadRequest(LoadContext loadContext, Uri uri, string method, string data)
            : this(loadContext, uri)
        {
            Method = method;
            Data = data;
        }

        /// <summary>
        ///     The Uri to request
        /// </summary>
        public Uri Uri { get; private set; }

        /// <summary>
        ///     The method to use - GET or POST.
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        ///     The string data to send as part of a POST.
        /// </summary>
        public string Data { get; private set; }

        /// <summary>
        ///     The content type of the data being sent.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        ///     override this to do things like setting headers and whatnot.
        /// </summary>
        /// <returns></returns>
        protected virtual HttpWebRequest CreateWebRequest()
        {
            Debug.Assert(Uri != null, "Null uri");
            var hwr = (HttpWebRequest)WebRequest.Create(Uri);
            hwr.Method = Method;
            if(!String.IsNullOrEmpty(ContentType))
            {
                hwr.ContentType = ContentType;
            }

            return hwr;
        }

        /// <summary>
        ///     Override this to take a closer look at the response, for example to look at the status code.
        ///     Default implementation looks for HttpResponse.StatusCode == 200.
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        protected virtual bool IsGoodResponse(HttpWebResponse response)
        {
            return response.StatusCode == HttpStatusCode.OK;
        }

        /// <summary>
        ///     Performs the actual HTTP get for this request.
        /// </summary>
        /// <param name="result"></param>
        [SuppressMessage("Microsoft.Reliability",
            "CA2000:Dispose objects before losing scope")]
        public override void Execute(Action<LoadRequestResult> result)
        {
            if(result == null)
            {
                throw new ArgumentNullException();
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                var request = CreateWebRequest();

                AsyncCallback responseHandler = asyncObject =>
                {
                    HttpWebResponse response;

                    try
                    {
                        response = (HttpWebResponse)request.EndGetResponse(asyncObject);
                    }
                    catch(WebException we)
                    {
                        // this happens if the network isnt' actually there.
                        //
                        result(new LoadRequestResult(we));
                        return;
                    }

                    if(IsGoodResponse(response))
                    {
                        // may need to copy this on the spot.
                        //

                        // content length isn't always right, so pinch it between 1K and 16K
                        // See https://github.com/shawnburke/AgFx/issues/4
                        //
                        var length = Math.Min(short.MaxValue/2, Math.Max(1024, response.ContentLength));

                        var bytes = new byte[length];
                        var resultStream = new MemoryStream(bytes.Length);
                        using(var stream = response.GetResponseStream())
                        {
                            for(var count = stream.Read(bytes, 0, bytes.Length);
                                count > 0;
                                count = stream.Read(bytes, 0, bytes.Length))
                            {
                                resultStream.Write(bytes, 0, count);
                            }
                        }
                        resultStream.Seek(0, SeekOrigin.Begin);
                        result(new LoadRequestResult(resultStream));
                    }
                    else
                    {
                        result(
                            new LoadRequestResult(new WebException("Bad web response, StatusCode=" + response.StatusCode)));
                    }
                };


                if(Data != null) // post
                {
                    request.AllowReadStreamBuffering = true;
                    try
                    {
                        request.BeginGetRequestStream(
                            asyncObject =>
                            {
                                var item = request.EndGetRequestStream(asyncObject);
                                var bytes = Encoding.UTF8.GetBytes(Data);
                                item.Write(bytes, 0, bytes.Length);
                                item.Close();
                                request.BeginGetResponse(responseHandler, null);
                            },
                            null
                            );
                    }
                    catch(SystemException sysex)
                    {
                        // something bad happened - not sure what causes these.
                        //
                        result(new LoadRequestResult(sysex));
                    }
                }
                else
                {
                    request.BeginGetResponse(responseHandler, null);
                }
            }, null);
        }
    }
}
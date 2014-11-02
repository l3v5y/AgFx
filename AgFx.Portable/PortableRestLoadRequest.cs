// Copyright (c) 2012-13 Olly Levett
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using PortableRest;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AgFx
{
    public class PortableRestLoadRequest : LoadRequest
    {
        private RestRequest _request;
        private bool _useGzip;

        public RestRequest Request
        {
            get { return _request; }
            set { _request = value; }
        }

        public string Resource { get; private set; }

        public HttpMethod Method
        {
            get
            {
                return _request.Method;
            }
            set
            {
                _request.Method = value;
            }
        }

        public ContentTypes ContentType
        {
            get
            {
                return _request.ContentType;
            }
            set
            {
                _request.ContentType = value;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public PortableRestLoadRequest(LoadContext loadContext, string resource, bool GZip = true)
            : base(loadContext)
        {
            Resource = resource;
            CreateRestRequest();
            _useGzip = GZip;
        }

        public void AddParameter(string name, object value, ParameterEncoding encoding = ParameterEncoding.UriEncoded)
        {
            _request.AddParameter(name, value, encoding);
        }

        public void AddHeader(string key, object value)
        {
            _request.AddHeader(key, value);
        }

        protected virtual RestRequest CreateRestRequest()
        {
            Debug.Assert(Resource != null, "Null resource");
            if (_request == null)
            {
                _request = new RestRequest(Resource);
            }
            return _request;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public override void Execute(Action<LoadRequestResult> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException();
            }

            var handler = new HttpClientHandler();
            if (handler.SupportsAutomaticDecompression && _useGzip)
            {
                handler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            }
            handler.AllowAutoRedirect = true;
            var client = new RestClient(handler);
            client.UserAgent = "AgFx";

            PriorityQueue.AddNetworkWorkItem(InnerExecute(result, client));
        }

        private async Task InnerExecute(Action<LoadRequestResult> result, RestClient client)
        {
            LoadRequestResult loadRequestResult = null;
            try
            {
                var resp = await client.ExecuteAsync<string>(_request);
                byte[] byteArray = Encoding.UTF8.GetBytes(resp);
                loadRequestResult = new LoadRequestResult(new MemoryStream(byteArray));
            }
            catch (AggregateException e)
            {
                if (e.InnerExceptions.Count > 0)
                {
                    loadRequestResult = new LoadRequestResult(e.InnerExceptions[0]);
                }
                else
                {
                    loadRequestResult = new LoadRequestResult(e);
                }
            }
            finally
            {
                result(loadRequestResult);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading.Tasks;
using SBRW.Nancy.Bootstrapper;
using SBRW.Nancy.Extensions;
using SBRW.Nancy.IO;
using System.Threading;

namespace SBRW.Nancy.Hosting.Self
{
    /// <summary>
    /// Allows to host Nancy server inside any application - console or windows service.
    /// </summary>
    /// <remarks>
    /// NancyHost uses <see cref="System.Net.HttpListener"/> internally. Therefore, it requires full .net 4.0 profile (not client profile)
    /// to run. <see cref="Start"/> will launch a thread that will listen for requests and then process them. Each request is processed in
    /// its own execution thread. NancyHost needs <see cref="SerializableAttribute"/> in order to be used from another appdomain under
    /// mono. Working with AppDomains is necessary if you want to unload the dependencies that come with NancyHost.
    /// </remarks>
    [Serializable]
    public class NancyHost : IDisposable
    {
        private static int ACCESS_DENIED 
        { 
            get 
            { 
                return 5; 
            } 
        }
        private static bool Unix_Detected 
        {
            get 
            {
                return Environment.OSVersion.Platform == PlatformID.Unix;
            }
        }
        private IList<Uri> BaseUriList { get; set; }
        private HttpListener I_Listener { get; set; }
        private INancyEngine N_Engine { get; set; }
        private HostConfiguration U_Configuration { get; set; }
        private INancyBootstrapper R_Bootstrapper { get; set; }
        private bool F_Stop { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NancyHost"/> class for the specified <paramref name="baseUris"/>.
        /// Uses the default configuration
        /// </summary>
        /// <param name="baseUris">The <see cref="Uri"/>s that the host will listen to.</param>
        public NancyHost(params Uri[] baseUris)
            : this(NancyBootstrapperLocator.Bootstrapper, new HostConfiguration(), baseUris) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NancyHost"/> class for the specified <paramref name="baseUris"/>.
        /// Uses the specified configuration.
        /// </summary>
        /// <param name="baseUris">The <see cref="Uri"/>s that the host will listen to.</param>
        /// <param name="U_Configuration">Configuration to use</param>
        public NancyHost(HostConfiguration U_Configuration, params Uri[] baseUris)
            : this(NancyBootstrapperLocator.Bootstrapper, U_Configuration, baseUris){}

        /// <summary>
        /// Initializes a new instance of the <see cref="NancyHost"/> class for the specified <paramref name="baseUris"/>, using
        /// the provided <paramref name="bootstrapper"/>.
        /// Uses the default configuration
        /// </summary>
        /// <param name="bootstrapper">The bootstrapper that should be used to handle the request.</param>
        /// <param name="baseUris">The <see cref="Uri"/>s that the host will listen to.</param>
        public NancyHost(INancyBootstrapper bootstrapper, params Uri[] baseUris)
            : this(bootstrapper, new HostConfiguration(), baseUris)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NancyHost"/> class for the specified <paramref name="baseUris"/>, using
        /// the provided <paramref name="R_Bootstrapper"/>.
        /// Uses the specified configuration.
        /// </summary>
        /// <param name="R_Bootstrapper">The bootstrapper that should be used to handle the request.</param>
        /// <param name="U_Configuration">Configuration to use</param>
        /// <param name="baseUris">The <see cref="Uri"/>s that the host will listen to.</param>
        public NancyHost(INancyBootstrapper R_Bootstrapper, HostConfiguration U_Configuration, params Uri[] baseUris)
        {
            this.R_Bootstrapper = R_Bootstrapper;
            this.U_Configuration = U_Configuration ?? new HostConfiguration();
            this.BaseUriList = baseUris;

            R_Bootstrapper.Initialise();
            this.N_Engine = R_Bootstrapper.GetEngine();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NancyHost"/> class for the specified <paramref name="baseUri"/>, using
        /// the provided <paramref name="bootstrapper"/>.
        /// Uses the default configuration
        /// </summary>
        /// <param name="baseUri">The <see cref="Uri"/> that the host will listen to.</param>
        /// <param name="bootstrapper">The bootstrapper that should be used to handle the request.</param>
        public NancyHost(Uri baseUri, INancyBootstrapper bootstrapper)
            : this(bootstrapper, new HostConfiguration(), baseUri)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NancyHost"/> class for the specified <paramref name="baseUri"/>, using
        /// the provided <paramref name="bootstrapper"/>.
        /// Uses the specified configuration.
        /// </summary>
        /// <param name="baseUri">The <see cref="Uri"/> that the host will listen to.</param>
        /// <param name="bootstrapper">The bootstrapper that should be used to handle the request.</param>
        /// <param name="U_Configuration">Configuration to use</param>
        public NancyHost(Uri baseUri, INancyBootstrapper bootstrapper, HostConfiguration U_Configuration)
            : this (bootstrapper, U_Configuration, baseUri)
        {
        }

        /// <summary>
        /// Stops the host if it is running.
        /// </summary>
        public void Dispose()
        {
            this.Stop();
            this.R_Bootstrapper.Dispose();
        }

        /// <summary>
        /// Start listening for incoming requests with the given configuration
        /// </summary>
        public void Start()
        {
            this.StartListener();

            Task.Run(() =>
            {
                Semaphore semaphore = new Semaphore(this.U_Configuration.MaximumConnectionCount, this.U_Configuration.MaximumConnectionCount);
                while (!this.F_Stop)
                {
                    semaphore.WaitOne();

                    this.I_Listener.GetContextAsync().ContinueWith(async (contextTask) =>
                    {
                        try
                        {
                            semaphore.Release();
                            HttpListenerContext context = await contextTask.ConfigureAwait(false);
                            await this.Process(context).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            this.U_Configuration.UnhandledExceptionCallback.Invoke(ex);
                            throw;
                        }
                    });
                }
            });
        }

        private void StartListener()
        {
            if (this.TryStartListener())
            {
                return;
            }

            if (!this.U_Configuration.UrlReservations.CreateAutomatically)
            {
                throw new AutomaticUrlReservationCreationFailureException(this.GetPrefixes(), this.GetUser());
            }

            if (!this.TryAddUrlReservations())
            {
                throw new InvalidOperationException("Unable to configure namespace reservation");
            }

            if (!this.TryStartListener())
            {
                throw new InvalidOperationException("Unable to start listener");
            }
        }

        private bool TryStartListener()
        {
            try
            {
                // if the listener fails to start, it gets disposed;
                // so we need a new one, each time.
                this.I_Listener = new HttpListener();
                foreach (string prefix in this.GetPrefixes())
                {
                    this.I_Listener.Prefixes.Add(prefix);
                }

                this.I_Listener.Start();

                return true;
            }
            catch (HttpListenerException e)
            {
                if (e.ErrorCode == ACCESS_DENIED)
                {
                    return false;
                }

                throw;
            }
        }

        private bool TryAddUrlReservations()
        {
            string user = this.GetUser();

            foreach (string prefix in this.GetPrefixes())
            {
                if (!NetSh.AddUrlAcl(prefix, user))
                {
                    return false;
                }
            }

            return true;
        }

        private string GetUser()
        {
            return !string.IsNullOrWhiteSpace(this.U_Configuration.UrlReservations.User)
                ? this.U_Configuration.UrlReservations.User
#if NETFRAMEWORK || NETSTANDARD2_0 || NET5_0_OR_GREATER && WINDOWS
                : Unix_Detected
                ? WindowsIdentity.GetCurrent().Name : string.Empty;
#else
                : string.Empty;
#endif
        }

        /// <summary>
        /// Stop listening for incoming requests.
        /// </summary>
        public void Stop()
        {
            if (this.I_Listener != null && this.I_Listener.IsListening)
            {
                this.F_Stop = true;
                this.I_Listener.Stop();
            }
        }

        internal IEnumerable<string> GetPrefixes()
        {
            foreach (var baseUri in this.BaseUriList)
            {
                string prefix = new UriBuilder(baseUri).ToString();

                if (this.U_Configuration.RewriteLocalhost && !baseUri.Host.Contains("."))
                {
                    prefix = prefix.Replace("localhost", this.U_Configuration.UseWeakWildcard ? "*" : "+");
                }

                yield return prefix;
            }
        }

        private Request ConvertRequestToNancyRequest(HttpListenerRequest request)
        {
            Uri baseUri = this.GetBaseUri(request);

            if (baseUri == null)
            {
                throw new InvalidOperationException(string.Format("Unable to locate base URI for request: {0}",request.Url));
            }

            long expectedRequestLength =
                GetExpectedRequestLength(request.Headers.ToDictionary());

            Url nancyUrl = new Url
            {
                Scheme = request.Url.Scheme,
                HostName = request.Url.Host,
                Port = request.Url.IsDefaultPort ? null : (int?)request.Url.Port,
                BasePath = baseUri.AbsolutePath.TrimEnd('/'),
                Path = baseUri.MakeAppLocalPath(request.Url),
                Query = request.Url.Query
            };

            X509Certificate2 certificate = null;

            if (this.U_Configuration.EnableClientCertificates)
            {
                X509Certificate2 x509Certificate = request.GetClientCertificate();

                if (x509Certificate != null)
                {
                    certificate = x509Certificate;
                }
            }

            // NOTE: For HTTP/2 we want fieldCount = 1,
            // otherwise (HTTP/1.0 and HTTP/1.1) we want fieldCount = 2
            int fieldCount = request.ProtocolVersion.Major == 2 ? 1 : 2;

            string protocolVersion = string.Format("HTTP/{0}", request.ProtocolVersion.ToString(fieldCount));

            return new Request(
                request.HttpMethod,
                nancyUrl,
                RequestStream.FromStream(request.InputStream, expectedRequestLength, StaticConfiguration.DisableRequestStreamSwitching ?? false),
                request.Headers.ToDictionary(),
                (request.RemoteEndPoint != null) ? request.RemoteEndPoint.Address.ToString() : null,
                certificate,
                protocolVersion);
        }

        private Uri GetBaseUri(HttpListenerRequest request)
        {
            Uri result = this.BaseUriList.FirstOrDefault(uri => uri.IsCaseInsensitiveBaseOf(request.Url));

            if (result != null)
            {
                return result;
            }

            if (!this.U_Configuration.AllowAuthorityFallback)
            {
                return null;
            }

            return new Uri(request.Url.GetLeftPart(UriPartial.Authority));
        }

        private void ConvertNancyResponseToResponse(Response nancyResponse, HttpListenerResponse response)
        {
            foreach (var header in nancyResponse.Headers)
            {
                if (!IgnoredHeaders.IsIgnored(header.Key))
                {
                    response.AddHeader(header.Key, header.Value);
                }
            }

            foreach (var nancyCookie in nancyResponse.Cookies)
            {
                response.Headers.Add(HttpResponseHeader.SetCookie, nancyCookie.ToString());
            }

            if (nancyResponse.ReasonPhrase != null)
            {
                response.StatusDescription = nancyResponse.ReasonPhrase;
            }

            if (nancyResponse.ContentType != null)
            {
                response.ContentType = nancyResponse.ContentType;
            }

            response.StatusCode = (int)nancyResponse.StatusCode;

            if (this.U_Configuration.AllowChunkedEncoding)
            {
                OutputWithDefaultTransferEncoding(nancyResponse, response);
            }
            else
            {
                OutputWithContentLength(nancyResponse, response);
            }
        }

        private static void OutputWithDefaultTransferEncoding(Response nancyResponse, HttpListenerResponse response)
        {
            using (Stream output = response.OutputStream)
            {
                nancyResponse.Contents.Invoke(output);
            }
        }

        private static void OutputWithContentLength(Response nancyResponse, HttpListenerResponse response)
        {
            byte[] buffer;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                nancyResponse.Contents.Invoke(memoryStream);
                buffer = memoryStream.ToArray();
            }

            long contentLength = nancyResponse.Headers.TryGetValue("Content-Length", out string value) ?
                Convert.ToInt64(value) :
                buffer.Length;

            response.SendChunked = false;
            response.ContentLength64 = contentLength;

            using (Stream output = response.OutputStream)
            {
                using (BinaryWriter writer = new BinaryWriter(output))
                {
                    writer.Write(buffer);
                    writer.Flush();
                }
            }
        }

        private static long GetExpectedRequestLength(IDictionary<string, IEnumerable<string>> incomingHeaders)
        {
            if (incomingHeaders == null)
            {
                return 0;
            }

            if (!incomingHeaders.TryGetValue("Content-Length", out IEnumerable<string> values))
            {
                return 0;
            }

            string headerValue = values.SingleOrDefault();

            if (headerValue == null)
            {
                return 0;
            }

            return !long.TryParse(headerValue, NumberStyles.Any, CultureInfo.InvariantCulture, out long contentLength) ?
                0 :
                contentLength;
        }

        private async Task Process(HttpListenerContext ctx)
        {
            try
            {
                Request nancyRequest = this.ConvertRequestToNancyRequest(ctx.Request);
                using (NancyContext nancyContext = await this.N_Engine.HandleRequest(nancyRequest).ConfigureAwait(false))
                {
                    try
                    {
                        this.ConvertNancyResponseToResponse(nancyContext.Response, ctx.Response);
                    }
                    catch (Exception e)
                    {
                        this.U_Configuration.UnhandledExceptionCallback.Invoke(e);
                    }
                }
            }
            catch (Exception e)
            {
                this.U_Configuration.UnhandledExceptionCallback.Invoke(e);
            }
        }
    }
}

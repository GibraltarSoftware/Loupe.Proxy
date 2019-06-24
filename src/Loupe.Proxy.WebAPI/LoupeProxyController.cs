using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Gibraltar.Agent;
using Loupe.Proxy.WebAPI.Internal;

namespace Loupe.Proxy.WebAPI
{
    public class LoupeProxyController : ApiController
    {
        private const string LogCategory = LoupeHttpProxy.LogCategory;

        /// <summary>
        /// Get the repository configuration
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ActionName("GetHub")]
        public async Task<HttpResponseMessage> GetHub(string fileName)
        {
            if (TryGetServerClient(out var serverConnection, out var response) == false)
            {
                return response;
            }

            //verify the file name is a valid one for our situation.
            if (string.Equals(fileName, "configuration.xml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "sessionsversion.xml", StringComparison.OrdinalIgnoreCase))
            {
                //request info from the server and buffer it locally...
                var result = await serverConnection.GetAsync(fileName);

                return CreateSafeResponse(result);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Get the list of requested sessions for the specified client
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("GetClientHost")]
        public async Task<HttpResponseMessage> GetClientHost(Guid clientId, string fileName)
        {
            if (TryGetServerClient(out var serverConnection, out var response) == false)
            {
                return response;
            }

            //verify the file name is a valid one for our situation.
            if (string.Equals(fileName, "requestedsessions.xml", StringComparison.OrdinalIgnoreCase))
            {
                //request info from the server and return it to our caller...
                string path = $"hosts/{clientId}/{fileName}";
                var result = await serverConnection.GetAsync(path);

                return CreateSafeResponse(result);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }


        /// <summary>
        /// Get the list of session files the server has for this session
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ActionName("GetSession")]
        public async Task<HttpResponseMessage> GetSession(Guid clientId, Guid sessionId, string fileName)
        {
            if (TryGetServerClient(out var serverConnection, out var response) == false)
            {
                return response;
            }

            //verify the file name is a valid one for our situation.
            if (string.Equals(fileName, "files.xml", StringComparison.OrdinalIgnoreCase))
            {
                //request info from the server and return it to our caller...
                string path = $"hosts/{clientId}/sessions/{sessionId}/{fileName}";
                var result = await serverConnection.GetAsync(path);

                return CreateSafeResponse(result);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Write a session header to the repository
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
//        [AcceptVerbs(WebRequestMethods.Http.Put, WebRequestMethods.Http.Post, "DELETE")]
        [HttpPost]
        [ActionName("PostSession")]
        public async Task<HttpResponseMessage> PostSession(Guid clientId, Guid sessionId, string fileName)
        {
            var httpRequest = HttpContext.Current.Request;

            if (TryGetServerClient(out var serverConnection, out var response) == false)
            {
                return response;
            }

            //verify the file name is a valid one for our situation.
            if (string.Equals(fileName, "session.glf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "session.gz", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "session.zip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "session.hdr", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "session.xml", StringComparison.OrdinalIgnoreCase))
            {
                //make sure the size of the file is in spec
                if (httpRequest.ContentLength / 1024 > LoupeHttpProxy.Configuration.MaxFileSizeKB)
                {
                    Log.Warning(LogCategory, "Client attempted to post session data that exceeds the allowed maximum size.",
                        "The proxy is configured to not accept data larger than {0:n0}KB and the input was {1:N0}KB.",
                        LoupeHttpProxy.Configuration.MaxFileSizeKB, httpRequest.ContentLength);
                    return new HttpResponseMessage(HttpStatusCode.NotAcceptable);
                }

                //it's a valid file - copy the data locally, we don't directly present
                //information to the server for security purposes.
                using (var tempFileStream = new FileStream(Path.GetTempFileName(),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Delete,
                    4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose))
                {
                    //get the data from the client...
                    await httpRequest.GetBufferlessInputStream().CopyToAsync(tempFileStream);

                    //set our file length and rewind the stream so we can read from it.
                    tempFileStream.SetLength(tempFileStream.Position);
                    tempFileStream.Position = 0;

                    //Now push the data to the remote server.
                    var path = $"hosts/{clientId}/sessions/{sessionId}/{fileName}";

                    var result = await SendStreamToServer(httpRequest, tempFileStream, serverConnection, 
                        path, new []
                        {
                            LoupeHttpProxy.HeaderRequestAppProtocolVersion,
                            LoupeHttpProxy.HeaderRequestMethod,
                            LoupeHttpProxy.HeaderRequestTimestamp,
                            LoupeHttpProxy.HeaderSHA1Hash
                        });

                    return CreateSafeResponse(result);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Write a session fragment file to the repository
        /// </summary>
        /// <returns></returns>
        [AcceptVerbs(WebRequestMethods.Http.Put, WebRequestMethods.Http.Post, "DELETE")]
        [ActionName("PostSessionFile")]
        public async Task<HttpResponseMessage> PostSessionFile(Guid clientId, Guid sessionId, string fileName)
        {
            var httpRequest = HttpContext.Current.Request;

            if (TryGetServerClient(out var serverConnection, out var response) == false)
            {
                return response;
            }
            
            //make sure the size of the file is in spec
            if (httpRequest.ContentLength / 1024 > LoupeHttpProxy.Configuration.MaxFileSizeKB)
            {
                Log.Warning(LogCategory, "Client attempted to post session file data that exceeds the allowed maximum size.",
                    "The proxy is configured to not accept data larger than {0:n0}KB and the input was {1:N0}KB.",
                    LoupeHttpProxy.Configuration.MaxFileSizeKB, httpRequest.ContentLength);
                return new HttpResponseMessage(HttpStatusCode.NotAcceptable);
            }

            //it's a valid file - copy the data locally, we don't directly present
            //information to the server for security purposes.
            using (var tempFileStream = new FileStream(Path.GetTempFileName(),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Delete,
                4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose))
            {
                //get the data from the client...
                await httpRequest.GetBufferlessInputStream().CopyToAsync(tempFileStream);

                //set our file length and rewind the stream so we can read from it.
                tempFileStream.SetLength(tempFileStream.Position);
                tempFileStream.Position = 0;

                //Now push the data to the remote server.
                var path = $"hosts/{clientId}/sessions/{sessionId}/files/{fileName}";

                var result = await SendStreamToServer(httpRequest, tempFileStream, serverConnection, 
                    path, new[]
                    {
                        LoupeHttpProxy.HeaderRequestAppProtocolVersion,
                        LoupeHttpProxy.HeaderRequestMethod,
                        LoupeHttpProxy.HeaderRequestTimestamp,
                        LoupeHttpProxy.HeaderSHA1Hash
                    }, 
                    new []{ "Start","Complete", "FileSize" });

                return CreateSafeResponse(result);
            }
        }

        private static async Task<HttpResponseMessage> SendStreamToServer(HttpRequest sourceRequest,
            Stream sourceStream, HttpClient serverConnection, string path, string[] validHeaders, string[] validParameters = null,
            [CallerMemberName] string callingMethod = "")
        {
            var content = new StreamContent(sourceStream);

            //we need some headers from the original request...
            content.Headers.ContentType = new MediaTypeHeaderValue(sourceRequest.ContentType);
            content.Headers.ContentLength = sourceStream.Length;

            if (validHeaders != null && validHeaders.Length > 0)
            {
                foreach (var header in validHeaders)
                {
                    var headerValue = sourceRequest.Headers.GetValues(header)?.FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(headerValue) == false)
                    {
                        content.Headers.Add(header, headerValue);
                    }
                }
            }

            var queryString = string.Empty;
            if (validParameters != null && validParameters.Length > 0)
            {
                foreach (var parameter in validParameters)
                {
                    //if this parameter was set in the client request copy it across to our request.
                    var parameterValue = sourceRequest.Params[parameter];
                    if (string.IsNullOrWhiteSpace(parameterValue) == false)
                    {
                        queryString += $"&{parameter}={parameterValue}";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(queryString) == false)
            {
                //pull off the first & that we incorrectly added
                //and append this to the path as a query string.
                path += "?" + queryString.Substring(1, queryString.Length - 1);
            }

            //we need to preserve the type of call the original caller made since the protocol expects it
            HttpResponseMessage result;
            try
            {
                if (string.Equals(sourceRequest.HttpMethod, "PUT", StringComparison.OrdinalIgnoreCase))
                {
                    result = await serverConnection.PutAsync(path, content);
                }
                else
                {
                    result = await serverConnection.PostAsync(path, content);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, LogCategory, "Unable to communicate with Loupe server during " + callingMethod + " due to " + ex.GetBaseException().GetType(),
                    "The client will be told the service is unavailable.");

                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return result;
        }

        /// <summary>
        /// Get a redacted server response to return
        /// </summary>
        /// <param name="serverResponse"></param>
        /// <returns></returns>
        private static HttpResponseMessage CreateSafeResponse(HttpResponseMessage serverResponse)
        {
            if (serverResponse.IsSuccessStatusCode)
            {
                return serverResponse; //we assume server responses are safe.
            }
            else
            {
                //we will let some specific status codes be proxied back, but not all to avoid
                //leaking.
                return CreateSafeFailureResponse(serverResponse);
            }
        }

        private static HttpResponseMessage CreateSafeFailureResponse(HttpResponseMessage serverResponse, [CallerMemberName] string callingMember = "")
        {
            HttpStatusCode responseCode;
            string responseMessage = null;
            switch (serverResponse.StatusCode)
            {
                case HttpStatusCode.InternalServerError:
                    //translate this to something more innocuous.
                    responseCode = HttpStatusCode.ServiceUnavailable;
                    break;
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.LengthRequired:
                case HttpStatusCode.MethodNotAllowed:
                case HttpStatusCode.NotAcceptable:
                case HttpStatusCode.NotImplemented:
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.RequestedRangeNotSatisfiable:
                case HttpStatusCode.RequestEntityTooLarge:
                case HttpStatusCode.RequestTimeout:
                case HttpStatusCode.RequestUriTooLong:
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.UnsupportedMediaType:
                    //these are valid, let the client have them.
                    responseCode = serverResponse.StatusCode;
                    responseMessage = serverResponse.ReasonPhrase;
                    break;
                default:
                    //this default should be handled by the agent in a safe way.
                    responseCode = HttpStatusCode.ServiceUnavailable;
                    break;
            }

            Log.Warning(LogCategory, "Failed to communicate with Loupe server during " + callingMember, 
                "Http Request path: {0}\r\n" +
                "Http Response Code: {1}\r\n" +
                "Response Message: {2}\r\n" +
                "Response Code for Client: {3}",
                serverResponse.RequestMessage.RequestUri,
                serverResponse.StatusCode, serverResponse.ReasonPhrase, responseCode);

            return new HttpResponseMessage
            {
                ReasonPhrase = responseMessage,
                StatusCode = responseCode
            };
        }

        private bool TryGetServerClient(out HttpClient serverClient, out HttpResponseMessage response)
        {
            serverClient = LoupeHttpProxy.ServerClient;
            response = null;
            if (serverClient == null)
            {
                response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                return false;
            }

            return true;
        }
    }
}

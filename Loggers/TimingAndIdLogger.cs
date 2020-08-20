using HttpClientFactoryExtensions.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpClientFactoryExtensions.Loggers
{
    public class TimingAndIdLoggingScopeHttpMessageHandler : DelegatingHandler
    {
        private readonly ILogger _logger;
        public TimingAndIdLoggingScopeHttpMessageHandler(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (Log.BeginRequestPipelineScope(_logger, request))
            {
                var timing = ValueStopwatch.StartNew();
                var requestId = Guid.NewGuid().ToString("N");
                request.Headers.Add("X-Request-Id",requestId);
                Log.RequestPipelineStart(_logger, request);

                var response = await base.SendAsync(request, cancellationToken);
                if(!response.Headers.Contains("X-Request-Id"))
                    response.Headers.Add("X-Request-Id", requestId);
                Log.RequestPipelineEnd(_logger, response, timing.GetElapsedTime(), Log.GetCorrelationIdFromRequest(request));

                return response;
            }
        }

        private static class Log
        {
            private static class EventIds
            {
                public static readonly EventId PipelineStart = new EventId(100, "RequestPipelineStart");
                public static readonly EventId PipelineEnd = new EventId(101, "RequestPipelineEnd");
            }

            private static readonly Func<ILogger, HttpMethod, Uri, string, IDisposable> _beginRequestPipelineScope =
                LoggerMessage.DefineScope<HttpMethod, Uri, string>(
                    "HTTP {HttpMethod} {Uri} {RequestId}");

            private static readonly Action<ILogger, HttpMethod, Uri, string, Exception> _requestPipelineStart =
                LoggerMessage.Define<HttpMethod, Uri, string>(
                    LogLevel.Information,
                    EventIds.PipelineStart,
                    "Start {HttpMethod} {Uri} [RequestId: {RequestId}]");

            private static readonly Action<ILogger, double, HttpStatusCode, string, Exception> _requestPipelineEnd =
                LoggerMessage.Define<double, HttpStatusCode, string>(
                    LogLevel.Information,
                    EventIds.PipelineEnd,
                    "End after {ElapsedMilliseconds}ms - {StatusCode} [RequestId: {RequestId}]");

            public static IDisposable BeginRequestPipelineScope(ILogger logger, HttpRequestMessage request)
            {
                var correlationId = GetCorrelationIdFromRequest(request);
                return _beginRequestPipelineScope(logger, request.Method, request.RequestUri, correlationId);
            }

            public static void RequestPipelineStart(ILogger logger, HttpRequestMessage request)
            {
                var correlationId = GetCorrelationIdFromRequest(request);
                _requestPipelineStart(logger, request.Method, request.RequestUri, correlationId, null);
            }

            public static void RequestPipelineEnd(ILogger logger, HttpResponseMessage response, TimeSpan duration, string id)
            {
                _requestPipelineEnd(logger, duration.TotalMilliseconds, response.StatusCode, id, null);
            }

            public static string GetCorrelationIdFromRequest(HttpRequestMessage request)
            {
                var correlationId = "Not set";

                if (request.Headers.TryGetValues("X-Request-Id", out var values))
                {
                    correlationId = values.First();
                }

                return correlationId;
            }
        }
    }
}

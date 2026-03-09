using Azure;
using Azure.Core;
using Azure.Core.Pipeline;

namespace OtelEvents.Azure.Storage.Tests;

/// <summary>
/// Mock Azure SDK HTTP pipeline transport for testing pipeline policies.
/// Returns predefined responses without making actual HTTP calls.
/// </summary>
internal sealed class MockPipelineTransport : HttpPipelineTransport
{
    private readonly Func<Request, MockPipelineResponse> _responseFactory;

    /// <summary>Records all requests processed by this transport.</summary>
    public List<Request> Requests { get; } = [];

    public MockPipelineTransport(int statusCode = 200, long? contentLength = null)
    {
        _responseFactory = _ =>
        {
            var response = new MockPipelineResponse(statusCode);
            if (contentLength.HasValue)
            {
                response.SetHeader("Content-Length", contentLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return response;
        };
    }

    public MockPipelineTransport(Func<Request, MockPipelineResponse> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public override Request CreateRequest() => new MockPipelineRequest();

    public override void Process(HttpMessage message)
    {
        Requests.Add(message.Request);
        message.Response = _responseFactory(message.Request);
    }

    public override ValueTask ProcessAsync(HttpMessage message)
    {
        Process(message);
        return default;
    }
}

/// <summary>
/// Mock Azure SDK HTTP request for testing.
/// </summary>
internal sealed class MockPipelineRequest : Request
{
    private readonly Dictionary<string, List<string>> _headers = new(StringComparer.OrdinalIgnoreCase);

    public override string ClientRequestId { get; set; } = Guid.NewGuid().ToString();

    protected override void AddHeader(string name, string value)
    {
        if (!_headers.TryGetValue(name, out var values))
        {
            values = [];
            _headers[name] = values;
        }
        values.Add(value);
    }

    protected override bool TryGetHeader(string name, out string value)
    {
        if (_headers.TryGetValue(name, out var values) && values.Count > 0)
        {
            value = string.Join(",", values);
            return true;
        }
        value = null!;
        return false;
    }

    protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
    {
        if (_headers.TryGetValue(name, out var vals))
        {
            values = vals;
            return true;
        }
        values = null!;
        return false;
    }

    protected override bool ContainsHeader(string name) => _headers.ContainsKey(name);

    protected override bool RemoveHeader(string name) => _headers.Remove(name);

    protected override IEnumerable<HttpHeader> EnumerateHeaders()
    {
        foreach (var kvp in _headers)
        {
            foreach (var value in kvp.Value)
            {
                yield return new HttpHeader(kvp.Key, value);
            }
        }
    }

    public override void Dispose() { }
}

/// <summary>
/// Mock Azure SDK HTTP response for testing.
/// </summary>
internal sealed class MockPipelineResponse : Response
{
    private readonly Dictionary<string, List<string>> _headers = new(StringComparer.OrdinalIgnoreCase);

    public override int Status { get; }
    public override string ReasonPhrase { get; }
    public override Stream? ContentStream { get; set; }
    public override string ClientRequestId { get; set; } = Guid.NewGuid().ToString();

    public MockPipelineResponse(int status, string reasonPhrase = "OK")
    {
        Status = status;
        ReasonPhrase = reasonPhrase;
    }

    public void SetHeader(string name, string value) => _headers[name] = [value];

    protected override bool TryGetHeader(string name, out string value)
    {
        if (_headers.TryGetValue(name, out var values) && values.Count > 0)
        {
            value = string.Join(",", values);
            return true;
        }
        value = null!;
        return false;
    }

    protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
    {
        if (_headers.TryGetValue(name, out var vals))
        {
            values = vals;
            return true;
        }
        values = null!;
        return false;
    }

    protected override bool ContainsHeader(string name) => _headers.ContainsKey(name);

    protected override IEnumerable<HttpHeader> EnumerateHeaders()
    {
        foreach (var kvp in _headers)
        {
            foreach (var value in kvp.Value)
            {
                yield return new HttpHeader(kvp.Key, value);
            }
        }
    }

    public override void Dispose() { }
}

/// <summary>
/// Test-only ClientOptions subclass to configure mock transports.
/// </summary>
internal sealed class TestClientOptions : ClientOptions
{
    public TestClientOptions()
    {
        Retry.MaxRetries = 0;
    }
}

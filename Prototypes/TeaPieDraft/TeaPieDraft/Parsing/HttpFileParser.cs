using System.Net.Http.Headers;
using System.Text;
using Consts = TeaPieDraft.Parsing.ParsingConstants;

namespace TeaPieDraft.Parsing;

internal class HttpFileParser
{
    internal static (Uri, HttpRequestMessage) ParseHttpFile(string fileContent)
    {
        IEnumerable<string?> lines = fileContent.Split(Environment.NewLine);

        // Parse the request
        var method = HttpMethod.Get;
        var requestUri = string.Empty;
        var httpClient = new System.Net.Http.HttpClient();
        var headers = httpClient.DefaultRequestHeaders;
        var content = new StringContent(string.Empty);
        var contentBuilder = new StringBuilder();
        var isBody = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                // Empty line indicates end of headers and beginning of body
                isBody = true;
                continue;
            }

            if (!isBody)
            {
                if (line.StartsWith(Consts.HttpGetMethodDirective, StringComparison.OrdinalIgnoreCase))
                {
                    method = HttpMethod.Get;
                    requestUri = line[(Consts.HttpGetMethodDirective.Length + 1)..].Trim();
                }
                else if (line.StartsWith(Consts.HttpPostMethodDirective, StringComparison.OrdinalIgnoreCase))
                {
                    method = HttpMethod.Post;
                    requestUri = line[(Consts.HttpPostMethodDirective.Length + 1)..].Trim();
                }
                else if (line.StartsWith(Consts.HttpPutMethodDirective, StringComparison.OrdinalIgnoreCase))
                {
                    method = HttpMethod.Put;
                    requestUri = line[(Consts.HttpPutMethodDirective.Length + 1)..].Trim();
                }
                else if (line.StartsWith(Consts.HttpDeleteMethodDirective, StringComparison.OrdinalIgnoreCase))
                {
                    method = HttpMethod.Delete;
                    requestUri = line[(Consts.HttpDeleteMethodDirective.Length + 1)..].Trim();
                }
                else if (line.Contains(':'))
                {
                    var headerParts = line.Split(':', 2);
                    var headerName = headerParts[0].Trim();
                    var headerValue = headerParts[1].Trim();
                    headers.TryAddWithoutValidation(headerName, headerValue);
                }
            }
            else
            {
                // Process the body
                contentBuilder.AppendLine(line);
            }
        }

        // Create the content for the request if there is a body
        if (contentBuilder.Length > 0)
        {
            content = new StringContent(contentBuilder.ToString().Trim(), Encoding.UTF8);
            if (headers.TryGetValues("Content-Type", out var contentType))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType.ToString());
            }
        }

        // Create and send the HTTP request
        var requestMessage = new HttpRequestMessage(method, requestUri)
        {
            Content = method != HttpMethod.Get ? content : null
        };

        // Add headers to the request
        foreach (var header in headers)
        {
            if (requestMessage.Headers.Contains(header.Key))
            {
                requestMessage.Headers.Remove(header.Key);
            }
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return (new Uri(requestUri), requestMessage);
    }
}

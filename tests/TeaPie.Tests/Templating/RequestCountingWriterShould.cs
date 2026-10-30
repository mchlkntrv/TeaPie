using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class RequestCountingWriterShould
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("### a\nGET https://example.test\n", 1)]
    [InlineData("GET https://example.test/1 ### a\npost https://example.test/2", 2)]
    [InlineData("###\r\nGET https://example.test\r\n###\r\nnote\r\n", 1)]
    [InlineData("GET https://example.test\n### last", 1)]
    [InlineData("#\n##\nGET https://example.test/1\nGET https://example.test/2", 1)]
    public void CountRequestsTheWayTheRequestsFileIsSplit(string output, int expected)
    {
        foreach (var chunkSize in new[] { 1, 2, output.Length + 1 })
        {
            var reported = new List<(int RequestCount, int Length)>();
            using var writer = new RequestCountingWriter((count, length) => reported.Add((count, length)));

            foreach (var chunk in output.Chunk(chunkSize))
            {
                writer.Write(chunk);
            }

            writer.Complete();

            writer.ToString().Should().Be(output);
            writer.RequestCount.Should().Be(expected, $"chunk size {chunkSize}");
            reported.Select(progress => progress.RequestCount).Should().BeInAscendingOrder();
            reported.LastOrDefault().Should().Be((expected, output.Length));
        }
    }
}

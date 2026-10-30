using System.Text.RegularExpressions;
using TeaPie.Http.Parsing;

namespace TeaPie.Templating;

// Counts requests while the text is written, splitting it the same way the requests file is split later: a
// separator starts at the first '###' after the previous separator and runs to the end of its line. After each
// write, onProgress gets the request count and the length written so far.
internal sealed partial class RequestCountingWriter(Action<int, int> onProgress) : StringWriter
{
    private const int SeparatorLength = 3;

    private readonly char[] _buffer = new char[4096];
    private int _scanned;
    private int _fragmentStart;
    private int _separatorStart = -1;
    private int _hashRun;

    public int RequestCount { get; private set; }

    public static bool ContainsRequestLine(string text) => RequestMethodAndUriLineRegex().IsMatch(text);

    public override void Write(char value)
    {
        base.Write(value);
        Scan();
    }

    public override void Write(char[] buffer, int index, int count)
    {
        base.Write(buffer, index, count);
        Scan();
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        base.Write(buffer);
        Scan();
    }

    public override void Write(string? value)
    {
        base.Write(value);
        Scan();
    }

    public void Complete()
    {
        Scan();
        if (CountFragment(_fragmentStart, _separatorStart >= 0 ? _separatorStart : _scanned))
        {
            onProgress(RequestCount, _scanned);
        }

        _fragmentStart = _scanned;
        _separatorStart = -1;
    }

    private void Scan()
    {
        var builder = GetStringBuilder();
        if (_scanned == builder.Length)
        {
            return;
        }

        while (_scanned < builder.Length)
        {
            var count = Math.Min(_buffer.Length, builder.Length - _scanned);
            builder.CopyTo(_scanned, _buffer, 0, count);

            for (var i = 0; i < count; i++)
            {
                Advance(_buffer[i], _scanned + i);
            }

            _scanned += count;
        }

        onProgress(RequestCount, _scanned);
    }

    private void Advance(char character, int position)
    {
        if (character == '\n')
        {
            if (_separatorStart >= 0)
            {
                CountFragment(_fragmentStart, _separatorStart);
                _fragmentStart = position + 1;
                _separatorStart = -1;
            }

            _hashRun = 0;
        }
        else if (_separatorStart < 0)
        {
            _hashRun = character == '#' ? _hashRun + 1 : 0;
            if (_hashRun == SeparatorLength)
            {
                _separatorStart = position - SeparatorLength + 1;
            }
        }
    }

    private bool CountFragment(int start, int end)
    {
        if (end > start && ContainsRequestLine(GetStringBuilder().ToString(start, end - start)))
        {
            RequestCount++;
            return true;
        }

        return false;
    }

    [GeneratedRegex(
        HttpFileParserConstants.RequestMethodAndUriLinePattern,
        RegexOptions.IgnoreCase, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex RequestMethodAndUriLineRegex();
}

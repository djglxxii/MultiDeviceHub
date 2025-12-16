using System.Text;

namespace Mdh.Core.Protocols;

/// <summary>
/// Extremely simple POCT1A framing: reads one standalone XML document by finding the root element
/// and reading until its matching closing tag appears.
/// </summary>
public sealed class SimpleXmlMessageFramer
{
    private readonly StringBuilder _buffer = new();

    public async Task<ReadOnlyMemory<byte>?> ReadNextMessageAsync(INetworkStreamAbstraction stream, CancellationToken cancellationToken)
    {
        var readBuffer = new byte[4096];

        while (true)
        {
            var extracted = TryExtractOneMessage();
            if (extracted is not null)
            {
                return extracted;
            }

            var read = await stream.ReadAsync(readBuffer, cancellationToken);
            if (read <= 0)
            {
                return null;
            }

            _buffer.Append(Encoding.UTF8.GetString(readBuffer, 0, read));
        }
    }

    private ReadOnlyMemory<byte>? TryExtractOneMessage()
    {
        var s = _buffer.ToString();

        // Find first real root start tag (skip XML declaration).
        var start = s.IndexOf('<');
        while (start >= 0 && start + 1 < s.Length && s[start + 1] == '?')
        {
            var declEnd = s.IndexOf("?>", start, StringComparison.Ordinal);
            if (declEnd < 0)
            {
                return null;
            }

            start = s.IndexOf('<', declEnd + 2);
        }

        if (start < 0)
        {
            return null;
        }

        // Read root name.
        var gt = s.IndexOf('>', start);
        if (gt < 0)
        {
            return null;
        }

        var nameStart = start + 1;
        if (nameStart < s.Length && s[nameStart] == '/')
        {
            // Buffer starts with an end tag; drop it and continue.
            _buffer.Remove(0, Math.Min(gt + 1, _buffer.Length));
            return null;
        }

        var nameEnd = nameStart;
        while (nameEnd < gt)
        {
            var ch = s[nameEnd];
            if (char.IsWhiteSpace(ch) || ch == '/' || ch == '>')
            {
                break;
            }

            nameEnd++;
        }

        if (nameEnd <= nameStart)
        {
            return null;
        }

        var rootName = s.Substring(nameStart, nameEnd - nameStart);
        var closeTag = $"</{rootName}>";

        var closeIdx = s.IndexOf(closeTag, gt + 1, StringComparison.Ordinal);
        if (closeIdx < 0)
        {
            return null;
        }

        var endExclusive = closeIdx + closeTag.Length;
        var xml = s.Substring(start, endExclusive - start);

        // Remove extracted message from buffer.
        _buffer.Remove(0, endExclusive);

        return Encoding.UTF8.GetBytes(xml);
    }
}

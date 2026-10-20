namespace Synthient;

// Skips the blank keepalive lines the API sends.
internal sealed class NdjsonReader(Stream stream)
{
    internal const int MaxLineSize = 16 << 20;

    private byte[] _buffer = new byte[64 << 10];
    private int _start;
    private int _end;
    private bool _eof;

    // The line points into the read buffer, so it is only valid until the next call.
    public async ValueTask<ReadOnlyMemory<byte>?> ReadLineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pending = _buffer.AsMemory(_start, _end - _start);
            var newline = pending.Span.IndexOf((byte)'\n');
            ReadOnlyMemory<byte> line;
            if (newline >= 0)
            {
                line = pending[..newline];
                _start += newline + 1;
            }
            else if (_eof)
            {
                if (pending.IsEmpty)
                {
                    return null;
                }
                line = pending;
                _start = _end;
            }
            else
            {
                await FillAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            line = line.Trim(" \t\r\n\f\v"u8);
            if (!line.IsEmpty)
            {
                return line;
            }
        }
    }

    private async ValueTask FillAsync(CancellationToken cancellationToken)
    {
        if (_start > 0)
        {
            _buffer.AsSpan(_start, _end - _start).CopyTo(_buffer);
            _end -= _start;
            _start = 0;
        }
        if (_end == _buffer.Length)
        {
            // The buffer holds one unterminated line. It may grow to the line limit plus the newline.
            if (_buffer.Length > MaxLineSize)
            {
                throw new SynthientException($"A stream event exceeds {MaxLineSize} bytes.");
            }
            Array.Resize(ref _buffer, Math.Min(_buffer.Length * 2, MaxLineSize + 1));
        }
        var read = await stream.ReadAsync(_buffer.AsMemory(_end), cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            _eof = true;
        }
        _end += read;
    }
}

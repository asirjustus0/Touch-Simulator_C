using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace TouchSimulator.Recording;

/// <summary>
/// Pure C# MJPEG-in-AVI writer. No Python, codecs, NuGet packages, or external
/// executables. Video contains screen frames without audio; FPS is nominal.
/// </summary>
public sealed class MjpegAviRecorder : IDisposable
{
    private readonly FileStream _file;
    private readonly BinaryWriter _writer;
    private readonly int _width, _height;
    private readonly List<(uint offset, uint size)> _index = new();
    private long _riffSizeOffset, _avihFramesOffset, _strhFramesOffset;
    private long _moviSizeOffset, _moviFourccOffset;
    private bool _finished;

    public string FilePath { get; }
    public int FramesWritten => _index.Count;

    public MjpegAviRecorder(string outputPath, int width, int height, int fps)
    {
        if (width <= 0 || height <= 0 || width > short.MaxValue || height > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (fps < 1 || fps > 10) throw new ArgumentOutOfRangeException(nameof(fps));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        _width = width; _height = height;
        FilePath = outputPath;
        _file = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        _writer = new BinaryWriter(_file, Encoding.ASCII, leaveOpen: false);
        BuildHeader(fps);
    }

    private void BuildHeader(int fps)
    {
        FourCC("RIFF"); _riffSizeOffset = Position; U32(0); FourCC("AVI ");
        FourCC("LIST"); long hdrlSizeOffset = Position; U32(0); FourCC("hdrl");
        FourCC("avih"); U32(56);
        U32((uint)(1_000_000 / fps)); // microseconds / frame
        U32((uint)Math.Min((long)_width * _height * 3 * fps, uint.MaxValue));
        U32(0); U32(0x10); // AVIF_HASINDEX
        _avihFramesOffset = Position; U32(0);
        U32(0); U32(1); U32(0);
        U32((uint)_width); U32((uint)_height);
        for (int i = 0; i < 4; i++) U32(0);

        FourCC("LIST"); long strlSizeOffset = Position; U32(0); FourCC("strl");
        FourCC("strh"); U32(56);
        FourCC("vids"); FourCC("MJPG"); U32(0);
        _writer.Write((ushort)0); _writer.Write((ushort)0); // priority, language
        U32(0); U32(1); U32((uint)fps); U32(0); // initialframes, scale, rate, start
        _strhFramesOffset = Position; U32(0);
        U32((uint)Math.Min((long)_width * _height * 3, uint.MaxValue));
        U32(uint.MaxValue); U32(0); // quality, sample size
        _writer.Write((short)0); _writer.Write((short)0);
        _writer.Write((short)_width); _writer.Write((short)_height);

        FourCC("strf"); U32(40);
        U32(40); _writer.Write(_width); _writer.Write(_height);
        _writer.Write((ushort)1); _writer.Write((ushort)24);
        FourCC("MJPG");
        U32((uint)Math.Min((long)_width * _height * 3, uint.MaxValue));
        U32(0); U32(0); U32(0); U32(0);
        PatchSize(strlSizeOffset, Position);
        PatchSize(hdrlSizeOffset, Position);

        FourCC("LIST"); _moviSizeOffset = Position; U32(0);
        _moviFourccOffset = Position;
        FourCC("movi");
    }

    public void AddFrame(Bitmap bitmap)
    {
        if (_finished) throw new ObjectDisposedException(nameof(MjpegAviRecorder));
        if (bitmap.Width != _width || bitmap.Height != _height)
            throw new InvalidOperationException("Target size changed while recording. Stop and restart recording.");
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Jpeg);
        if (stream.Length > uint.MaxValue) throw new InvalidOperationException("Frame is too large for AVI.");
        uint length = (uint)stream.Length;
        long offset = Position - _moviFourccOffset;
        if (offset > uint.MaxValue) throw new InvalidOperationException("AVI recording exceeded 4 GiB. Stop recording.");
        FourCC("00dc"); U32(length);
        stream.Position = 0;
        stream.CopyTo(_file);
        if ((length & 1) != 0) _writer.Write((byte)0);
        _index.Add(((uint)offset, length));
    }

    public void Finish()
    {
        if (_finished) return;
        _finished = true;
        long moviEnd = Position;
        PatchSize(_moviSizeOffset, moviEnd);
        FourCC("idx1"); U32((uint)(_index.Count * 16));
        foreach (var item in _index)
        {
            FourCC("00dc"); U32(0x10); U32(item.offset); U32(item.size);
        }
        PatchU32(_avihFramesOffset, (uint)_index.Count);
        PatchU32(_strhFramesOffset, (uint)_index.Count);
        PatchSize(_riffSizeOffset, Position);
        _writer.Flush();
    }

    private long Position => _file.Position;
    private void FourCC(string value) => _writer.Write(Encoding.ASCII.GetBytes(value));
    private void U32(uint value) => _writer.Write(value);
    private void PatchSize(long sizeOffset, long endPosition)
    {
        long size = endPosition - sizeOffset - sizeof(uint);
        if (size < 0 || size > uint.MaxValue) throw new InvalidOperationException("AVI 4 GiB size limit exceeded.");
        PatchU32(sizeOffset, (uint)size);
    }
    private void PatchU32(long offset, uint value)
    {
        long current = Position;
        _file.Position = offset;
        U32(value);
        _file.Position = current;
    }

    public void Dispose()
    {
        try { Finish(); }
        finally { _writer.Dispose(); }
    }
}

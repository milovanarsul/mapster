using System.Buffers.Binary;
using System.Collections;
using System.IO.MemoryMappedFiles;

using Protobuf = Google.Protobuf;

namespace OSMDataParser;

public class PBFFile : IEnumerable<Blob>
{
    private bool _disposedValue = false;
    private MemoryMappedFile _mmapFile;

    public PBFFile(string filePath)
    {
        _mmapFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
    }

    public IEnumerator<Blob> GetEnumerator()
    {
        return new BlobEnumerator(_mmapFile.CreateViewStream());
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _mmapFile.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public class BlobEnumerator : IEnumerator<Blob>
{
    const int MAX_BLOB_HEADER_SIZE = 64 * 1024;
    const int MAX_BLOB_MESSAGE_SIZE = 32 * 1024 * 1024;

    private static ThreadLocal<byte[]> _headerSizeBuffer = new ThreadLocal<byte[]>(() => new byte[4]);

    private bool _disposedValue = false;
    private MemoryMappedViewStream _memoryMappedStream;
    private Blob _currentBlob = new Blob();

    public Blob Current => _currentBlob;

    object IEnumerator.Current => Current;

    public BlobEnumerator(MemoryMappedViewStream mmapStream)
    {
        mmapStream.Seek(0, SeekOrigin.Begin);
        _memoryMappedStream = mmapStream;
    }

    public bool MoveNext()
    {
        MemoryMappedViewStream stream = _memoryMappedStream;

        if (stream.Position == stream.Length)
            return false;

        var remainingBytes = (stream.Length - 1) - stream.Position;
        if (remainingBytes == 0)
            throw new IndexOutOfRangeException($"Not enough bytes to read header size at position {stream.Position}; expected 4 bytes -> available {remainingBytes} bytes");

        var headerSizeBuffer = _headerSizeBuffer.Value!;
        stream.Read(headerSizeBuffer, 0, headerSizeBuffer.Length);
        var headerSize = (int)BinaryPrimitives.ReadUInt32BigEndian(headerSizeBuffer.AsSpan());

        if (headerSize >= MAX_BLOB_HEADER_SIZE)
            throw new ArgumentOutOfRangeException($"Header is too large. Header size {headerSize} exceeds the maximum of {MAX_BLOB_HEADER_SIZE} bytes");


        var blobHeader = DeserializeMessage<OSMPBF.BlobHeader>(stream, headerSize);

        if (blobHeader.Datasize >= MAX_BLOB_MESSAGE_SIZE)
            throw new ArgumentOutOfRangeException($"Blob is too large. Blob size {blobHeader.Datasize} exceeds the maximum of {MAX_BLOB_MESSAGE_SIZE} bytes");

        var protoBlob = DeserializeMessage<OSMPBF.Blob>(stream, blobHeader.Datasize);
        BlobType blobType = BlobType.Unknown;
        switch (blobHeader.Type)
        {
            case "OSMHeader":
                blobType = BlobType.Header;
                break;

            case "OSMData":
                blobType = BlobType.Primitive;
                break;

            default:
                throw new InvalidDataException($"Unknown or unsupported blob type: {blobHeader.Type}");
        }

        switch (protoBlob.DataCase)
        {
            case OSMPBF.Blob.DataOneofCase.Raw:
                {
                    _currentBlob = new Blob(blobType, isCompressed: true, protoBlob.Raw.Memory);
                    break;
                }
            case OSMPBF.Blob.DataOneofCase.ZlibData:
                {
                    _currentBlob = new Blob(blobType, isCompressed: true, protoBlob.ZlibData.Memory);
                    break;
                }
            case OSMPBF.Blob.DataOneofCase.None: throw new InvalidDataException($"Blob does not contain any data");
            case OSMPBF.Blob.DataOneofCase.LzmaData:
            case OSMPBF.Blob.DataOneofCase.OBSOLETEBzip2Data:
            case OSMPBF.Blob.DataOneofCase.Lz4Data:
            case OSMPBF.Blob.DataOneofCase.ZstdData:
                throw new InvalidDataException($"Unsupported data compression used in blob: {protoBlob.DataCase}");
        }

        return true;
    }

    public void Reset()
    {
        _memoryMappedStream?.Seek(0, SeekOrigin.Begin);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _memoryMappedStream?.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    static T DeserializeMessage<T>(MemoryMappedViewStream stream, int size) where T : Protobuf.IMessage<T>, new()
    {
        var remainingBytes = stream.Length - stream.Position;
        if (remainingBytes < size)
            throw new IndexOutOfRangeException($"Not enough bytes to read data at position {stream.Position}. Expected {size} bytes -> available {remainingBytes} bytes");

        T result;
        unsafe
        {
            byte* buffer = null;

            var memoryHandle = stream.SafeMemoryMappedViewHandle;
            memoryHandle.AcquirePointer(ref buffer);
            {
                result = (new Protobuf.MessageParser<T>(() => new T())).ParseFrom(new ReadOnlySpan<byte>((void*)(buffer + stream.Position), size));
            }
            memoryHandle.ReleasePointer();
        }
        stream.Seek((long)size, SeekOrigin.Current);

        return result;
    }
}


/// <summary>
/// Represents a management component for block allocation and tracking within a file system.
/// </summary>
public class SuperBlock
{
    // Fields
    private readonly FileStream _fs;
    private readonly long _blocks;
    private readonly long _memoryBlocks;
    private readonly long ReservedBlocks;
    
    public const long BlockSize = 4096;
    public SuperBlock(FileStream fs)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));

        var driveSize = _fs.Length;
        if (driveSize % BlockSize != 0)
            throw new ArgumentException("Drive size is not a multiple of block size.");

        _blocks = driveSize / BlockSize;
        _memoryBlocks = _blocks / 8; // Assuming each block occupies 8 bytes in memory
        ReservedBlocks = _memoryBlocks/BlockSize;
    }
    public bool IsOccupied(long position)
    {
        if (position < 0 || position >= _blocks)
            throw new ArgumentOutOfRangeException(nameof(position), "Position is out of range.");

        _fs.Position = position / 8;
        var currentByte = _fs.ReadByte();
        if (currentByte == -1)
            throw new EndOfStreamException("End of stream reached while reading.");

        return (currentByte & (1 << (int)(position % 8))) != 0;
    }
    public void SetOccupancy(long position, bool state)
    {
        if (position < 0 || position >= _blocks)
            throw new ArgumentOutOfRangeException(nameof(position), "Position is out of range.");

        _fs.Position = position / 8;
        var currentByte = _fs.ReadByte();
        if (currentByte == -1)
            throw new EndOfStreamException("End of stream reached while reading.");

        var bitMask = (byte)(1 << (int)(position % 8));
        if (((currentByte & bitMask) != 0) != state)
        {

            if (state)
            {
                currentByte |= bitMask;
                StateDelta++;
            }
            else
            {
                currentByte &= (byte)~bitMask;
                StateDelta--;
            }


            _fs.Position--;
            _fs.WriteByte((byte)currentByte);
            _fs.Flush();
            WritesDelta++;
        }
    }
    public long FindFreeBlock()
    {
        for (int i = 0; i < 16; i++)
        {
            long randomLong = (long)(Random.Shared.NextDouble() * (_blocks - ReservedBlocks)) + ReservedBlocks;
            if(!IsOccupied(randomLong))
                return randomLong;
        }
        for (long i = ReservedBlocks; i < _blocks; i++)
            if(!IsOccupied(i))
                return i; 
        throw new Exception("Drive is full!");
    }
    public long FindAndOccupyFreeBlock()
    {
        var blk = FindFreeBlock();
        SetOccupancy(blk,true);
        return blk;
    }
    public void ClearBlock(long block)
    {
        WriteToBlock(block,0,new byte[BlockSize]);
    }

    public void WriteToBlock(long block,int offset,ReadOnlySpan<byte> bytes)
    {
        if(!IsOccupied(block))
            throw new Exception("Block is not occupied");
        if(block<ReservedBlocks)
            throw new Exception($"This block [{block}] reserved for system needs!");
        if(bytes.Length>BlockSize)
            throw new Exception($"Buffer is too long! {bytes.Length} out of {BlockSize}");
        var max = offset +  bytes.Length;
        if(max>BlockSize)
            throw new Exception($"Trying to write more data than you posibly can");
        _fs.Seek(GetBlockPos(block)+offset,SeekOrigin.Begin);
        _fs.Write(bytes);
        _fs.Flush();
    }
    public byte[] ReadFromBlock(long block, int offset, long length)
    {
        if(block<ReservedBlocks)
            throw new Exception($"This block [{block}] reserved for system needs!");
        var max = offset +  length;
        if(max>BlockSize)
            throw new Exception($"Trying to get more data than you posibly can");
        _fs.Seek(GetBlockPos(block)+offset,SeekOrigin.Begin);
        var buffer = new byte[length];
        _fs.Read(buffer);
        return buffer;
    }
    
    public long WritesDelta {get;private set;} = 0;
    public long StateDelta {get;private set;} = 0;
    public long Blocks => _blocks;
    public long MemoryBlocks => _memoryBlocks;
    private static long GetBlockPos(long block) => block*BlockSize;
}
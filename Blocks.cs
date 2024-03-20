using System.Text;

public abstract class IBlock(long pointer){
    public readonly long BlockPointer = pointer;
}
public class FileBlock(long pointer,SuperBlock sb) : ExtensionBlock(pointer)
{
    #region Metadata
    private int NameOffset => sb.ReadFromBlock(base.BlockPointer, 0, 1)[0] + 1;
    public string Name{
    get{
        var c = (int)sb.ReadFromBlock(base.BlockPointer,0,1)[0];
        return Encoding.UTF8.GetString(sb.ReadFromBlock(base.BlockPointer,1,c));
    }
    set{
        if(value.Length>255)
            throw new Exception("Name is too long!");
        var data = Encoding.UTF8.GetBytes(value);
        if(data.Length>255)
            throw new Exception("Name is too long!");
        long data1 = CreateDate;
        long data2 = LastModifyDate;
        byte data3 = Flags;
        long data4 = Length;
        uint data5 = BlocksCount;
        sb.WriteToBlock(base.BlockPointer,0,[(byte)data.Length]);
        sb.WriteToBlock(base.BlockPointer,1,data);
        CreateDate = data1;
        LastModifyDate = data2;
        Flags = data3;
        Length = data4;
        BlocksCount = data5;
    }}
    public long CreateDate{
        get => BitConverter.ToInt64(sb.ReadFromBlock(base.BlockPointer, NameOffset, sizeof(long)));
        set => sb.WriteToBlock(base.BlockPointer, NameOffset, BitConverter.GetBytes(value));
    }
    public long LastModifyDate{
        get => BitConverter.ToInt64(sb.ReadFromBlock(base.BlockPointer, NameOffset+sizeof(long), sizeof(long)));
        set => sb.WriteToBlock(base.BlockPointer, NameOffset+sizeof(long), BitConverter.GetBytes(value));
    }
    public byte Flags{
        get => sb.ReadFromBlock(base.BlockPointer, NameOffset+(2*sizeof(long)), sizeof(byte))[0];
        set => sb.WriteToBlock(base.BlockPointer, NameOffset+(2*sizeof(long)), [value]);
    }
    public long Length{
        get => BitConverter.ToInt64(sb.ReadFromBlock(base.BlockPointer, NameOffset+(2*sizeof(long))+1, sizeof(long)));
        set => sb.WriteToBlock(base.BlockPointer, NameOffset+(2*sizeof(long))+1, BitConverter.GetBytes(value));
    }
    public uint BlocksCount{
        get => BitConverter.ToUInt32(sb.ReadFromBlock(base.BlockPointer, NameOffset+(3*sizeof(long))+1, sizeof(uint)));
        set => sb.WriteToBlock(base.BlockPointer, NameOffset+(3*sizeof(long))+1, BitConverter.GetBytes(value));
    }
    #endregion
    public const int metadataoffset = 256+sizeof(long)+sizeof(long)+sizeof(byte)+sizeof(long)+sizeof(uint);
    public override long MaxAddreses => ((SuperBlock.BlockSize-metadataoffset)/sizeof(long))-1;
    public void AllocateData(SuperBlock sb, Stream data)
    {
        Length = data.Length;
        BlocksCount = (uint)Math.Ceiling(((double)data.Length)/SuperBlock.BlockSize);

        ExtensionBlock currentBlock = this;
        var needToAllocate = BlocksCount;
        uint allocated = 0;
        while (needToAllocate!=0)
        {
            var allocate = (uint)Math.Min(needToAllocate,currentBlock.MaxAddreses);
            currentBlock.FillFreeBlocks(sb,allocate);
            allocated+=allocate;
            needToAllocate-=allocate;
            if(needToAllocate!=0)
            {
                currentBlock.SetFreeExtentionBlock(sb);
                currentBlock = new ExtensionBlock(currentBlock.GetExtentionBlock(sb));
                sb.ClearBlock(currentBlock.BlockPointer);
            }
        }
        //System.Console.WriteLine($"Occupied {sb.WritesDelta-faf} we | {allocated} woe blocks");
        //System.Console.WriteLine($"Predicted {BlocksCount} + extentions");

        needToAllocate = BlocksCount;
        currentBlock = this;
        byte[] buffer = new byte[SuperBlock.BlockSize];
        while (needToAllocate!=0)
        {
            var allocate = (uint)Math.Min(needToAllocate,currentBlock.MaxAddreses);
            for (int i = 0; i < allocate; i++)
            {
                var c = currentBlock.GetAddress(sb,i);
                int l = data.Read(buffer);
                sb.WriteToBlock(c,0,buffer.AsSpan(0,l));
            }
            needToAllocate-=allocate;
            if(needToAllocate!=0)
                currentBlock = new ExtensionBlock(currentBlock.GetExtentionBlock(sb));
        }
    }
    public byte[] ReadData(SuperBlock sb)   
    {
        byte[] data = new byte[Length];

        var currentBlock = (ExtensionBlock)this;
        var needToAllocate = BlocksCount;
        var last = Length;
        var offset = 0;
        while (needToAllocate!=0)
        {
            var allocate = (uint)Math.Min(needToAllocate,currentBlock.MaxAddreses);
            for (int i = 0; i < allocate; i++)
            {
                var c = currentBlock.GetAddress(sb,i);
                sb.ReadFromBlock(c,0,(int)Math.Min(last,SuperBlock.BlockSize)).CopyTo(data,offset);
                offset+=(int)SuperBlock.BlockSize;
                last-=(int)SuperBlock.BlockSize;
                if(last==0)
                    return data;
            }
            needToAllocate-=allocate;
            if(needToAllocate!=0)
                currentBlock = new ExtensionBlock(currentBlock.GetExtentionBlock(sb));
        }
        return data;
    }
    public void ReadData(SuperBlock sb, Stream stream)
    {
        var currentBlock = (ExtensionBlock)this;
        var needToAllocate = BlocksCount;
        var last = Length;
        while (needToAllocate!=0)
        {
            var allocate = (uint)Math.Min(needToAllocate,currentBlock.MaxAddreses);
            for (int i = 0; i < allocate; i++)
            {
                var c = currentBlock.GetAddress(sb,i);
                stream.Write(sb.ReadFromBlock(c,0,(int)Math.Min(last,SuperBlock.BlockSize)));
                last-=(int)SuperBlock.BlockSize;
                if(last==0)
                    return;
            }
            needToAllocate-=allocate;
            if(needToAllocate!=0)
                currentBlock = new ExtensionBlock(currentBlock.GetExtentionBlock(sb));
        }
    }

    public override long GetAddress(SuperBlock sb, int i){
        var offs = metadataoffset+(sizeof(long)-(metadataoffset%sizeof(long)))+(i*sizeof(long));
        var data = sb.ReadFromBlock(base.BlockPointer,offs,sizeof(long));
        return BitConverter.ToInt64(data);
    }
    public override void SetAddress(SuperBlock sb, int i, long block)
    {
        var offs = metadataoffset+(sizeof(long)-(metadataoffset%sizeof(long)))+(i*sizeof(long));
        var data = BitConverter.GetBytes(block);
        sb.WriteToBlock(base.BlockPointer,offs,data);
    }
}
public class ExtensionBlock(long pointer) : IBlock(pointer){
    public virtual long MaxAddreses {get;} = (SuperBlock.BlockSize/sizeof(long))-1;
    public virtual void SetAddress(SuperBlock sb,int i,long block)
    {
        var full = (int)(i*sizeof(long));
        var data = BitConverter.GetBytes(block);
        sb.WriteToBlock(base.BlockPointer,full,data);
    }
    public virtual long GetAddress(SuperBlock sb,int i)
    {
        var full = (int)(i*sizeof(long));
        var data = sb.ReadFromBlock(base.BlockPointer,full,sizeof(long));
        return BitConverter.ToInt64(data);
    }
    public long GetExtentionBlock(SuperBlock sb)
    {
        var full = (int)(SuperBlock.BlockSize - sizeof(long));
        var data = sb.ReadFromBlock(base.BlockPointer, full, sizeof(long)); // Corrected sizeof(long) here
        return BitConverter.ToInt64(data); // Added offset parameter
    }
    public void SetFreeExtentionBlock(SuperBlock sb)
    {
        var blk = sb.FindAndOccupyFreeBlock();
        sb.ClearBlock(blk);

        var full = (int)(SuperBlock.BlockSize-sizeof(long));
        var data = BitConverter.GetBytes(blk);
        sb.WriteToBlock(base.BlockPointer,full,data);

    }
    public void FillFreeBlocks(SuperBlock sb,long count)
    {
        for (int i = 0; i < count; i++)
        {
            if(GetAddress(sb,i)<=0)
            {
                var blk = sb.FindAndOccupyFreeBlock();
                SetAddress(sb,i,blk);
            }
        }
    }
}
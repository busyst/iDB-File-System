using System.Text;

public abstract class IBlock
{
    public long PointerToBlock;

    protected IBlock(long pointer)
    {
        PointerToBlock = pointer;
    }
}
public class FileBlock(long pointer,SuperBlock sb) : ExtensionBlock(pointer)
{
    #region Metadata
    public string Name{
    get{
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        return Encoding.UTF8.GetString(sb.ReadFromBlock(base.PointerToBlock,1,c));
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
        sb.WriteToBlock(base.PointerToBlock,0,[(byte)data.Length]);
        sb.WriteToBlock(base.PointerToBlock,1,data);
        CreateDate = data1;
        LastModifyDate = data2;
        Flags = data3;
        Length = data4;
        BlocksCount = data5;
    }}
    public long CreateDate{
    get{
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        return BitConverter.ToInt64(sb.ReadFromBlock(base.PointerToBlock,1+c,sizeof(long)));
    }
    set{
        var data = BitConverter.GetBytes(value);
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        sb.WriteToBlock(base.PointerToBlock,1+c,data);
    }}
    public long LastModifyDate{
    get{
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        return BitConverter.ToInt64(sb.ReadFromBlock(base.PointerToBlock,1+c+sizeof(long),sizeof(long)));
    }
    set{
        var data = BitConverter.GetBytes(value);
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        sb.WriteToBlock(base.PointerToBlock,1+c+sizeof(long),data);
    }}
    public byte Flags{
    get{
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        return sb.ReadFromBlock(base.PointerToBlock,1+c+sizeof(long)+sizeof(long),1)[0];
    }
    set{
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        sb.WriteToBlock(base.PointerToBlock,1+c+sizeof(long)+sizeof(long),[value]);
    }}
    public long Length{
    get{
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        return BitConverter.ToInt64(sb.ReadFromBlock(base.PointerToBlock,1+c+sizeof(long)+sizeof(long)+1,sizeof(long)));
    }
    set{
        var data = BitConverter.GetBytes(value);
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        sb.WriteToBlock(base.PointerToBlock,1+c+sizeof(long)+sizeof(long)+1,data);
    }}
    public uint BlocksCount{
    get{
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        return BitConverter.ToUInt32(sb.ReadFromBlock(base.PointerToBlock,1+c+sizeof(long)+sizeof(long)+1+sizeof(long),sizeof(uint)));
    }
    set{
        var data = BitConverter.GetBytes(value);
        var c = (int)sb.ReadFromBlock(base.PointerToBlock,0,1)[0];
        sb.WriteToBlock(base.PointerToBlock,1+c+sizeof(long)+sizeof(long)+1+sizeof(long),data);
    }}
    #endregion
    public long[] blocksPointer = [];
    public const int metadataoffset = 256+sizeof(long)+sizeof(long)+sizeof(byte)+sizeof(long)+sizeof(uint);
    public override long MaxAddreses => ((SuperBlock.BlockSize-metadataoffset)/sizeof(long))-1;
    public static FileBlock CreateNow(string name,SuperBlock sb)
    {
        var blk = sb.FindFreeBlock();
        sb.SetOccupancy(blk,true);
        var c = new FileBlock(blk,sb);
        var tt = DateTime.UtcNow.Ticks;
        c.CreateDate = tt;
        c.LastModifyDate = tt;
        c.Name = name;
        
        return c;
    }
    public void UpdateData(SuperBlock sb, MemoryStream data)
    {
        var blocks = (uint)Math.Ceiling(((double)data.Length)/SuperBlock.BlockSize);
        BlocksCount = blocks;
        Length = data.Length;
        var needToAllocate = BlocksCount;
        var currentBlock = (ExtensionBlock)this;
        var faf = sb.WritesDelta;
        while (needToAllocate!=0)
        {
            var allocate = (uint)Math.Min(needToAllocate,currentBlock.MaxAddreses);
            currentBlock.FillFreeBlocks(sb,allocate);
            needToAllocate-=allocate;
            if(needToAllocate!=0)
            {
                currentBlock.SetFreeExtentionBlock(sb);
                currentBlock = new ExtensionBlock(currentBlock.GetExtentionBlock(sb));
                currentBlock.Clear(sb);
                System.Console.WriteLine("+++");
            }
        }
        System.Console.WriteLine($"Occupied {sb.WritesDelta-faf} blocks");
        System.Console.WriteLine($"Predicted {BlocksCount} + extentions");

        needToAllocate = BlocksCount;
        currentBlock = (ExtensionBlock)this;
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
            {
                currentBlock = new ExtensionBlock(currentBlock.GetExtentionBlock(sb));
                System.Console.WriteLine("+++");
            }
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
            {
                currentBlock = new ExtensionBlock(currentBlock.GetExtentionBlock(sb));
                System.Console.WriteLine("+++");
            }
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
            {
                currentBlock = new ExtensionBlock(currentBlock.GetExtentionBlock(sb));
                System.Console.WriteLine("+++");
            }
        }
        return;
    }
    public override void FillFreeBlocks(SuperBlock sb, long count)
    {
        long[] adresses = new long[count];
        for (int i = 0; i < MaxAddreses; i++)
        {
            if(i<count)
            {
                if(i<blocksPointer.Length)
                    adresses[i] = blocksPointer[i];
                else
                {
                    var blk = sb.FindFreeBlock();
                    sb.SetOccupancy(blk,true);
                    adresses[i] = blk;
                }
            }
            else if(i<blocksPointer.Length)
                sb.SetOccupancy(blocksPointer[i],false);
        }
        blocksPointer = adresses;
    }
    public override long GetAddress(SuperBlock sb, int i) => blocksPointer[i];
}
public class ExtensionBlock(long pointer) : IBlock(pointer)
{
    public virtual long MaxAddreses {get;} = (SuperBlock.BlockSize/sizeof(long))-1;
    public void Clear(SuperBlock sb) => sb.WriteToBlock(base.PointerToBlock, 0, new byte[SuperBlock.BlockSize]);
    public void SetAddress(SuperBlock sb,int i,long block)
    {
        var full = (int)(i*sizeof(long));
        var data = BitConverter.GetBytes(block);
        sb.WriteToBlock(base.PointerToBlock,full,data);
    }
    public virtual long GetAddress(SuperBlock sb,int i)
    {
        var full = (int)(i*sizeof(long));
        var data = sb.ReadFromBlock(base.PointerToBlock,full,sizeof(long));
        return BitConverter.ToInt64(data);
    }
    public long GetExtentionBlock(SuperBlock sb)
    {
        var full = (int)(SuperBlock.BlockSize - sizeof(long));
        var data = sb.ReadFromBlock(base.PointerToBlock, full, sizeof(long)); // Corrected sizeof(long) here
        return BitConverter.ToInt64(data); // Added offset parameter
    }
    private void SetExtentionBlock(SuperBlock sb,long block)
    {
        var full = (int)(SuperBlock.BlockSize-sizeof(long));
        var data = BitConverter.GetBytes(block);
        sb.WriteToBlock(base.PointerToBlock,full,data);
        new ExtensionBlock(block).Clear(sb);
    }
    public void SetFreeExtentionBlock(SuperBlock sb)
    {
        var blk = sb.FindFreeBlock();
        sb.SetOccupancy(blk,true);
        SetExtentionBlock(sb,blk);
    }
    public virtual void FillFreeBlocks(SuperBlock sb,long count)
    {
        for (int i = 0; i < count; i++)
        {
            if(GetAddress(sb,i)<=0)
            {
                var blk = sb.FindFreeBlock();
                sb.SetOccupancy(blk,true);
                SetAddress(sb,i,blk);
            }
        }
    }
}
using System.Text;

public class DirectoryBlock(SuperBlock sb, long block)
{
    public string Name{
    get{
        var c = (int)sb.ReadFromBlock(block,0,1)[0];
        return Encoding.UTF8.GetString(sb.ReadFromBlock(block,1,c));
    }
    set{
        if(value.Length>255)
            throw new Exception("Name is too long!");
        var data = Encoding.UTF8.GetBytes(value);
        if(data.Length>255)
            throw new Exception("Name is too long!");
        sb.WriteToBlock(block,0,[(byte)data.Length]);
        sb.WriteToBlock(block,1,data);
    }}

    private static readonly byte[] buffer = new byte[sizeof(long)*2];
    public const int offset = 256;
    public const long MaxBlocksPerBlock = ((SuperBlock.BlockSize-offset)/(sizeof(long)*2))-1;
     private (ulong metadata,long pointer) GetDataEntry(int i)
    {
        if(i>(int)MaxBlocksPerBlock)
            throw new IndexOutOfRangeException();
        sb.ReadFromBlock(block,offset+(i*sizeof(long)*2),sizeof(long)*2).CopyTo(buffer,0);
        
        var meta = BitConverter.ToUInt64(buffer,0);
        var point = BitConverter.ToInt64(buffer,sizeof(long));
        return (meta,point);
    }
    private void SetDataEntry(int i,(ulong metadata,long block) value)
    {
        var d = new BlockMetadata(GetDataEntry(i).metadata);
        if(d.Type!=BlockMetadata.BlockType.None)
            throw new Exception("Block is ocupied!");
        if(i>(int)MaxBlocksPerBlock)
            throw new IndexOutOfRangeException();
        BitConverter.GetBytes(value.metadata).CopyTo(buffer,0);
        BitConverter.GetBytes(value.block).CopyTo(buffer,sizeof(long));
        sb.WriteToBlock(block,offset+(i*16),buffer);
    }
    public void Clear() => sb.WriteToBlock(block, 0, new byte[SuperBlock.BlockSize]);

    public void Display()
    {
        int offset = 0;
        DirectoryBlock c = (DirectoryBlock)this;
        while (true)
        {        
            for (int i = 0; i < MaxBlocksPerBlock; i++)
            {
                var entr = c.GetDataEntry(i);  
                var meta = new BlockMetadata(entr.metadata);
                string flags = "";
                flags+=meta.Archived?"A":"-";
                flags+=meta.Encrypted?"E":"-";
                flags+=meta.Hidden?"H":"-";
                flags+=meta.Readonly?"R":"-";
                System.Console.WriteLine($"Entry\t{offset}\t{entr.pointer}\t{meta.Type}\t{flags}");
                offset++;
            }
            var d = c.GetDataEntry((int)MaxBlocksPerBlock);
            if(new BlockMetadata(d.metadata).Type!=BlockMetadata.BlockType.Extension)
                break;
            c = new DirectoryBlock(sb,d.pointer);
        }
    }
    public List<(BlockMetadata,FileBlock)> GetFiles()
    {
        List<(BlockMetadata,FileBlock)> files = [];

        for (int i = 0; i < MaxBlocksPerBlock; i++)
        {
            var ent = GetDataEntry(i);
            if(ent.pointer==0)
                continue;
            var meta = new BlockMetadata(ent.metadata);
            if(meta.Type==BlockMetadata.BlockType.None)
                continue;
            if(meta.Type==BlockMetadata.BlockType.File)
            {
                var file = new FileBlock(ent.pointer,sb);
                files.Add((meta,file));
            }
        }
        var d = GetDataEntry((int)MaxBlocksPerBlock);
        if(new BlockMetadata(d.metadata).Type==BlockMetadata.BlockType.Extension)
            files.AddRange(new DirectoryBlock(sb,d.pointer).GetFiles());
        return files;

    }
    public void AddBlock<T>(T block,BlockMetadata metadata) where T : IBlock => AddBlock(block,metadata.ToLong());
    public void AddBlock<T>(T block,ulong metadata) where T : IBlock
    {
        DirectoryBlock c = (DirectoryBlock)this;
        while (true)
        {

            for (int i = 0; i < MaxBlocksPerBlock; i++)
            {
                if(new BlockMetadata(c.GetDataEntry(i).metadata).Type==BlockMetadata.BlockType.None)
                {
                    c.SetDataEntry(i,(metadata,block.PointerToBlock));
                    return;
                }
            }
            var d = c.GetDataEntry((int)MaxBlocksPerBlock);
            if(new BlockMetadata(d.metadata).Type!=BlockMetadata.BlockType.Extension)
            {
                var blk = sb.FindFreeBlock();
                sb.SetOccupancy(blk,true);
                var mtd = new BlockMetadata(0)
                {
                    Type = BlockMetadata.BlockType.Extension
                };

                c.SetDataEntry((int)MaxBlocksPerBlock,(mtd.ToLong(),blk));
                c = new DirectoryBlock(sb,blk);
                c.Clear();
                continue;
            }
            c = new DirectoryBlock(sb,d.pointer);
        }

        throw new NotImplementedException();
    }
}
// Must fit into 64 bits
public struct BlockMetadata
{
    public enum BlockType : byte
    {
        None = 0,        
        File = 1,        
        Directory = 2,        
        Extension = 3,        
    }
    public bool Readonly
    {
        readonly get { return (meta & 0b_1000_0000) != 0; }
        set { if (value) meta |= 0b_1000_0000; else meta &= 0b_0111_1111; }
    }

    public bool Hidden
    {
        readonly get { return (meta & 0b_0100_0000) != 0; }
        set { if (value) meta |= 0b_0100_0000; else meta &= 0b_1011_1111; }
    }

    public bool Archived
    {
        readonly get { return (meta & 0b_0010_0000) != 0; }
        set { if (value) meta |= 0b_0010_0000; else meta &= 0b_1101_1111; }
    }

    public bool Encrypted
    {
        readonly get { return (meta & 0b_0001_0000) != 0; }
        set { if (value) meta |= 0b_0001_0000; else meta &= 0b_1110_1111; }
    }
    public BlockType Type
    {
        readonly get { return (BlockType)(meta & 0b_0000_0011); }
        set { meta = (byte)((meta & 0b_1111_1100) | ((byte)value & 0b_0000_0011)); }
    }
    private byte meta;
    public byte Permissions; // 8 bits
    public short Reserved1; // 16 bits
    public int Reserved2; // 32 bits
    public ulong ToLong()
    {
        byte[] buffer = new byte[sizeof(long)];
        buffer[0] = meta;
        buffer[1] = Permissions;
        BitConverter.GetBytes(Reserved1).CopyTo(buffer,2);
        BitConverter.GetBytes(Reserved2).CopyTo(buffer,2+sizeof(short));
        return BitConverter.ToUInt64(buffer);
    }
    public BlockMetadata(ulong data)
    {
        byte[] bytes = BitConverter.GetBytes(data);
        if (bytes.Length > 8)
        {
            throw new ArgumentException("Data exceeds 64 bits.");
        }

        // Convert bytes to struct fields
        meta = bytes[0];
        Permissions = bytes[1];
        Reserved1 = BitConverter.ToInt16(bytes, 2);
        Reserved2 = BitConverter.ToInt32(bytes, 2 + sizeof(short));
    }
}

class FileSystem(string path)
{
    public readonly Dictionary<string,FileBlock> files = [];
    private FileStream fs;
    public SuperBlock superBlock;
    public DirectoryBlock blockOfDBlocks;
    public void Format()
    {
        fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        superBlock = new SuperBlock(fs);
        System.Console.WriteLine($"Blocks:{superBlock.Blocks}, occ:{superBlock.MemoryBlocks}");
        var occ = (superBlock.MemoryBlocks/SuperBlock.BlockSize);

        for (long i = 0; i < superBlock.Blocks; i++)
        {  
            superBlock.SetOccupancy(i,i<=occ);
            if((i+1)%SuperBlock.BlockSize==0)
                System.Console.WriteLine($"{(((100*i)/(decimal)superBlock.Blocks)):0.00}");
        }
        blockOfDBlocks = new DirectoryBlock(superBlock, occ);
        blockOfDBlocks.Clear();

        System.Console.WriteLine($"Writes Total:{superBlock.WritesDelta}");
    }
    public void Load()
    {
        fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        superBlock = new SuperBlock(fs);
        Console.WriteLine($"Blocks:{superBlock.Blocks}, occ:{superBlock.MemoryBlocks}");
        blockOfDBlocks = new DirectoryBlock(superBlock, (superBlock.MemoryBlocks/SuperBlock.BlockSize));

        Console.WriteLine($"Writes Total:{superBlock.WritesDelta}");
        blockOfDBlocks.Display();
    }
    public FileBlock AddFile(string path)
    {
        if(!File.Exists(path))
            throw new FileNotFoundException(path);
        

        var data = File.ReadAllBytes(path);
        var file = FileBlock.CreateNow(Path.GetFileName(path),superBlock);
        var metadata = new BlockMetadata();
        metadata.Type = BlockMetadata.BlockType.File;
        blockOfDBlocks.AddBlock(file,metadata);
        using MemoryStream ms = new(data);
        file.UpdateData(superBlock,ms);
        return file;
    }
}
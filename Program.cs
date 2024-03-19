var fs = new FileSystem("drive");
fs.Format();

var fls = fs.blockOfDBlocks.GetFiles();
//File.WriteAllLines("files.txt",fls.Select(x=>$"{x.Item2.Name}\t{x.Item2.PointerToBlock}\t{((x.Item2.Length/1024d)/1024d):0.00} MiB\t{(x.Item1.Archived?"A":"-")}{(x.Item1.Encrypted?"E":"-")}{(x.Item1.Hidden?"H":"-")}{(x.Item1.Readonly?"R":"-")}"));
fs.blockOfDBlocks.Display();
//file.ReadData(fs.superBlock,st);
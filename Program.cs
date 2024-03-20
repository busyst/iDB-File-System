using JD.Security.Dummy;

var fs = new FileSystem("drive");
fs.Format();
DummyEncryptionHandler dummyEncryptionHandler = new DummyEncryptionHandler("test");
foreach (var x in FileHelper.GetItemsRecursively(".\\ign"))
{
    System.Console.WriteLine(x);
    using var e = new DummyEncryptionHandler.DummyEncryptor(dummyEncryptionHandler);
    fs.AddFile(x,fs.mainBlock,e);
}
var files = fs.mainBlock.GetFiles(false);
foreach (var x in files)
{
    using var fd = File.OpenWrite(".\\Dump\\Dec\\"+x.Item2.Name);
    using var d = new DummyEncryptionHandler.DummyDecryptor(dummyEncryptionHandler);
    x.Item2.ReadData(fd,d);
}
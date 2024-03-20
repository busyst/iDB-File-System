File system written in C# that suports encryption, partition is divided in blocks(512,1024 and so on by power of 2).
Every file have matadata, that stored along with pointer in directory.

If you want to use it efficiently rewrite DirectoryBlock class so you not creating new directories with empty name as extention for directory.

static class FileHelper
{
    public static void CreateFileWithLength(ulong sizeInBytes, string filename)
    {
        using FileStream file = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write);
        if (file == null)
        {
            Console.WriteLine("Error opening file " + filename);
            return;
        }

        file.Seek((long)sizeInBytes - 1, SeekOrigin.Begin);
        file.WriteByte((byte)'A');

        Console.WriteLine("File " + filename + " created with length " + sizeInBytes + " bytes");
    }
    public static List<string> GetItemsRecursively(string directoryPath)
    {
        List<string> allItems = [];
        try
        {
            // Get all files in the current directory
            string[] files = Directory.GetFiles(directoryPath);
            allItems.AddRange(files);

            // Get all directories in the current directory
            string[] subDirectories = Directory.GetDirectories(directoryPath);
            foreach (string subDirectory in subDirectories)
            {
                // Recursively call the method to get items in the subdirectory
                List<string> subItems = GetItemsRecursively(subDirectory);
                allItems.AddRange(subItems);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }

        return allItems;
    }
}
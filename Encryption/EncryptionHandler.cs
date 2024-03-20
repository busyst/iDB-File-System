using System.Security.Cryptography;
using System.Text;
namespace JD.Security.Dummy;
public class DummyEncryptionHandler
{
    static int StringToUniqueInt(string input) => BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(input)), 0);
    public readonly string Key;
    public DummyEncryptionHandler(string key)
    {
        Key = key;
        // Fill the array with numbers 0 to 255
        for (int i = 0; i < 256; i++)
            substitutionTable[i] = (byte)i;
        Random random = new(StringToUniqueInt(Key));
        for (int i = offsetTable.Length - 1; i >= 0; i--)
            offsetTable[i] = (byte)random.Next(0,255);
        // Shuffle the array
        for (int i = 255; i > 0; i--)
        {
            int randIndex = random.Next(i + 1);
            (substitutionTable[i], substitutionTable[randIndex]) = (substitutionTable[randIndex], substitutionTable[i]);
        }
        internalOffset = (uint)random.Next(0,256);
    }
    private readonly byte[] substitutionTable = new byte[256];
    private readonly byte[] offsetTable = new byte[64];
    private readonly uint internalOffset; 
    public class DummyEncryptor(DummyEncryptionHandler eh) : Encryptor
    {
        public uint internalOffset = eh.internalOffset;
        private byte[] buffer = [];
        public override Memory<byte> Encrypt(ReadOnlySpan<byte> input)
        {
            var length = input.Length;
            if (length > buffer.Length)
                buffer = new byte[length];
            
            for (int i = 0; i < length; i++)
            {
                var val = input[i] - eh.offsetTable[(i + eh.offsetTable[0] + internalOffset) % eh.offsetTable.Length];
                
                if ((internalOffset + i) % 3 != 0)
                    internalOffset++;

                var c = eh.substitutionTable[(((val & 0xF0) >> 4) * 16) + (val & 0x0F)];
                buffer[i] = c;
            }

            return buffer.AsMemory(0,length);
        }

        public override void Dispose()
        {
            buffer = [];
            GC.SuppressFinalize(this);
        }
    }
    public class DummyDecryptor(DummyEncryptionHandler eh) : Decryptor
    {
        public uint internalOffset = eh.internalOffset;
        private byte[] buffer = [];
        public override Memory<byte> Decrypt(ReadOnlySpan<byte> input)
        {
            var length = input.Length;
            if (length > buffer.Length)
                buffer = new byte[length];

            for (int i = 0; i < length; i++)
            {
                var index = Array.IndexOf(eh.substitutionTable, input[i]); // Find the index of the input byte in the table

                if (index == -1) // If byte is not found in the table
                    throw new Exception("Impossible");

                var indexX = index % 16;
                var indexY = index / 16;
                var val = (byte)(((indexY << 4) | indexX) + eh.offsetTable[(i + eh.offsetTable[0] + internalOffset) % eh.offsetTable.Length]);

                if ((internalOffset + i) % 3 != 0)
                    internalOffset++;

                buffer[i] = val;
            }

            return buffer.AsMemory(0,length);
        }

        public override void Dispose()
        {
            buffer = [];
            GC.SuppressFinalize(this);
        }
    }
}
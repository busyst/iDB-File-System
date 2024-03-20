namespace JD.Security;
public abstract class Encryptor : IDisposable
{
    public abstract void Dispose();
    public abstract Memory<byte> Encrypt(ReadOnlySpan<byte> input);
}
public abstract class Decryptor : IDisposable
{
    public abstract void Dispose();
    public abstract Memory<byte> Decrypt(ReadOnlySpan<byte> input);
}
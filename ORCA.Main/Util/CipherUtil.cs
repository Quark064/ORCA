using System.Security.Cryptography;
using System.Text;

namespace ORCA.Main.Util;

public static class CipherUtil
{
    public static string Encrypt(string plainText, byte[] key)
    {
        using Aes aes = Aes.Create();

        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.GenerateIV();

        using var ms = new MemoryStream();
        ms.Write(aes.IV);

        using (var encryptor = aes.CreateEncryptor())
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            byte[] plaintext = Encoding.UTF8.GetBytes(plainText);
            cs.Write(plaintext);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public static string Decrypt(string cipherTextBase64, byte[] key)
    {
        byte[] data = Convert.FromBase64String(cipherTextBase64);

        using Aes aes = Aes.Create();

        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = data.AsSpan(0, 16).ToArray();

        using var decryptor = aes.CreateDecryptor();

        using var ms = new MemoryStream(data, 16, data.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);

        return sr.ReadToEnd();
    }
}

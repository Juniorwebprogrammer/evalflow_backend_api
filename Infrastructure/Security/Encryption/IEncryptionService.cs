namespace evalflow_backend_api.Infrastructure.Security.Encryption;

public interface IEncryptionService
{
    string Encrypt(string plainText);

    string Decrypt(string cipherText);
}
from ff3 import FF3Cipher
import secrets

ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_=:;."
CHUNK_MIN = 4
CHUNK_MAX = 30
PAD_CHAR = ";"

hex_string = secrets.token_hex(24).upper()

key = "2DE79D232DF5585D68CE47882AE256D6"
tweak = "CBD09280979564FF"

plaintext = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJmZTg5MTY3M2E1MzdmOTU4IiwidHlwIjoic2Vzc2lvbl90b2tlbiIsImF1ZCI6IjcxYjk2M2MxYjdiNmQxMTkiLCJpYXQiOjE3NzM0Njc4MzQsImV4cCI6MTgzNjUzOTgzNCwic3Q6c2NwIjpbMCw4LDksMTcsMjNdLCJpc3MiOiJodHRwczovL2FjY291bnRzLm5pbnRlbmRvLmNvbSIsImp0aSI6MTk0OTU3NzU5OTF9.AfU_hWHNCZWBoeisFIYeuy_k-gRxGkXc2kVb3vnGD6s:eyJhbGciOiJSUzI1NiIsImprdSI6Imh0dHBzOi8vYXBpLWxwMS56bmMuc3J2Lm5pbnRlbmRvLm5ldC92MS9XZWJTZXJ2aWNlL0NlcnRpZmljYXRlL0xpc3QiLCJraWQiOiJKNy1qblVXZDkxVmRxZzMxRWlmcVcxS1BJcGsiLCJ0eXAiOiJKV1QifQ.eyJpc0NoaWxkUmVzdHJpY3RlZCI6ZmFsc2UsImF1ZCI6IjY2MzM2NzcyOTE1NTI3NjgiLCJleHAiOjE3NzM0Nzg3NDcsImlhdCI6MTc3MzQ2Nzk0NywiaXNzIjoiYXBpLWxwMS56bmMuc3J2Lm5pbnRlbmRvLm5ldCIsImp0aSI6IjE4NTBmM2QzLTdlNzYtNGI0MS1hNzliLWEwZGE5MDkyMDBmMCIsInN1YiI6NTEwMzQyNzM5MzY4MzQ1NiwibGlua3MiOnsibmV0d29ya1NlcnZpY2VBY2NvdW50Ijp7ImlkIjoiZGZjNDVjYWNlMjQ3OGVjYyJ9fSwidHlwIjoiaWRfdG9rZW4iLCJtZW1iZXJzaGlwIjp7ImFjdGl2ZSI6dHJ1ZSwiZXhwaXJlc19hdCI6MTc4NTQ2Njg1N319.7AbNwlM8qJGL4U2ByHrKFoD6pcayfo_fB4VSgMrLZ98QHpl6UeumcwtcsJOcvdxHp6GWUZ_v6QRVV1_mCVe951-AbuM35LnTv9TgSwVMoD_aRvayL3hzy9nBVZfUaEL6h5djZA2Sfwaq2xhgyqX-Kagxo--T_QW6uhDNRb2FW_SO7Qy3NB_203knmRE_4iIRqv5FHLS2TZR0c9qALfRB46UA5u8amF9BvNOrn3AmzXUH0X7LJ5KO4B2GaHkSvjbxXDpiiPtU3Jju-gOU98lCP6Jm1a3CGYPMb2V_Tnv0navsVfw5ybIILYb4dPTnuyCq7aojvH6qE0q85H4BWX7L-Q:xdrNETUjQtNnf7spsGoiNdV2tXWJZwfot8kyt44WZrN_SifNAcMiwYMEjdXaKEjGACIBDnCSv77xxuNgbgjpl3c6RqgGOnnNSnsuP9X234yk9G53-aKYvXS7x68="

cipher = FF3Cipher.withCustomAlphabet(key, tweak, ALPHABET)


def encryptString(text: str, cipher: FF3Cipher):
    out = []
    for i in range(0, len(text), CHUNK_MAX):
        chunk = text[i:i+CHUNK_MAX]
        out.append(cipher.encrypt(chunk))
    
    if len(out[-1]) < 4:
        out[-1] += PAD_CHAR * (4 - len(out[-1]))

    return "".join(out)

def decryptString(cipherText: str, cipher: FF3Cipher):
    out = []
    for i in range(0, len(cipherText), CHUNK_MAX):
        chunk = cipherText[i:i+CHUNK_MAX]
        out.append(cipher.decrypt(chunk))
    
    out[-1].strip(PAD_CHAR)

    return "".join(out)

cipherText = encryptString(plaintext, cipher)
og = decryptString(cipherText, cipher)

print()
Imports System.Security.Cryptography

Namespace Security

    ''' <summary>
    ''' PBKDF2-HMACSHA256 password hashing. Stored format is
    ''' "PBKDF2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;" so the
    ''' iteration count can be raised later without invalidating old hashes.
    ''' </summary>
    Public Module PasswordHasher

        Private Const Iterations As Integer = 100000
        Private Const SaltSize As Integer = 16
        Private Const HashSize As Integer = 32

        Public Function HashPassword(password As String) As String
            Dim salt(SaltSize - 1) As Byte
            Using rng = RandomNumberGenerator.Create()
                rng.GetBytes(salt)
            End Using
            Dim hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize)
            Return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}"
        End Function

        Public Function VerifyPassword(password As String, storedHash As String) As Boolean
            If String.IsNullOrEmpty(storedHash) Then Return False
            Dim parts = storedHash.Split("$"c)
            If parts.Length <> 4 OrElse parts(0) <> "PBKDF2" Then Return False

            Dim iterations As Integer
            If Not Integer.TryParse(parts(1), iterations) Then Return False

            Dim salt, expected As Byte()
            Try
                salt = Convert.FromBase64String(parts(2))
                expected = Convert.FromBase64String(parts(3))
            Catch
                Return False
            End Try

            Dim actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length)
            Return CryptographicOperations.FixedTimeEquals(actual, expected)
        End Function

    End Module

End Namespace

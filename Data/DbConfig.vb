Imports System.IO

Namespace Data

    ''' <summary>
    ''' Resolves the MySQL connection string. Looks for a "dbconfig.ini" file
    ''' next to the executable so the RHU can point the app at their XAMPP
    ''' MySQL instance without recompiling; falls back to XAMPP's defaults
    ''' (localhost, root, no password, database rhu_care).
    ''' </summary>
    Public Module DbConfig

        Private _connectionString As String = Nothing

        Public Function GetConnectionString() As String
            If _connectionString IsNot Nothing Then Return _connectionString

            Dim server As String = "localhost"
            Dim port As String = "3306"
            Dim database As String = "rhu_care"
            Dim uid As String = "root"
            Dim pwd As String = ""

            Try
                Dim configPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbconfig.ini")
                If File.Exists(configPath) Then
                    For Each line In File.ReadAllLines(configPath)
                        Dim trimmed = line.Trim()
                        If trimmed.Length = 0 OrElse trimmed.StartsWith("#") OrElse trimmed.StartsWith(";") Then Continue For
                        Dim parts = trimmed.Split(New Char() {"="c}, 2)
                        If parts.Length <> 2 Then Continue For
                        Dim key = parts(0).Trim().ToLowerInvariant()
                        Dim value = parts(1).Trim()
                        Select Case key
                            Case "server" : server = value
                            Case "port" : port = value
                            Case "database" : database = value
                            Case "uid", "user" : uid = value
                            Case "pwd", "password" : pwd = value
                        End Select
                    Next
                End If
            Catch
                ' fall through to defaults below
            End Try

            _connectionString = $"Server={server};Port={port};Database={database};Uid={uid};Pwd={pwd};AllowPublicKeyRetrieval=True;SslMode=Preferred;"
            Return _connectionString
        End Function

        ''' <summary>Writes a starter dbconfig.ini next to the exe if one doesn't already exist.</summary>
        Public Sub EnsureConfigFileExists()
            Try
                Dim configPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbconfig.ini")
                If Not File.Exists(configPath) Then
                    File.WriteAllLines(configPath, New String() {
                        "# Project C.A.R.E - MySQL connection settings",
                        "# Defaults match a fresh XAMPP install.",
                        "server=localhost",
                        "port=3306",
                        "database=rhu_care",
                        "uid=root",
                        "pwd="
                    })
                End If
            Catch
                ' non-fatal; the app still works with in-memory defaults
            End Try
        End Sub

    End Module

End Namespace

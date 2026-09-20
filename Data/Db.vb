Imports MySqlConnector

Namespace Data

    ''' <summary>
    ''' Thin ADO.NET helper around MySqlConnector. Keeps every form's data
    ''' access down to a single line for the common cases (query -> DataTable,
    ''' or a parameterized insert/update/delete) instead of repeating
    ''' connection/command boilerplate in every form.
    ''' </summary>
    Public Module Db

        Public Function OpenConnection() As MySqlConnection
            Dim conn As New MySqlConnection(DbConfig.GetConnectionString())
            conn.Open()
            Return conn
        End Function

        Friend Sub AddParams(cmd As MySqlCommand, params As Object)
            If params Is Nothing Then Return
            For Each prop In params.GetType().GetProperties()
                Dim value = prop.GetValue(params)
                cmd.Parameters.AddWithValue("@" & prop.Name, If(value, DBNull.Value))
            Next
        End Sub

        ''' <summary>Runs a SELECT and returns the results as a DataTable.</summary>
        Public Function Query(sql As String, Optional params As Object = Nothing) As DataTable
            Using conn = OpenConnection()
                Using cmd As New MySqlCommand(sql, conn)
                    AddParams(cmd, params)
                    Using reader = cmd.ExecuteReader()
                        Dim table As New DataTable()
                        table.Load(reader)
                        Return table
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>Runs a SELECT and returns the first column of the first row, or Nothing.</summary>
        Public Function QueryScalar(sql As String, Optional params As Object = Nothing) As Object
            Using conn = OpenConnection()
                Using cmd As New MySqlCommand(sql, conn)
                    AddParams(cmd, params)
                    Dim result = cmd.ExecuteScalar()
                    If result Is Nothing OrElse result Is DBNull.Value Then Return Nothing
                    Return result
                End Using
            End Using
        End Function

        ''' <summary>Runs an INSERT/UPDATE/DELETE and returns rows affected.</summary>
        Public Function Execute(sql As String, Optional params As Object = Nothing) As Integer
            Using conn = OpenConnection()
                Using cmd As New MySqlCommand(sql, conn)
                    AddParams(cmd, params)
                    Return cmd.ExecuteNonQuery()
                End Using
            End Using
        End Function

        ''' <summary>Runs an INSERT and returns the new row's AUTO_INCREMENT id.</summary>
        Public Function ExecuteInsertReturnId(sql As String, Optional params As Object = Nothing) As Long
            Using conn = OpenConnection()
                Using cmd As New MySqlCommand(sql, conn)
                    AddParams(cmd, params)
                    cmd.ExecuteNonQuery()
                    Return cmd.LastInsertedId
                End Using
            End Using
        End Function

        ''' <summary>Quick connectivity check used by the login screen to give a clear error if MySQL/XAMPP isn't running.</summary>
        Public Function CanConnect(ByRef errorMessage As String) As Boolean
            Try
                Using conn = OpenConnection()
                End Using
                errorMessage = Nothing
                Return True
            Catch ex As Exception
                errorMessage = ex.Message
                Return False
            End Try
        End Function

    End Module

    ''' <summary>
    ''' Groups several Execute/ExecuteInsertReturnId calls into one real
    ''' MySQL transaction, for the handful of flows that touch more than one
    ''' table and must not be left half-done by a crash or lost connection
    ''' mid-sequence (e.g. dispensing a medication: log the dispensing row,
    ''' decrement the stock batch, and flip the prescription's status --
    ''' all three or none). Callers MUST call Commit() explicitly; Dispose
    ''' (so also a Using block exiting without Commit) rolls back.
    ''' </summary>
    Public Class DbTransaction
        Implements IDisposable

        Private ReadOnly connection As MySqlConnection
        Private ReadOnly transaction As MySqlTransaction
        Private committed As Boolean = False

        Public Sub New()
            connection = Db.OpenConnection()
            transaction = connection.BeginTransaction()
        End Sub

        Public Function Execute(sql As String, Optional params As Object = Nothing) As Integer
            Using cmd As New MySqlCommand(sql, connection, transaction)
                Db.AddParams(cmd, params)
                Return cmd.ExecuteNonQuery()
            End Using
        End Function

        Public Function ExecuteInsertReturnId(sql As String, Optional params As Object = Nothing) As Long
            Using cmd As New MySqlCommand(sql, connection, transaction)
                Db.AddParams(cmd, params)
                cmd.ExecuteNonQuery()
                Return cmd.LastInsertedId
            End Using
        End Function

        Public Function QueryScalar(sql As String, Optional params As Object = Nothing) As Object
            Using cmd As New MySqlCommand(sql, connection, transaction)
                Db.AddParams(cmd, params)
                Dim result = cmd.ExecuteScalar()
                If result Is Nothing OrElse result Is DBNull.Value Then Return Nothing
                Return result
            End Using
        End Function

        Public Sub Commit()
            transaction.Commit()
            committed = True
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                If Not committed Then transaction.Rollback()
            Catch
                ' connection may already be closed/broken -- nothing more we can do
            Finally
                transaction.Dispose()
                connection.Dispose()
            End Try
        End Sub

    End Class

End Namespace

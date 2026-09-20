Namespace Models

    ''' <summary>Represents the currently logged-in staff member for the lifetime of the app session.</summary>
    Public Class CurrentUser
        Public Property UserId As Integer
        Public Property FullName As String
        Public Property Username As String
        Public Property RoleId As Integer
        Public Property RoleName As String
        Public Property ModuleAccess As String
        Public Property CanAdd As Boolean
        Public Property CanEdit As Boolean
        Public Property CanDelete As Boolean
        Public Property CanView As Boolean

        Public Function HasModule(moduleKey As String) As Boolean
            If String.IsNullOrEmpty(ModuleAccess) Then Return False
            Dim modules = ModuleAccess.Split(","c)
            For Each m In modules
                If m.Trim().Equals(moduleKey, StringComparison.OrdinalIgnoreCase) Then Return True
            Next
            Return False
        End Function
    End Class

    ''' <summary>Process-wide holder for the active session. Set once at login, read everywhere.</summary>
    Public Module Session
        Public Property Current As CurrentUser = Nothing

        Public ReadOnly Property IsLoggedIn As Boolean
            Get
                Return Current IsNot Nothing
            End Get
        End Property
    End Module

End Namespace

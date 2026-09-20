Imports TechTonic_IM_RHU.Models

Namespace Data

    ''' <summary>Writes a row to audit_logs (backs Report #9 / Figure 36).</summary>
    Public Module AuditLogger

        Public Sub Log(action As String, moduleKey As String, targetRecord As String, description As String, Optional status As String = "Success")
            Try
                Dim userId As Object = If(Session.IsLoggedIn, CType(Session.Current.UserId, Object), DBNull.Value)
                Dim username As Object = If(Session.IsLoggedIn, CType(Session.Current.Username, Object), DBNull.Value)
                Dim role As Object = If(Session.IsLoggedIn, CType(Session.Current.RoleName, Object), DBNull.Value)

                Db.Execute("INSERT INTO audit_logs (user_id, username_snapshot, role_snapshot, action, module, target_record, description, status)
                            VALUES (@UserId, @Username, @Role, @Action, @ModuleKey, @TargetRecord, @Description, @Status)",
                    New With {
                        .UserId = userId,
                        .Username = username,
                        .Role = role,
                        .Action = action,
                        .ModuleKey = moduleKey,
                        .TargetRecord = targetRecord,
                        .Description = description,
                        .Status = status
                    })
            Catch
                ' auditing must never crash the workflow it's observing
            End Try
        End Sub

    End Module

End Namespace

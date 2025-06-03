using InventarioAPI.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace InventarioAPI.Services
{
    public interface IUserService
    {
        Task<ApiResponse<List<UserResponse>>> GetUsersAsync(GetUsersRequest request);
        Task<ApiResponse<UserResponse>> CreateUserAsync(CreateUserRequest request, int createdBy);
        Task<ApiResponse<UserResponse>> UpdateUserAsync(int userId, UpdateUserRequest request, int updatedBy);
        Task<ApiResponse<bool>> DeleteUserAsync(int userId, int deletedBy);
        Task<ApiResponse<List<UserActivity>>> GetUserActivityAsync(int userId);
        Task<ApiResponse<List<UserResponse>>> GetUsersByRoleAsync(UserProfile? perfil, string tienda = "");
        Task<ApiResponse<PermissionCheckResponse>> CheckPermissionsAsync(int userId, CheckPermissionsRequest request);
        Task<ApiResponse<SystemStatsResponse>> GetSystemStatsAsync();
        Task<ApiResponse<List<UserResponse>>> GetUsersByTiendaAsync(string tienda);
    }

    public class UserService : IUserService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public UserService(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("InventarioConnection") 
                ?? throw new InvalidOperationException("Connection string not found");
        }

        public async Task<ApiResponse<List<UserResponse>>> GetUsersAsync(GetUsersRequest request)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var whereConditions = new List<string> { "IsActive = 1" };
                var parameters = new List<SqlParameter>();

                // Aplicar filtros
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    whereConditions.Add("(USUARIO LIKE @SearchTerm OR NOMBRE LIKE @SearchTerm OR EMAIL LIKE @SearchTerm)");
                    parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 200) { Value = $"%{request.SearchTerm}%" });
                }

                if (request.Perfil.HasValue)
                {
                    whereConditions.Add("PERFIL = @Perfil");
                    parameters.Add(new SqlParameter("@Perfil", SqlDbType.NVarChar, 50) { Value = request.Perfil.Value.ToString() });
                }

                if (!string.IsNullOrEmpty(request.Tienda))
                {
                    whereConditions.Add("TIENDA = @Tienda");
                    parameters.Add(new SqlParameter("@Tienda", SqlDbType.NVarChar, 50) { Value = request.Tienda });
                }

                if (request.Activo.HasValue)
                {
                    whereConditions.Add("IsActive = @Activo");
                    parameters.Add(new SqlParameter("@Activo", SqlDbType.Bit) { Value = request.Activo.Value });
                }

                var whereClause = string.Join(" AND ", whereConditions);

                var query = $@"
                    SELECT u.*, 
                           STRING_AGG(ud.DivisionCode, ',') as DivisionesAsignadas
                    FROM Usuarios u
                    LEFT JOIN UserDivisions ud ON u.ID = ud.UserID AND ud.IsActive = 1
                    WHERE {whereClause}
                    GROUP BY u.ID, u.USUARIO, u.NOMBRE, u.EMAIL, u.PERFIL, u.TIENDA, u.AREA, 
                             u.IsActive, u.UltimoAcceso, u.FechaCreacion, u.FechaActualizacion
                    ORDER BY u.FechaCreacion DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddRange(parameters.ToArray());
                command.Parameters.Add("@Offset", SqlDbType.Int).Value = (request.PageNumber - 1) * request.PageSize;
                command.Parameters.Add("@PageSize", SqlDbType.Int).Value = request.PageSize;

                var users = new List<UserResponse>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var user = new UserResponse
                    {
                        Id = Convert.ToInt32(reader["ID"]),
                        Usuario = reader["USUARIO"]?.ToString() ?? "",
                        Nombre = reader["NOMBRE"]?.ToString() ?? "",
                        Email = reader["EMAIL"]?.ToString() ?? "",
                        Perfil = Enum.Parse<UserProfile>(reader["PERFIL"]?.ToString() ?? "INVENTARIO"),
                        Tienda = reader["TIENDA"]?.ToString() ?? "",
                        Area = reader["AREA"]?.ToString() ?? "",
                        Activo = Convert.ToBoolean(reader["IsActive"]),
                        UltimoAcceso = reader.IsDBNull("UltimoAcceso") ? null : Convert.ToDateTime(reader["UltimoAcceso"]),
                        FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                        FechaActualizacion = Convert.ToDateTime(reader["FechaActualizacion"])
                    };

                    // Procesar divisiones asignadas
                    var divisionesStr = reader["DivisionesAsignadas"]?.ToString();
                    if (!string.IsNullOrEmpty(divisionesStr))
                    {
                        user.DivisionesAsignadas = divisionesStr.Split(',').ToList();
                    }

                    users.Add(user);
                }

                return new ApiResponse<List<UserResponse>>
                {
                    Success = true,
                    Message = "Usuarios obtenidos exitosamente",
                    Data = users
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<UserResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener usuarios: {ex.Message}",
                    Data = new List<UserResponse>()
                };
            }
        }

        public async Task<ApiResponse<UserResponse>> CreateUserAsync(CreateUserRequest request, int createdBy)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Verificar si el usuario ya existe
                    const string checkUserQuery = "SELECT COUNT(1) FROM Usuarios WHERE USUARIO = @Usuario";
                    using var checkCommand = new SqlCommand(checkUserQuery, connection, transaction);
                    checkCommand.Parameters.Add("@Usuario", SqlDbType.NVarChar, 50).Value = request.Usuario;

                    var userExists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                    if (userExists)
                    {
                        return new ApiResponse<UserResponse>
                        {
                            Success = false,
                            Message = "El usuario ya existe",
                            Errors = new List<string> { "El nombre de usuario ya está en uso" }
                        };
                    }

                    // Verificar si el email ya existe
                    const string checkEmailQuery = "SELECT COUNT(1) FROM Usuarios WHERE EMAIL = @Email";
                    using var checkEmailCommand = new SqlCommand(checkEmailQuery, connection, transaction);
                    checkEmailCommand.Parameters.Add("@Email", SqlDbType.NVarChar, 200).Value = request.Email;

                    var emailExists = Convert.ToInt32(await checkEmailCommand.ExecuteScalarAsync()) > 0;
                    if (emailExists)
                    {
                        return new ApiResponse<UserResponse>
                        {
                            Success = false,
                            Message = "El email ya existe",
                            Errors = new List<string> { "El email ya está registrado" }
                        };
                    }

                    // Hashear contraseña
                    var hashedPassword = HashPassword(request.Password);

                    // Crear usuario
                    const string insertQuery = @"
                        INSERT INTO Usuarios (USUARIO, NOMBRE, EMAIL, PASSWORD, PERFIL, TIENDA, AREA, 
                                            IsActive, FechaCreacion, FechaActualizacion, CreatedBy)
                        VALUES (@Usuario, @Nombre, @Email, @Password, @Perfil, @Tienda, @Area, 
                                1, GETDATE(), GETDATE(), @CreatedBy);
                        SELECT SCOPE_IDENTITY();";

                    using var insertCommand = new SqlCommand(insertQuery, connection, transaction);
                    insertCommand.Parameters.Add("@Usuario", SqlDbType.NVarChar, 50).Value = request.Usuario;
                    insertCommand.Parameters.Add("@Nombre", SqlDbType.NVarChar, 200).Value = request.Nombre;
                    insertCommand.Parameters.Add("@Email", SqlDbType.NVarChar, 200).Value = request.Email;
                    insertCommand.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value = hashedPassword;
                    insertCommand.Parameters.Add("@Perfil", SqlDbType.NVarChar, 50).Value = request.Perfil.ToString();
                    insertCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = request.Tienda;
                    insertCommand.Parameters.Add("@Area", SqlDbType.NVarChar, 100).Value = request.Area;
                    insertCommand.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = createdBy;

                    var userId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());

                    // Asignar divisiones si es LIDER
                    if (request.Perfil == UserProfile.LIDER && request.DivisionesAsignadas.Any())
                    {
                        await AssignDivisionsToUserAsync(userId, request.Tienda, request.DivisionesAsignadas, connection, transaction);
                    }

                    // Registrar actividad
                    await LogUserActivityAsync(userId, createdBy, "user_created", $"Usuario {request.Usuario} creado", 
                                             "user", userId, null, connection, transaction);

                    transaction.Commit();

                    // Obtener el usuario creado
                    var createdUser = await GetUserByIdAsync(userId);
                    return new ApiResponse<UserResponse>
                    {
                        Success = true,
                        Message = "Usuario creado exitosamente",
                        Data = createdUser.Data
                    };
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = $"Error al crear usuario: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<UserResponse>> UpdateUserAsync(int userId, UpdateUserRequest request, int updatedBy)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Obtener usuario actual
                    var currentUser = await GetUserByIdAsync(userId, connection, transaction);
                    if (currentUser.Data == null)
                    {
                        return new ApiResponse<UserResponse>
                        {
                            Success = false,
                            Message = "Usuario no encontrado"
                        };
                    }

                    // Construir query de actualización dinámicamente
                    var updateFields = new List<string>();
                    var parameters = new List<SqlParameter>();

                    if (!string.IsNullOrEmpty(request.Nombre))
                    {
                        updateFields.Add("NOMBRE = @Nombre");
                        parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = request.Nombre });
                    }

                    if (!string.IsNullOrEmpty(request.Email))
                    {
                        // Verificar que el email no esté en uso por otro usuario
                        const string checkEmailQuery = "SELECT COUNT(1) FROM Usuarios WHERE EMAIL = @Email AND ID != @UserId";
                        using var checkEmailCommand = new SqlCommand(checkEmailQuery, connection, transaction);
                        checkEmailCommand.Parameters.Add("@Email", SqlDbType.NVarChar, 200).Value = request.Email;
                        checkEmailCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                        var emailExists = Convert.ToInt32(await checkEmailCommand.ExecuteScalarAsync()) > 0;
                        if (emailExists)
                        {
                            return new ApiResponse<UserResponse>
                            {
                                Success = false,
                                Message = "El email ya está en uso por otro usuario"
                            };
                        }

                        updateFields.Add("EMAIL = @Email");
                        parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 200) { Value = request.Email });
                    }

                    if (!string.IsNullOrEmpty(request.Password))
                    {
                        var hashedPassword = HashPassword(request.Password);
                        updateFields.Add("PASSWORD = @Password");
                        parameters.Add(new SqlParameter("@Password", SqlDbType.NVarChar, 255) { Value = hashedPassword });
                    }

                    if (request.Perfil.HasValue)
                    {
                        updateFields.Add("PERFIL = @Perfil");
                        parameters.Add(new SqlParameter("@Perfil", SqlDbType.NVarChar, 50) { Value = request.Perfil.Value.ToString() });
                    }

                    if (!string.IsNullOrEmpty(request.Tienda))
                    {
                        updateFields.Add("TIENDA = @Tienda");
                        parameters.Add(new SqlParameter("@Tienda", SqlDbType.NVarChar, 50) { Value = request.Tienda });
                    }

                    if (!string.IsNullOrEmpty(request.Area))
                    {
                        updateFields.Add("AREA = @Area");
                        parameters.Add(new SqlParameter("@Area", SqlDbType.NVarChar, 100) { Value = request.Area });
                    }

                    if (request.Activo.HasValue)
                    {
                        updateFields.Add("IsActive = @Activo");
                        parameters.Add(new SqlParameter("@Activo", SqlDbType.Bit) { Value = request.Activo.Value });
                    }

                    if (updateFields.Any())
                    {
                        updateFields.Add("FechaActualizacion = GETDATE()");
                        updateFields.Add("UpdatedBy = @UpdatedBy");
                        parameters.Add(new SqlParameter("@UpdatedBy", SqlDbType.Int) { Value = updatedBy });
                        parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });

                        var updateQuery = $@"
                            UPDATE Usuarios 
                            SET {string.Join(", ", updateFields)}
                            WHERE ID = @UserId";

                        using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                        updateCommand.Parameters.AddRange(parameters.ToArray());
                        await updateCommand.ExecuteNonQueryAsync();
                    }

                    // Actualizar divisiones si es necesario
                    if (request.DivisionesAsignadas != null)
                    {
                        var finalProfile = request.Perfil ?? currentUser.Data.Perfil;
                        var finalTienda = request.Tienda ?? currentUser.Data.Tienda;

                        if (finalProfile == UserProfile.LIDER)
                        {
                            // Desactivar divisiones actuales
                            const string deactivateDivisionsQuery = @"
                                UPDATE UserDivisions 
                                SET IsActive = 0 
                                WHERE UserID = @UserId AND Tienda = @Tienda";

                            using var deactivateCommand = new SqlCommand(deactivateDivisionsQuery, connection, transaction);
                            deactivateCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                            deactivateCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = finalTienda;
                            await deactivateCommand.ExecuteNonQueryAsync();

                            // Asignar nuevas divisiones
                            if (request.DivisionesAsignadas.Any())
                            {
                                await AssignDivisionsToUserAsync(userId, finalTienda, request.DivisionesAsignadas, connection, transaction);
                            }
                        }
                    }

                    // Registrar actividad
                    await LogUserActivityAsync(userId, updatedBy, "user_updated", 
                                             $"Usuario {currentUser.Data.Usuario} actualizado", 
                                             "user", userId, null, connection, transaction);

                    transaction.Commit();

                    // Obtener usuario actualizado
                    var updatedUser = await GetUserByIdAsync(userId);
                    return new ApiResponse<UserResponse>
                    {
                        Success = true,
                        Message = "Usuario actualizado exitosamente",
                        Data = updatedUser.Data
                    };
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = $"Error al actualizar usuario: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<bool>> DeleteUserAsync(int userId, int deletedBy)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Verificar que no sea el último administrador
                    const string checkAdminQuery = @"
                        SELECT COUNT(1) FROM Usuarios 
                        WHERE PERFIL = 'ADMINISTRADOR' AND IsActive = 1 AND ID != @UserId";

                    using var checkAdminCommand = new SqlCommand(checkAdminQuery, connection, transaction);
                    checkAdminCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                    var adminCount = Convert.ToInt32(await checkAdminCommand.ExecuteScalarAsync());

                    // Obtener información del usuario a eliminar
                    var userToDelete = await GetUserByIdAsync(userId, connection, transaction);
                    if (userToDelete.Data == null)
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "Usuario no encontrado"
                        };
                    }

                    if (userToDelete.Data.Perfil == UserProfile.ADMINISTRADOR && adminCount == 0)
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "No se puede eliminar el último administrador"
                        };
                    }

                    // Soft delete del usuario
                    const string deleteQuery = @"
                        UPDATE Usuarios 
                        SET IsActive = 0, FechaActualizacion = GETDATE(), UpdatedBy = @DeletedBy
                        WHERE ID = @UserId";

                    using var deleteCommand = new SqlCommand(deleteQuery, connection, transaction);
                    deleteCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    deleteCommand.Parameters.Add("@DeletedBy", SqlDbType.Int).Value = deletedBy;

                    await deleteCommand.ExecuteNonQueryAsync();

                    // Desactivar divisiones asignadas
                    const string deactivateDivisionsQuery = @"
                        UPDATE UserDivisions 
                        SET IsActive = 0 
                        WHERE UserID = @UserId";

                    using var deactivateCommand = new SqlCommand(deactivateDivisionsQuery, connection, transaction);
                    deactivateCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    await deactivateCommand.ExecuteNonQueryAsync();

                    // Registrar actividad
                    await LogUserActivityAsync(userId, deletedBy, "user_deleted", 
                                             $"Usuario {userToDelete.Data.Usuario} eliminado", 
                                             "user", userId, null, connection, transaction);

                    transaction.Commit();

                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Message = "Usuario eliminado exitosamente",
                        Data = true
                    };
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Error al eliminar usuario: {ex.Message}",
                    Data = false
                };
            }
        }

        public async Task<ApiResponse<List<UserActivity>>> GetUserActivityAsync(int userId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT ua.*, u.USUARIO, u.NOMBRE
                    FROM UserActivity ua
                    INNER JOIN Usuarios u ON ua.UserId = u.ID
                    WHERE ua.UserId = @UserId
                    ORDER BY ua.CreatedAt DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                var activities = new List<UserActivity>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    activities.Add(new UserActivity
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        UserId = Convert.ToInt32(reader["UserId"]),
                        Action = reader["Action"]?.ToString() ?? "",
                        Description = reader["Description"]?.ToString() ?? "",
                        EntityType = reader["EntityType"]?.ToString() ?? "",
                        EntityId = reader.IsDBNull("EntityId") ? null : Convert.ToInt32(reader["EntityId"]),
                        Metadata = reader["Metadata"]?.ToString() ?? "",
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                        User = new UserResponse
                        {
                            Id = Convert.ToInt32(reader["UserId"]),
                            Usuario = reader["USUARIO"]?.ToString() ?? "",
                            Nombre = reader["NOMBRE"]?.ToString() ?? ""
                        }
                    });
                }

                return new ApiResponse<List<UserActivity>>
                {
                    Success = true,
                    Message = "Actividad obtenida exitosamente",
                    Data = activities
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<UserActivity>>
                {
                    Success = false,
                    Message = $"Error al obtener actividad: {ex.Message}",
                    Data = new List<UserActivity>()
                };
            }
        }

        public async Task<ApiResponse<List<UserResponse>>> GetUsersByRoleAsync(UserProfile? perfil, string tienda = "")
        {
            var request = new GetUsersRequest
            {
                Perfil = perfil,
                Tienda = tienda,
                PageSize = 1000
            };

            return await GetUsersAsync(request);
        }

        public async Task<ApiResponse<PermissionCheckResponse>> CheckPermissionsAsync(int userId, CheckPermissionsRequest request)
        {
            try
            {
                // Obtener información del usuario
                var userResult = await GetUserByIdAsync(userId);
                if (!userResult.Success || userResult.Data == null)
                {
                    return new ApiResponse<PermissionCheckResponse>
                    {
                        Success = false,
                        Message = "Usuario no encontrado"
                    };
                }

                var user = userResult.Data;
                var hasPermission = false;
                string? reason = null;

                // Implementar lógica de permisos basada en el rol
                switch (request.Permission.ToUpper())
                {
                    case "CREATE_USER":
                    case "UPDATE_USER":
                    case "DELETE_USER":
                        hasPermission = user.Perfil == UserProfile.ADMINISTRADOR;
                        if (!hasPermission) reason = "Solo los administradores pueden gestionar usuarios";
                        break;

                    case "CREATE_REQUEST":
                        hasPermission = user.Activo;
                        if (!hasPermission) reason = "Usuario inactivo";
                        break;

                    case "ASSIGN_CODES":
                        hasPermission = user.Perfil == UserProfile.ADMINISTRADOR || 
                                       user.Perfil == UserProfile.GERENTE_TIENDA ||
                                       user.Perfil == UserProfile.LIDER;
                        if (!hasPermission) reason = "Sin permisos para asignar códigos";

                        // Verificar tienda si está en contexto
                        if (hasPermission && request.Context.ContainsKey("tienda"))
                        {
                            var contextTienda = request.Context["tienda"]?.ToString();
                            if (user.Perfil != UserProfile.ADMINISTRADOR && user.Tienda != contextTienda)
                            {
                                hasPermission = false;
                                reason = "Solo puede asignar códigos en su tienda";
                            }
                        }
                        break;

                    case "VIEW_ALL_REQUESTS":
                        hasPermission = user.Perfil == UserProfile.ADMINISTRADOR || 
                                       user.Perfil == UserProfile.GERENTE_TIENDA;
                        if (!hasPermission) reason = "Solo administradores y gerentes pueden ver todas las solicitudes";
                        break;

                    default:
                        hasPermission = false;
                        reason = "Permiso no reconocido";
                        break;
                }

                return new ApiResponse<PermissionCheckResponse>
                {
                    Success = true,
                    Message = "Permisos verificados",
                    Data = new PermissionCheckResponse
                    {
                        HasPermission = hasPermission,
                        Reason = reason
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PermissionCheckResponse>
                {
                    Success = false,
                    Message = $"Error al verificar permisos: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<SystemStatsResponse>> GetSystemStatsAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var stats = new SystemStatsResponse();

                // Estadísticas básicas
                const string basicStatsQuery = @"
                    SELECT 
                        (SELECT COUNT(*) FROM Usuarios WHERE IsActive = 1) as TotalUsers,
                        (SELECT COUNT(DISTINCT TIENDA) FROM Usuarios WHERE IsActive = 1) as TotalStores,
                        (SELECT COUNT(*) FROM ProductRequests WHERE IsActive = 1 AND Status IN ('PENDIENTE', 'EN_REVISION')) as ActiveRequests,
                        (SELECT COUNT(*) FROM InventoryCounts WHERE IsActive = 1 AND CANTIDAD_FISICA IS NOT NULL) as CompletedCounts,
                        (SELECT COUNT(*) FROM UserProductAssignments WHERE IsActive = 1) as PendingAssignments";

                using var basicCommand = new SqlCommand(basicStatsQuery, connection);
                using var basicReader = await basicCommand.ExecuteReaderAsync();

                if (await basicReader.ReadAsync())
                {
                    stats.TotalUsers = Convert.ToInt32(basicReader["TotalUsers"]);
                    stats.TotalStores = Convert.ToInt32(basicReader["TotalStores"]);
                    stats.ActiveRequests = Convert.ToInt32(basicReader["ActiveRequests"]);
                    stats.CompletedCounts = Convert.ToInt32(basicReader["CompletedCounts"]);
                    stats.PendingAssignments = Convert.ToInt32(basicReader["PendingAssignments"]);
                }
                basicReader.Close();

                // Usuarios por rol
                const string roleStatsQuery = @"
                    SELECT PERFIL as Role, COUNT(*) as Count
                    FROM Usuarios 
                    WHERE IsActive = 1
                    GROUP BY PERFIL";

                using var roleCommand = new SqlCommand(roleStatsQuery, connection);
                using var roleReader = await roleCommand.ExecuteReaderAsync();

                while (await roleReader.ReadAsync())
                {
                    stats.UsersByRole.Add(new RoleStatsItem
                    {
                        Role = roleReader["Role"]?.ToString() ?? "",
                        Count = Convert.ToInt32(roleReader["Count"])
                    });
                }
                roleReader.Close();

                // Solicitudes por estado
                const string statusStatsQuery = @"
                    SELECT Status, COUNT(*) as Count
                    FROM ProductRequests 
                    WHERE IsActive = 1
                    GROUP BY Status";

                using var statusCommand = new SqlCommand(statusStatsQuery, connection);
                using var statusReader = await statusCommand.ExecuteReaderAsync();

                while (await statusReader.ReadAsync())
                {
                    stats.RequestsByStatus.Add(new StatusStatsItem
                    {
                        Status = statusReader["Status"]?.ToString() ?? "",
                        Count = Convert.ToInt32(statusReader["Count"])
                    });
                }
                statusReader.Close();

                // Top tiendas por rendimiento
                const string performanceQuery = @"
                    SELECT TOP 5 
                        pr.Tienda,
                        CAST(COUNT(CASE WHEN pr.Status IN ('LISTO', 'AJUSTADO') THEN 1 END) * 100.0 / COUNT(*) AS DECIMAL(5,2)) as CompletionRate
                    FROM ProductRequests pr
                    WHERE pr.IsActive = 1
                    GROUP BY pr.Tienda
                    HAVING COUNT(*) > 0
                    ORDER BY CompletionRate DESC";

                using var performanceCommand = new SqlCommand(performanceQuery, connection);
                using var performanceReader = await performanceCommand.ExecuteReaderAsync();

                while (await performanceReader.ReadAsync())
                {
                    stats.TopPerformingStores.Add(new StorePerformanceItem
                    {
                        Tienda = performanceReader["Tienda"]?.ToString() ?? "",
                        CompletionRate = Convert.ToDecimal(performanceReader["CompletionRate"])
                    });
                }

                // Calcular crecimiento mensual (simplificado)
                stats.MonthlyGrowth = 15.2m; // Placeholder - implementar lógica real

                return new ApiResponse<SystemStatsResponse>
                {
                    Success = true,
                    Message = "Estadísticas obtenidas exitosamente",
                    Data = stats
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<SystemStatsResponse>
                {
                    Success = false,
                    Message = $"Error al obtener estadísticas: {ex.Message}",
                    Data = new SystemStatsResponse()
                };
            }
        }

        public async Task<ApiResponse<List<UserResponse>>> GetUsersByTiendaAsync(string tienda)
        {
            var request = new GetUsersRequest
            {
                Tienda = tienda,
                PageSize = 1000
            };

            return await GetUsersAsync(request);
        }

        // Métodos auxiliares privados
        private async Task<ApiResponse<UserResponse>> GetUserByIdAsync(int userId, SqlConnection? connection = null, SqlTransaction? transaction = null)
        {
            var shouldDisposeConnection = connection == null;
            if (connection == null)
            {
                connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
            }

            try
            {
                const string query = @"
                    SELECT u.*, 
                           STRING_AGG(ud.DivisionCode, ',') as DivisionesAsignadas
                    FROM Usuarios u
                    LEFT JOIN UserDivisions ud ON u.ID = ud.UserID AND ud.IsActive = 1
                    WHERE u.ID = @UserId AND u.IsActive = 1
                    GROUP BY u.ID, u.USUARIO, u.NOMBRE, u.EMAIL, u.PERFIL, u.TIENDA, u.AREA, 
                             u.IsActive, u.UltimoAcceso, u.FechaCreacion, u.FechaActualizacion";

                using var command = transaction != null ? 
                    new SqlCommand(query, connection, transaction) : 
                    new SqlCommand(query, connection);
                
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var user = new UserResponse
                    {
                        Id = Convert.ToInt32(reader["ID"]),
                        Usuario = reader["USUARIO"]?.ToString() ?? "",
                        Nombre = reader["NOMBRE"]?.ToString() ?? "",
                        Email = reader["EMAIL"]?.ToString() ?? "",
                        Perfil = Enum.Parse<UserProfile>(reader["PERFIL"]?.ToString() ?? "INVENTARIO"),
                        Tienda = reader["TIENDA"]?.ToString() ?? "",
                        Area = reader["AREA"]?.ToString() ?? "",
                        Activo = Convert.ToBoolean(reader["IsActive"]),
                        UltimoAcceso = reader.IsDBNull("UltimoAcceso") ? null : Convert.ToDateTime(reader["UltimoAcceso"]),
                        FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                        FechaActualizacion = Convert.ToDateTime(reader["FechaActualizacion"])
                    };

                    var divisionesStr = reader["DivisionesAsignadas"]?.ToString();
                    if (!string.IsNullOrEmpty(divisionesStr))
                    {
                        user.DivisionesAsignadas = divisionesStr.Split(',').ToList();
                    }

                    return new ApiResponse<UserResponse>
                    {
                        Success = true,
                        Data = user
                    };
                }

                return new ApiResponse<UserResponse>
                {
                    Success = false,
                    Message = "Usuario no encontrado"
                };
            }
            finally
            {
                if (shouldDisposeConnection)
                {
                    connection.Dispose();
                }
            }
        }

        private async Task AssignDivisionsToUserAsync(int userId, string tienda, List<string> divisionCodes, 
                                                     SqlConnection connection, SqlTransaction transaction)
        {
            foreach (var divisionCode in divisionCodes)
            {
                const string insertDivisionQuery = @"
                    INSERT INTO UserDivisions (UserID, Tienda, DivisionCode, IsActive, CreatedDate)
                    VALUES (@UserID, @Tienda, @DivisionCode, 1, GETDATE())";

                using var insertCommand = new SqlCommand(insertDivisionQuery, connection, transaction);
                insertCommand.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                insertCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;
                insertCommand.Parameters.Add("@DivisionCode", SqlDbType.NVarChar, 50).Value = divisionCode;
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        private async Task LogUserActivityAsync(int targetUserId, int actionUserId, string action, string description,
                                              string entityType, int? entityId, string? metadata,
                                              SqlConnection connection, SqlTransaction? transaction = null)
        {
            const string query = @"
                INSERT INTO UserActivity (UserId, Action, Description, EntityType, EntityId, Metadata, CreatedAt)
                VALUES (@UserId, @Action, @Description, @EntityType, @EntityId, @Metadata, GETDATE())";

            using var command = transaction != null ?
                new SqlCommand(query, connection, transaction) :
                new SqlCommand(query, connection);

            command.Parameters.Add("@UserId", SqlDbType.Int).Value = targetUserId;
            command.Parameters.Add("@Action", SqlDbType.NVarChar, 50).Value = action;
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = description;
            command.Parameters.Add("@EntityType", SqlDbType.NVarChar, 50).Value = entityType;
            command.Parameters.Add("@EntityId", SqlDbType.Int).Value = entityId ?? (object)DBNull.Value;
            command.Parameters.Add("@Metadata", SqlDbType.NVarChar).Value = metadata ?? (object)DBNull.Value;

            await command.ExecuteNonQueryAsync();
        }

        private string HashPassword(string password)
        {
            // Usar BCrypt o similar en producción
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "salt123"));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
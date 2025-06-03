using InventarioAPI.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Services
{
    public interface IStoreService
    {
        Task<ApiResponse<List<StoreResponse>>> GetStoresAsync();
        Task<ApiResponse<List<UserResponse>>> GetStoreUsersAsync(string tienda);
        Task<ApiResponse<StoreResponse>> GetStoreByCodeAsync(string tienda);
        Task<ApiResponse<StoreResponse>> CreateStoreAsync(CreateStoreRequest request, int createdBy);
        Task<ApiResponse<StoreResponse>> UpdateStoreAsync(string tienda, UpdateStoreRequest request, int updatedBy);
        Task<ApiResponse<bool>> DeleteStoreAsync(string tienda, int deletedBy);
        Task<ApiResponse<object>> GetStoreStatsAsync(string tienda);
    }

    public class StoreService : IStoreService
    {
        private readonly IConfiguration _configuration;
        private readonly string _inventarioConnection;
        private readonly ILogger<StoreService> _logger;

        public StoreService(IConfiguration configuration, ILogger<StoreService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _inventarioConnection = _configuration.GetConnectionString("InventarioConnection") 
                ?? throw new InvalidOperationException("InventarioConnection not found");
        }

        public async Task<ApiResponse<List<StoreResponse>>> GetStoresAsync()
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        s.Codigo as Tienda,
                        s.Nombre,
                        s.Direccion,
                        s.Telefono,
                        s.Email,
                        s.Activa,
                        COUNT(DISTINCT u.ID) as TotalUsuarios,
                        COUNT(DISTINCT CASE WHEN pr.Status IN ('PENDIENTE', 'EN_REVISION') THEN pr.ID END) as SolicitudesActivas,
                        MAX(CASE WHEN pr.IsActive = 1 THEN pr.CreatedDate END) as UltimaActividad
                    FROM Stores s
                    LEFT JOIN Usuarios u ON s.Codigo = u.TIENDA AND u.IsActive = 1
                    LEFT JOIN ProductRequests pr ON s.Codigo = pr.Tienda AND pr.IsActive = 1
                    WHERE s.Activa = 1
                    GROUP BY s.Codigo, s.Nombre, s.Direccion, s.Telefono, s.Email, s.Activa
                    ORDER BY s.Nombre";

                using var command = new SqlCommand(query, connection);

                var stores = new List<StoreResponse>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    stores.Add(new StoreResponse
                    {
                        Tienda = reader["Tienda"]?.ToString() ?? "",
                        Nombre = reader["Nombre"]?.ToString() ?? "",
                        Direccion = reader["Direccion"]?.ToString() ?? "",
                        Telefono = reader["Telefono"]?.ToString() ?? "",
                        Email = reader["Email"]?.ToString() ?? "",
                        Activa = Convert.ToBoolean(reader["Activa"]),
                        TotalUsuarios = Convert.ToInt32(reader["TotalUsuarios"]),
                        SolicitudesActivas = Convert.ToInt32(reader["SolicitudesActivas"]),
                        UltimaActividad = reader.IsDBNull("UltimaActividad") ? null : Convert.ToDateTime(reader["UltimaActividad"])
                    });
                }

                return new ApiResponse<List<StoreResponse>>
                {
                    Success = true,
                    Message = "Tiendas obtenidas exitosamente",
                    Data = stores
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tiendas");
                return new ApiResponse<List<StoreResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener tiendas: {ex.Message}",
                    Data = new List<StoreResponse>()
                };
            }
        }

        public async Task<ApiResponse<List<UserResponse>>> GetStoreUsersAsync(string tienda)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT u.*, 
                           STRING_AGG(ud.DivisionCode, ',') as DivisionesAsignadas
                    FROM Usuarios u
                    LEFT JOIN UserDivisions ud ON u.ID = ud.UserID AND ud.IsActive = 1 AND ud.Tienda = @Tienda
                    WHERE u.TIENDA = @Tienda AND u.IsActive = 1
                    GROUP BY u.ID, u.USUARIO, u.NOMBRE, u.EMAIL, u.PERFIL, u.TIENDA, u.AREA, 
                             u.IsActive, u.UltimoAcceso, u.FechaCreacion, u.FechaActualizacion
                    ORDER BY u.PERFIL, u.NOMBRE";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

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
                    Message = "Usuarios de la tienda obtenidos exitosamente",
                    Data = users
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios de la tienda: {Tienda}", tienda);
                return new ApiResponse<List<UserResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener usuarios de la tienda: {ex.Message}",
                    Data = new List<UserResponse>()
                };
            }
        }

        public async Task<ApiResponse<StoreResponse>> GetStoreByCodeAsync(string tienda)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        s.Codigo as Tienda,
                        s.Nombre,
                        s.Direccion,
                        s.Telefono,
                        s.Email,
                        s.Activa,
                        COUNT(DISTINCT u.ID) as TotalUsuarios,
                        COUNT(DISTINCT CASE WHEN pr.Status IN ('PENDIENTE', 'EN_REVISION') THEN pr.ID END) as SolicitudesActivas,
                        MAX(CASE WHEN pr.IsActive = 1 THEN pr.CreatedDate END) as UltimaActividad
                    FROM Stores s
                    LEFT JOIN Usuarios u ON s.Codigo = u.TIENDA AND u.IsActive = 1
                    LEFT JOIN ProductRequests pr ON s.Codigo = pr.Tienda AND pr.IsActive = 1
                    WHERE s.Codigo = @Tienda AND s.Activa = 1
                    GROUP BY s.Codigo, s.Nombre, s.Direccion, s.Telefono, s.Email, s.Activa";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var store = new StoreResponse
                    {
                        Tienda = reader["Tienda"]?.ToString() ?? "",
                        Nombre = reader["Nombre"]?.ToString() ?? "",
                        Direccion = reader["Direccion"]?.ToString() ?? "",
                        Telefono = reader["Telefono"]?.ToString() ?? "",
                        Email = reader["Email"]?.ToString() ?? "",
                        Activa = Convert.ToBoolean(reader["Activa"]),
                        TotalUsuarios = Convert.ToInt32(reader["TotalUsuarios"]),
                        SolicitudesActivas = Convert.ToInt32(reader["SolicitudesActivas"]),
                        UltimaActividad = reader.IsDBNull("UltimaActividad") ? null : Convert.ToDateTime(reader["UltimaActividad"])
                    };

                    return new ApiResponse<StoreResponse>
                    {
                        Success = true,
                        Message = "Tienda obtenida exitosamente",
                        Data = store
                    };
                }

                return new ApiResponse<StoreResponse>
                {
                    Success = false,
                    Message = "Tienda no encontrada"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tienda: {Tienda}", tienda);
                return new ApiResponse<StoreResponse>
                {
                    Success = false,
                    Message = $"Error al obtener tienda: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<StoreResponse>> CreateStoreAsync(CreateStoreRequest request, int createdBy)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Verificar si el código de tienda ya existe
                    const string checkQuery = "SELECT COUNT(1) FROM Stores WHERE Codigo = @Codigo";
                    using var checkCommand = new SqlCommand(checkQuery, connection, transaction);
                    checkCommand.Parameters.Add("@Codigo", SqlDbType.NVarChar, 50).Value = request.Codigo;

                    var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                    if (exists)
                    {
                        return new ApiResponse<StoreResponse>
                        {
                            Success = false,
                            Message = "El código de tienda ya existe"
                        };
                    }

                    // Crear la tienda
                    const string insertQuery = @"
                        INSERT INTO Stores (Codigo, Nombre, Direccion, Telefono, Email, Activa, FechaCreacion, FechaActualizacion, CreatedBy)
                        VALUES (@Codigo, @Nombre, @Direccion, @Telefono, @Email, 1, GETDATE(), GETDATE(), @CreatedBy)";

                    using var insertCommand = new SqlCommand(insertQuery, connection, transaction);
                    insertCommand.Parameters.Add("@Codigo", SqlDbType.NVarChar, 50).Value = request.Codigo;
                    insertCommand.Parameters.Add("@Nombre", SqlDbType.NVarChar, 200).Value = request.Nombre;
                    insertCommand.Parameters.Add("@Direccion", SqlDbType.NVarChar, 500).Value = request.Direccion ?? (object)DBNull.Value;
                    insertCommand.Parameters.Add("@Telefono", SqlDbType.NVarChar, 50).Value = request.Telefono ?? (object)DBNull.Value;
                    insertCommand.Parameters.Add("@Email", SqlDbType.NVarChar, 200).Value = request.Email ?? (object)DBNull.Value;
                    insertCommand.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = createdBy;

                    await insertCommand.ExecuteNonQueryAsync();

                    // Registrar actividad
                    await LogStoreActivityAsync(request.Codigo, createdBy, "store_created", 
                                              $"Tienda {request.Nombre} creada", "store", null, 
                                              $"{{\"codigo\": \"{request.Codigo}\", \"nombre\": \"{request.Nombre}\"}}", 
                                              connection, transaction);

                    transaction.Commit();

                    // Obtener la tienda creada
                    var createdStore = await GetStoreByCodeAsync(request.Codigo);
                    return new ApiResponse<StoreResponse>
                    {
                        Success = true,
                        Message = "Tienda creada exitosamente",
                        Data = createdStore.Data
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
                _logger.LogError(ex, "Error al crear tienda: {Codigo}", request.Codigo);
                return new ApiResponse<StoreResponse>
                {
                    Success = false,
                    Message = $"Error al crear tienda: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<StoreResponse>> UpdateStoreAsync(string tienda, UpdateStoreRequest request, int updatedBy)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Verificar que la tienda existe
                    var existingStore = await GetStoreByCodeAsync(tienda);
                    if (!existingStore.Success || existingStore.Data == null)
                    {
                        return new ApiResponse<StoreResponse>
                        {
                            Success = false,
                            Message = "Tienda no encontrada"
                        };
                    }

                    // Construir query de actualización dinámicamente
                    var updateFields = new List<string>();
                    var parameters = new List<SqlParameter>();

                    if (!string.IsNullOrEmpty(request.Nombre))
                    {
                        updateFields.Add("Nombre = @Nombre");
                        parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = request.Nombre });
                    }

                    if (request.Direccion != null)
                    {
                        updateFields.Add("Direccion = @Direccion");
                        parameters.Add(new SqlParameter("@Direccion", SqlDbType.NVarChar, 500) { Value = request.Direccion });
                    }

                    if (request.Telefono != null)
                    {
                        updateFields.Add("Telefono = @Telefono");
                        parameters.Add(new SqlParameter("@Telefono", SqlDbType.NVarChar, 50) { Value = request.Telefono });
                    }

                    if (request.Email != null)
                    {
                        updateFields.Add("Email = @Email");
                        parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 200) { Value = request.Email });
                    }

                    if (request.Activa.HasValue)
                    {
                        updateFields.Add("Activa = @Activa");
                        parameters.Add(new SqlParameter("@Activa", SqlDbType.Bit) { Value = request.Activa.Value });
                    }

                    if (updateFields.Any())
                    {
                        updateFields.Add("FechaActualizacion = GETDATE()");
                        updateFields.Add("UpdatedBy = @UpdatedBy");
                        parameters.Add(new SqlParameter("@UpdatedBy", SqlDbType.Int) { Value = updatedBy });
                        parameters.Add(new SqlParameter("@Tienda", SqlDbType.NVarChar, 50) { Value = tienda });

                        var updateQuery = $@"
                            UPDATE Stores 
                            SET {string.Join(", ", updateFields)}
                            WHERE Codigo = @Tienda";

                        using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                        updateCommand.Parameters.AddRange(parameters.ToArray());
                        await updateCommand.ExecuteNonQueryAsync();

                        // Registrar actividad
                        await LogStoreActivityAsync(tienda, updatedBy, "store_updated", 
                                                  $"Tienda {tienda} actualizada", "store", null, 
                                                  $"{{\"cambios\": \"{string.Join(", ", updateFields)}\"}}", 
                                                  connection, transaction);
                    }

                    transaction.Commit();

                    // Obtener la tienda actualizada
                    var updatedStore = await GetStoreByCodeAsync(tienda);
                    return new ApiResponse<StoreResponse>
                    {
                        Success = true,
                        Message = "Tienda actualizada exitosamente",
                        Data = updatedStore.Data
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
                _logger.LogError(ex, "Error al actualizar tienda: {Tienda}", tienda);
                return new ApiResponse<StoreResponse>
                {
                    Success = false,
                    Message = $"Error al actualizar tienda: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<bool>> DeleteStoreAsync(string tienda, int deletedBy)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Verificar que no hay usuarios activos en la tienda
                    const string checkUsersQuery = "SELECT COUNT(1) FROM Usuarios WHERE TIENDA = @Tienda AND IsActive = 1";
                    using var checkUsersCommand = new SqlCommand(checkUsersQuery, connection, transaction);
                    checkUsersCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

                    var activeUsers = Convert.ToInt32(await checkUsersCommand.ExecuteScalarAsync());
                    if (activeUsers > 0)
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = $"No se puede eliminar la tienda. Tiene {activeUsers} usuarios activos."
                        };
                    }

                    // Verificar que no hay solicitudes activas
                    const string checkRequestsQuery = "SELECT COUNT(1) FROM ProductRequests WHERE Tienda = @Tienda AND IsActive = 1";
                    using var checkRequestsCommand = new SqlCommand(checkRequestsQuery, connection, transaction);
                    checkRequestsCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

                    var activeRequests = Convert.ToInt32(await checkRequestsCommand.ExecuteScalarAsync());
                    if (activeRequests > 0)
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = $"No se puede eliminar la tienda. Tiene {activeRequests} solicitudes activas."
                        };
                    }

                    // Desactivar la tienda (soft delete)
                    const string deleteQuery = @"
                        UPDATE Stores 
                        SET Activa = 0, FechaActualizacion = GETDATE(), UpdatedBy = @DeletedBy
                        WHERE Codigo = @Tienda";

                    using var deleteCommand = new SqlCommand(deleteQuery, connection, transaction);
                    deleteCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;
                    deleteCommand.Parameters.Add("@DeletedBy", SqlDbType.Int).Value = deletedBy;

                    var rowsAffected = await deleteCommand.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "Tienda no encontrada"
                        };
                    }

                    // Registrar actividad
                    await LogStoreActivityAsync(tienda, deletedBy, "store_deleted", 
                                              $"Tienda {tienda} eliminada", "store", null, null, 
                                              connection, transaction);

                    transaction.Commit();

                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Message = "Tienda eliminada exitosamente",
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
                _logger.LogError(ex, "Error al eliminar tienda: {Tienda}", tienda);
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Error al eliminar tienda: {ex.Message}",
                    Data = false
                };
            }
        }

        public async Task<ApiResponse<object>> GetStoreStatsAsync(string tienda)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var stats = new
                {
                    Tienda = tienda,
                    Usuarios = await GetUserStatsAsync(tienda, connection),
                    Solicitudes = await GetRequestStatsAsync(tienda, connection),
                    Conteos = await GetCountStatsAsync(tienda, connection),
                    Actividad = await GetActivityStatsAsync(tienda, connection)
                };

                return new ApiResponse<object>
                {
                    Success = true,
                    Message = "Estadísticas de tienda obtenidas exitosamente",
                    Data = stats
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de tienda: {Tienda}", tienda);
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = $"Error al obtener estadísticas: {ex.Message}"
                };
            }
        }

        // Métodos auxiliares privados
        private async Task<object> GetUserStatsAsync(string tienda, SqlConnection connection)
        {
            const string query = @"
                SELECT 
                    COUNT(*) as Total,
                    COUNT(CASE WHEN IsActive = 1 THEN 1 END) as Activos,
                    COUNT(CASE WHEN PERFIL = 'ADMINISTRADOR' THEN 1 END) as Administradores,
                    COUNT(CASE WHEN PERFIL = 'GERENTE_TIENDA' THEN 1 END) as Gerentes,
                    COUNT(CASE WHEN PERFIL = 'LIDER' THEN 1 END) as Lideres,
                    COUNT(CASE WHEN PERFIL = 'INVENTARIO' THEN 1 END) as UsuariosInventario,
                    COUNT(CASE WHEN UltimoAcceso IS NOT NULL AND UltimoAcceso >= DATEADD(day, -30, GETDATE()) THEN 1 END) as ActivosUltimos30Dias
                FROM Usuarios 
                WHERE TIENDA = @Tienda";

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new
                {
                    Total = Convert.ToInt32(reader["Total"]),
                    Activos = Convert.ToInt32(reader["Activos"]),
                    Administradores = Convert.ToInt32(reader["Administradores"]),
                    Gerentes = Convert.ToInt32(reader["Gerentes"]),
                    Lideres = Convert.ToInt32(reader["Lideres"]),
                    UsuariosInventario = Convert.ToInt32(reader["UsuariosInventario"]),
                    ActivosUltimos30Dias = Convert.ToInt32(reader["ActivosUltimos30Dias"])
                };
            }

            return new { Total = 0, Activos = 0 };
        }

        private async Task<object> GetRequestStatsAsync(string tienda, SqlConnection connection)
        {
            const string query = @"
                SELECT 
                    COUNT(*) as Total,
                    COUNT(CASE WHEN Status = 'PENDIENTE' THEN 1 END) as Pendientes,
                    COUNT(CASE WHEN Status = 'EN_REVISION' THEN 1 END) as EnRevision,
                    COUNT(CASE WHEN Status = 'LISTO' THEN 1 END) as Listos,
                    COUNT(CASE WHEN Status = 'AJUSTADO' THEN 1 END) as Ajustados,
                    COUNT(CASE WHEN DueDate < GETDATE() AND Status NOT IN ('LISTO', 'AJUSTADO') THEN 1 END) as Vencidas
                FROM ProductRequests 
                WHERE Tienda = @Tienda AND IsActive = 1";

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new
                {
                    Total = Convert.ToInt32(reader["Total"]),
                    Pendientes = Convert.ToInt32(reader["Pendientes"]),
                    EnRevision = Convert.ToInt32(reader["EnRevision"]),
                    Listos = Convert.ToInt32(reader["Listos"]),
                    Ajustados = Convert.ToInt32(reader["Ajustados"]),
                    Vencidas = Convert.ToInt32(reader["Vencidas"])
                };
            }

            return new { Total = 0, Pendientes = 0 };
        }

        private async Task<object> GetCountStatsAsync(string tienda, SqlConnection connection)
        {
            const string query = @"
                SELECT 
                    COUNT(*) as Total,
                    COUNT(CASE WHEN CANTIDAD_FISICA IS NOT NULL THEN 1 END) as Completados,
                    COUNT(CASE WHEN ABS(ISNULL(CANTIDAD_FISICA, 0) - STOCK_CALCULADO) > 0.01 THEN 1 END) as ConDiferencias,
                    SUM(COSTO_TOTAL) as TotalCostoDiferencias
                FROM InventoryCounts 
                WHERE TIENDA = @Tienda AND IsActive = 1";

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new
                {
                    Total = Convert.ToInt32(reader["Total"]),
                    Completados = Convert.ToInt32(reader["Completados"]),
                    ConDiferencias = Convert.ToInt32(reader["ConDiferencias"]),
                    TotalCostoDiferencias = Convert.ToDecimal(reader["TotalCostoDiferencias"] ?? 0)
                };
            }

            return new { Total = 0, Completados = 0 };
        }

        private async Task<object> GetActivityStatsAsync(string tienda, SqlConnection connection)
        {
            const string query = @"
                SELECT 
                    COUNT(*) as TotalActividades,
                    COUNT(CASE WHEN ua.CreatedAt >= DATEADD(day, -7, GETDATE()) THEN 1 END) as ActividadesUltimos7Dias,
                    COUNT(CASE WHEN ua.Action = 'login' AND ua.CreatedAt >= DATEADD(day, -1, GETDATE()) THEN 1 END) as LoginsUltimas24Horas
                FROM UserActivity ua
                INNER JOIN Usuarios u ON ua.UserId = u.ID
                WHERE u.TIENDA = @Tienda";

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new
                {
                    TotalActividades = Convert.ToInt32(reader["TotalActividades"]),
                    ActividadesUltimos7Dias = Convert.ToInt32(reader["ActividadesUltimos7Dias"]),
                    LoginsUltimas24Horas = Convert.ToInt32(reader["LoginsUltimas24Horas"])
                };
            }

            return new { TotalActividades = 0 };
        }

        private async Task LogStoreActivityAsync(string tienda, int userId, string action, string description,
                                               string entityType, int? entityId, string? metadata,
                                               SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                INSERT INTO UserActivity (UserId, Action, Description, EntityType, EntityId, Metadata, CreatedAt)
                VALUES (@UserId, @Action, @Description, @EntityType, @EntityId, @Metadata, GETDATE())";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            command.Parameters.Add("@Action", SqlDbType.NVarChar, 50).Value = action;
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = description;
            command.Parameters.Add("@EntityType", SqlDbType.NVarChar, 50).Value = entityType;
            command.Parameters.Add("@EntityId", SqlDbType.Int).Value = entityId ?? (object)DBNull.Value;
            command.Parameters.Add("@Metadata", SqlDbType.NVarChar).Value = metadata ?? (object)DBNull.Value;

            await command.ExecuteNonQueryAsync();
        }
    }

    // Modelos de request para StoreService
    public class CreateStoreRequest
    {
        [Required(ErrorMessage = "El código es requerido")]
        [StringLength(50, ErrorMessage = "El código no puede exceder 50 caracteres")]
        public string Codigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "La dirección no puede exceder 500 caracteres")]
        public string? Direccion { get; set; }

        [StringLength(50, ErrorMessage = "El teléfono no puede exceder 50 caracteres")]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "El email no es válido")]
        [StringLength(200, ErrorMessage = "El email no puede exceder 200 caracteres")]
        public string? Email { get; set; }
    }

    public class UpdateStoreRequest
    {
        [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string? Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La dirección no puede exceder 500 caracteres")]
        public string? Direccion { get; set; }

        [StringLength(50, ErrorMessage = "El teléfono no puede exceder 50 caracteres")]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "El email no es válido")]
        [StringLength(200, ErrorMessage = "El email no puede exceder 200 caracteres")]
        public string? Email { get; set; }

        public bool? Activa { get; set; }
    }
}
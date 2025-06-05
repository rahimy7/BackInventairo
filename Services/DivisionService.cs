using InventarioAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using System.Data;

namespace InventarioAPI.Services
{
    public interface IDivisionService
    {
        Task<ApiResponse<List<DivisionResponse>>> GetDivisionsAsync();
        Task<ApiResponse<List<DivisionResponse>>> GetDivisionsByStoreAsync(string tienda);
        Task<ApiResponse<DivisionAssignmentResponse>> AssignDivisionsToLeaderAsync(AssignDivisionsRequest request, int assignedBy);
        Task<ApiResponse<List<string>>> GetUserDivisionsAsync(int userId, string tienda);
        Task<ApiResponse<bool>> RemoveDivisionAssignmentAsync(int userId, string tienda, string divisionCode, int removedBy);
        Task<ApiResponse<object>> GetDivisionStatsAsync(string? tienda = null);
        Task<ApiResponse<List<UserResponse>>> GetDivisionLeadersAsync(string divisionCode, string? tienda = null);
        Task<ApiResponse<DivisionResponse>> GetDivisionByCodeAsync(string divisionCode);
        Task<ApiResponse<List<DivisionResponse>>> GetDivisionsWithAssignmentsAsync(string? tienda = null);
        Task<ApiResponse<bool>> ValidateDivisionAccessAsync(int userId, string divisionCode, string tienda);
        Task<ApiResponse<object>> GetDivisionHierarchyAsync();
        Task<ApiResponse<bool>> BulkAssignDivisionsAsync(List<AssignDivisionsRequest> assignments, int assignedBy);
        Task ClearDivisionCacheAsync();
    }

    public class DivisionService : IDivisionService
    {
        private readonly IConfiguration _configuration;
        private readonly string _inventarioConnection;
        private readonly string _innovacentroConnection;
        private readonly ILogger<DivisionService> _logger;
        private readonly IMemoryCache _cache;

        // Cache keys
        private const string CACHE_KEY_ALL_DIVISIONS = "divisions_all";
        private const string CACHE_KEY_DIVISION_PREFIX = "division_";
        private const string CACHE_KEY_USER_DIVISIONS_PREFIX = "user_divisions_";
        private const int CACHE_EXPIRATION_MINUTES = 30;

        public DivisionService(IConfiguration configuration, ILogger<DivisionService> logger, IMemoryCache cache)
        {
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
            _inventarioConnection = _configuration.GetConnectionString("InventarioConnection") 
                ?? throw new InvalidOperationException("InventarioConnection not found");
            _innovacentroConnection = _configuration.GetConnectionString("InnovacentroConnection") 
                ?? throw new InvalidOperationException("InnovacentroConnection not found");
        }

        public async Task<ApiResponse<List<DivisionResponse>>> GetDivisionsAsync()
        {
            try
            {
                // Intentar obtener desde caché
                if (_cache.TryGetValue(CACHE_KEY_ALL_DIVISIONS, out List<DivisionResponse>? cachedDivisions))
                {
                    _logger.LogInformation("Divisiones obtenidas desde caché");
                    return new ApiResponse<List<DivisionResponse>>
                    {
                        Success = true,
                        Message = "Divisiones obtenidas exitosamente (caché)",
                        Data = cachedDivisions!
                    };
                }

                using var connection = new SqlConnection(_innovacentroConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT DISTINCT 
                        [Division Code] as Codigo,
                        [Division] as Nombre,
                        [Division] as Descripcion,
                        COUNT(*) OVER (PARTITION BY [Division Code]) as ProductCount
                    FROM [INNOVACENTRO].[dbo].[View_ProductosLI]
                    WHERE [Division Code] IS NOT NULL 
                        AND [Division Code] != ''
                        AND [Division] IS NOT NULL 
                        AND [Division] != ''
                        AND [Blocked] = 0
                    ORDER BY [Division]";

                using var command = new SqlCommand(query, connection);

                var divisions = new List<DivisionResponse>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    divisions.Add(new DivisionResponse
                    {
                        Codigo = reader["Codigo"]?.ToString() ?? "",
                        Nombre = reader["Nombre"]?.ToString() ?? "",
                        Descripcion = reader["Descripcion"]?.ToString() ?? "",
                        Activa = true,
                        Tienda = null // null = todas las tiendas
                    });
                }

                // Guardar en caché
                _cache.Set(CACHE_KEY_ALL_DIVISIONS, divisions, TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES));

                _logger.LogInformation("Se obtuvieron {Count} divisiones desde la base de datos", divisions.Count);

                return new ApiResponse<List<DivisionResponse>>
                {
                    Success = true,
                    Message = "Divisiones obtenidas exitosamente",
                    Data = divisions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener divisiones");
                return new ApiResponse<List<DivisionResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener divisiones: {ex.Message}",
                    Data = new List<DivisionResponse>()
                };
            }
        }

        public async Task<ApiResponse<List<DivisionResponse>>> GetDivisionsByStoreAsync(string tienda)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tienda))
                {
                    return new ApiResponse<List<DivisionResponse>>
                    {
                        Success = false,
                        Message = "El código de tienda es requerido"
                    };
                }

                var cacheKey = $"divisions_store_{tienda}";
                if (_cache.TryGetValue(cacheKey, out List<DivisionResponse>? cachedDivisions))
                {
                    return new ApiResponse<List<DivisionResponse>>
                    {
                        Success = true,
                        Message = "Divisiones de tienda obtenidas exitosamente (caché)",
                        Data = cachedDivisions!
                    };
                }

                // Por ahora, todas las divisiones están disponibles para todas las tiendas
                var allDivisionsResult = await GetDivisionsAsync();
                
                if (allDivisionsResult.Success && allDivisionsResult.Data != null)
                {
                    // Marcar las divisiones específicas de la tienda
                    foreach (var division in allDivisionsResult.Data)
                    {
                        division.Tienda = tienda;
                    }

                    // Obtener información adicional sobre asignaciones en esta tienda
                    using var connection = new SqlConnection(_inventarioConnection);
                    await connection.OpenAsync();

                    const string assignmentQuery = @"
    SELECT 
        ud.DivisionCode,
        COUNT(DISTINCT ud.UserID) as LeadersAsignados,
        STUFF((SELECT ', ' + u2.NOMBRE 
               FROM UserDivisions ud2 
               INNER JOIN Usuarios u2 ON ud2.UserID = u2.ID
               WHERE ud2.DivisionCode = ud.DivisionCode 
                 AND ud2.Tienda = @Tienda 
                 AND ud2.IsActive = 1 
                 AND u2.IsActive = 1
               FOR XML PATH('')), 1, 2, '') as NombresLideres
    FROM UserDivisions ud
    INNER JOIN Usuarios u ON ud.UserID = u.ID
    WHERE ud.Tienda = @Tienda AND ud.IsActive = 1 AND u.IsActive = 1
    GROUP BY ud.DivisionCode";

                    using var command = new SqlCommand(assignmentQuery, connection);
                    command.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

                    var assignmentDict = new Dictionary<string, (int Count, string Names)>();
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var divisionCode = reader["DivisionCode"]?.ToString() ?? "";
                        var count = Convert.ToInt32(reader["LeadersAsignados"]);
                        var names = reader["NombresLideres"]?.ToString() ?? "";
                        assignmentDict[divisionCode] = (count, names);
                    }

                    // Agregar información de asignaciones a las divisiones
                    foreach (var division in allDivisionsResult.Data)
                    {
                        if (assignmentDict.TryGetValue(division.Codigo, out var assignment))
                        {
                            division.Descripcion += $" ({assignment.Count} líder{(assignment.Count != 1 ? "es" : "")} asignado{(assignment.Count != 1 ? "s" : "")})";
                        }
                    }

                    // Guardar en caché
                    _cache.Set(cacheKey, allDivisionsResult.Data, TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES));
                }

                return allDivisionsResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener divisiones de la tienda: {Tienda}", tienda);
                return new ApiResponse<List<DivisionResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener divisiones de la tienda: {ex.Message}",
                    Data = new List<DivisionResponse>()
                };
            }
        }

        public async Task<ApiResponse<DivisionAssignmentResponse>> AssignDivisionsToLeaderAsync(AssignDivisionsRequest request, int assignedBy)
        {
            try
            {
                if (request?.DivisionCodes == null || !request.DivisionCodes.Any())
                {
                    return new ApiResponse<DivisionAssignmentResponse>
                    {
                        Success = false,
                        Message = "Debe especificar al menos una división"
                    };
                }

                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Verificar que el usuario existe y es LIDER
                    const string checkUserQuery = @"
                        SELECT PERFIL, NOMBRE, TIENDA FROM Usuarios 
                        WHERE ID = @UserId AND IsActive = 1";

                    using var checkCommand = new SqlCommand(checkUserQuery, connection, transaction);
                    checkCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = request.UserId;

                    using var reader = await checkCommand.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return new ApiResponse<DivisionAssignmentResponse>
                        {
                            Success = false,
                            Message = "Usuario no encontrado"
                        };
                    }

                    var userProfile = reader["PERFIL"]?.ToString();
                    var userName = reader["NOMBRE"]?.ToString();
                    var userTienda = reader["TIENDA"]?.ToString();
                    reader.Close();

                    if (userProfile != "LIDER")
                    {
                        return new ApiResponse<DivisionAssignmentResponse>
                        {
                            Success = false,
                            Message = "Solo se pueden asignar divisiones a usuarios con perfil LIDER"
                        };
                    }

                    // Verificar que el usuario pertenece a la tienda especificada
                    if (userTienda != request.Tienda)
                    {
                        return new ApiResponse<DivisionAssignmentResponse>
                        {
                            Success = false,
                            Message = "El usuario no pertenece a la tienda especificada"
                        };
                    }

                    // Verificar que las divisiones existen
                    var validDivisions = await ValidateDivisionsExistAsync(request.DivisionCodes);
                    if (validDivisions.Count != request.DivisionCodes.Count)
                    {
                        var invalidDivisions = request.DivisionCodes.Except(validDivisions).ToList();
                        return new ApiResponse<DivisionAssignmentResponse>
                        {
                            Success = false,
                            Message = $"Las siguientes divisiones no son válidas: {string.Join(", ", invalidDivisions)}"
                        };
                    }

                    // Obtener asignaciones anteriores para auditoría
                    var previousAssignments = await GetUserDivisionsInternalAsync(request.UserId, request.Tienda, connection, transaction);

                    // Desactivar asignaciones anteriores para esta tienda
                    const string deactivateQuery = @"
                        UPDATE UserDivisions 
                        SET IsActive = 0, UpdatedDate = GETDATE(), UpdatedBy = @AssignedBy
                        WHERE UserID = @UserId AND Tienda = @Tienda AND IsActive = 1";

                    using var deactivateCommand = new SqlCommand(deactivateQuery, connection, transaction);
                    deactivateCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = request.UserId;
                    deactivateCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = request.Tienda;
                    deactivateCommand.Parameters.Add("@AssignedBy", SqlDbType.Int).Value = assignedBy;

                    await deactivateCommand.ExecuteNonQueryAsync();

                    // Asignar nuevas divisiones
                    foreach (var divisionCode in request.DivisionCodes)
                    {
                        const string insertQuery = @"
                            INSERT INTO UserDivisions (UserID, Tienda, DivisionCode, IsActive, CreatedDate, CreatedBy)
                            VALUES (@UserID, @Tienda, @DivisionCode, 1, GETDATE(), @AssignedBy)";

                        using var insertCommand = new SqlCommand(insertQuery, connection, transaction);
                        insertCommand.Parameters.Add("@UserID", SqlDbType.Int).Value = request.UserId;
                        insertCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = request.Tienda;
                        insertCommand.Parameters.Add("@DivisionCode", SqlDbType.NVarChar, 50).Value = divisionCode;
                        insertCommand.Parameters.Add("@AssignedBy", SqlDbType.Int).Value = assignedBy;

                        await insertCommand.ExecuteNonQueryAsync();
                    }

                    // Registrar actividad
                    var metadata = new
                    {
                        tienda = request.Tienda,
                        divisiones_anteriores = previousAssignments,
                        divisiones_nuevas = request.DivisionCodes,
                        usuario_objetivo = userName
                    };

                    await LogDivisionActivityAsync(request.UserId, assignedBy, "divisions_assigned", 
                                                 $"Divisiones asignadas a {userName}: {string.Join(", ", request.DivisionCodes)}", 
                                                 "division_assignment", request.UserId, 
                                                 System.Text.Json.JsonSerializer.Serialize(metadata),
                                                 connection, transaction);

                    transaction.Commit();

                    // Limpiar caché relacionado
                    await ClearUserDivisionCacheAsync(request.UserId, request.Tienda);

                    _logger.LogInformation("Divisiones asignadas exitosamente. Usuario: {UserId}, Tienda: {Tienda}, Divisiones: {Divisiones}", 
                        request.UserId, request.Tienda, string.Join(", ", request.DivisionCodes));

                    return new ApiResponse<DivisionAssignmentResponse>
                    {
                        Success = true,
                        Message = "Divisiones asignadas exitosamente",
                        Data = new DivisionAssignmentResponse
                        {
                            UserId = request.UserId,
                            Tienda = request.Tienda,
                            DivisionesAsignadas = request.DivisionCodes
                        }
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
                _logger.LogError(ex, "Error al asignar divisiones al usuario: {UserId}", request?.UserId);
                return new ApiResponse<DivisionAssignmentResponse>
                {
                    Success = false,
                    Message = $"Error al asignar divisiones: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<List<string>>> GetUserDivisionsAsync(int userId, string tienda)
        {
            try
            {
                var cacheKey = $"{CACHE_KEY_USER_DIVISIONS_PREFIX}{userId}_{tienda}";
                if (_cache.TryGetValue(cacheKey, out List<string>? cachedDivisions))
                {
                    return new ApiResponse<List<string>>
                    {
                        Success = true,
                        Message = "Divisiones del usuario obtenidas exitosamente (caché)",
                        Data = cachedDivisions!
                    };
                }

                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var divisions = await GetUserDivisionsInternalAsync(userId, tienda, connection);

                // Guardar en caché
                _cache.Set(cacheKey, divisions, TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES));

                return new ApiResponse<List<string>>
                {
                    Success = true,
                    Message = "Divisiones del usuario obtenidas exitosamente",
                    Data = divisions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener divisiones del usuario: {UserId} en tienda: {Tienda}", userId, tienda);
                return new ApiResponse<List<string>>
                {
                    Success = false,
                    Message = $"Error al obtener divisiones del usuario: {ex.Message}",
                    Data = new List<string>()
                };
            }
        }

        public async Task<ApiResponse<bool>> RemoveDivisionAssignmentAsync(int userId, string tienda, string divisionCode, int removedBy)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Verificar que la asignación existe
                    const string checkQuery = @"
                        SELECT COUNT(1) FROM UserDivisions 
                        WHERE UserID = @UserId AND Tienda = @Tienda AND DivisionCode = @DivisionCode AND IsActive = 1";

                    using var checkCommand = new SqlCommand(checkQuery, connection, transaction);
                    checkCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    checkCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;
                    checkCommand.Parameters.Add("@DivisionCode", SqlDbType.NVarChar, 50).Value = divisionCode;

                    var assignmentExists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                    if (!assignmentExists)
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "La asignación no existe o ya fue removida"
                        };
                    }

                    // Desactivar la asignación específica
                    const string removeQuery = @"
                        UPDATE UserDivisions 
                        SET IsActive = 0, UpdatedDate = GETDATE(), UpdatedBy = @RemovedBy
                        WHERE UserID = @UserId AND Tienda = @Tienda AND DivisionCode = @DivisionCode AND IsActive = 1";

                    using var removeCommand = new SqlCommand(removeQuery, connection, transaction);
                    removeCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    removeCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;
                    removeCommand.Parameters.Add("@DivisionCode", SqlDbType.NVarChar, 50).Value = divisionCode;
                    removeCommand.Parameters.Add("@RemovedBy", SqlDbType.Int).Value = removedBy;

                    var rowsAffected = await removeCommand.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "Asignación no encontrada o ya removida"
                        };
                    }

                    // Obtener nombre del usuario para el log
                    const string getUserQuery = "SELECT NOMBRE FROM Usuarios WHERE ID = @UserId";
                    using var getUserCommand = new SqlCommand(getUserQuery, connection, transaction);
                    getUserCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    
                    var userName =  getUserCommand.ExecuteScalarAsync()?.ToString() ?? "Usuario";

                    // Registrar actividad
                    await LogDivisionActivityAsync(userId, removedBy, "division_removed", 
                                                 $"División {divisionCode} removida de {userName} en tienda {tienda}", 
                                                 "division_assignment", userId, 
                                                 $"{{\"tienda\": \"{tienda}\", \"division\": \"{divisionCode}\"}}",
                                                 connection, transaction);

                    transaction.Commit();

                    // Limpiar caché
                    await ClearUserDivisionCacheAsync(userId, tienda);

                    _logger.LogInformation("División {DivisionCode} removida del usuario {UserId} en tienda {Tienda}", 
                        divisionCode, userId, tienda);

                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Message = "Asignación de división removida exitosamente",
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
                _logger.LogError(ex, "Error al remover asignación de división para usuario: {UserId}", userId);
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Error al remover asignación: {ex.Message}",
                    Data = false
                };
            }
        }

        public async Task<ApiResponse<object>> GetDivisionStatsAsync(string? tienda = null)
        {
            try
            {
                var cacheKey = $"division_stats_{tienda ?? "all"}";
                if (_cache.TryGetValue(cacheKey, out object? cachedStats))
                {
                    return new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Estadísticas de divisiones obtenidas exitosamente (caché)",
                        Data = cachedStats!
                    };
                }

                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var whereClause = string.IsNullOrEmpty(tienda) ? "" : "WHERE ud.Tienda = @Tienda";
                var parameters = new List<SqlParameter>();
                
                if (!string.IsNullOrEmpty(tienda))
                {
                    parameters.Add(new SqlParameter("@Tienda", SqlDbType.NVarChar, 50) { Value = tienda });
                }

                // Estadísticas de asignaciones
                var assignmentStatsQuery = $@"
    SELECT 
        ud.DivisionCode,
        d.Division as DivisionName,
        COUNT(DISTINCT ud.UserID) as LideresAsignados,
        COUNT(*) as TotalAsignaciones,
        STUFF((SELECT ', ' + u2.NOMBRE 
               FROM UserDivisions ud2 
               INNER JOIN Usuarios u2 ON ud2.UserID = u2.ID
               WHERE ud2.DivisionCode = ud.DivisionCode 
                 {(string.IsNullOrEmpty(tienda) ? "" : "AND ud2.Tienda = @Tienda")}
                 AND ud2.IsActive = 1 
                 AND u2.IsActive = 1
               FOR XML PATH('')), 1, 2, '') as NombresLideres
    FROM UserDivisions ud
    INNER JOIN Usuarios u ON ud.UserID = u.ID
    LEFT JOIN (
        SELECT DISTINCT [Division Code] as DivisionCode, [Division]
        FROM [INNOVACENTRO].[dbo].[View_ProductosLI]
        WHERE [Division Code] IS NOT NULL AND [Division Code] != ''
    ) d ON ud.DivisionCode = d.DivisionCode
    WHERE ud.IsActive = 1 AND u.IsActive = 1 {(string.IsNullOrEmpty(tienda) ? "" : "AND ud.Tienda = @Tienda")}
    GROUP BY ud.DivisionCode, d.Division
    ORDER BY LideresAsignados DESC, ud.DivisionCode";

                using var command = new SqlCommand(assignmentStatsQuery, connection);
                command.Parameters.AddRange(parameters.ToArray());

                var divisionStats = new List<object>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    divisionStats.Add(new
                    {
                        DivisionCode = reader["DivisionCode"]?.ToString() ?? "",
                        DivisionName = reader["DivisionName"]?.ToString() ?? "",
                        LideresAsignados = Convert.ToInt32(reader["LideresAsignados"]),
                        TotalAsignaciones = Convert.ToInt32(reader["TotalAsignaciones"]),
                        NombresLideres = reader["NombresLideres"]?.ToString() ?? ""
                    });
                }
                reader.Close();

                // Obtener total de divisiones disponibles
                var allDivisionsResult = await GetDivisionsAsync();
                var totalDivisiones = allDivisionsResult.Data?.Count ?? 0;

                // Estadísticas de productos por división (desde INNOVACENTRO)
                using var innovaConnection = new SqlConnection(_innovacentroConnection);
                await innovaConnection.OpenAsync();

                const string productStatsQuery = @"
                    SELECT 
                        [Division Code] as DivisionCode,
                        [Division] as DivisionName,
                        COUNT(*) as ProductCount,
                        AVG(CAST([Unit Price] AS DECIMAL(18,2))) as AveragePrice,
                        AVG(CAST([Unit Cost (LCY)] AS DECIMAL(18,2))) as AverageCost,
                        COUNT(CASE WHEN [Blocked] = 0 THEN 1 END) as ActiveProducts,
                        COUNT(CASE WHEN [Blocked] = 1 THEN 1 END) as BlockedProducts
                    FROM [INNOVACENTRO].[dbo].[View_ProductosLI]
                    WHERE [Division Code] IS NOT NULL AND [Division Code] != ''
                    GROUP BY [Division Code], [Division]
                    ORDER BY ProductCount DESC";

                using var productCommand = new SqlCommand(productStatsQuery, innovaConnection);
                var productStats = new List<object>();
                using var productReader = await productCommand.ExecuteReaderAsync();

                while (await productReader.ReadAsync())
                {
                    productStats.Add(new
                    {
                        DivisionCode = productReader["DivisionCode"]?.ToString() ?? "",
                        DivisionName = productReader["DivisionName"]?.ToString() ?? "",
                        ProductCount = Convert.ToInt32(productReader["ProductCount"]),
                        ActiveProducts = Convert.ToInt32(productReader["ActiveProducts"]),
                        BlockedProducts = Convert.ToInt32(productReader["BlockedProducts"]),
                        AveragePrice = Convert.ToDecimal(productReader["AveragePrice"] ?? 0),
                        AverageCost = Convert.ToDecimal(productReader["AverageCost"] ?? 0)
                    });
                }

                var stats = new
                {
                    Tienda = tienda ?? "Todas",
                    TotalDivisiones = totalDivisiones,
                    DivisionesConAsignaciones = divisionStats.Count,
                    DivisionesConLideres = divisionStats,
                    DivisionesConProductos = productStats,
                    TotalLideresAsignados = divisionStats.Sum(d => (int)((dynamic)d).LideresAsignados),

                    // Resumen de asignaciones
                    ResumenAsignaciones = new
                    {
                        DivisionesConLideres = divisionStats.Count,
                        DivisionesSinLideres = Math.Max(0, totalDivisiones - divisionStats.Count),
                        PromedioLideresPorDivision = divisionStats.Any() ?
                            Math.Round(divisionStats.Average(d => (int)((dynamic)d).LideresAsignados), 2) : 0,
                        MaxLideresPorDivision = divisionStats.Any() ?
                            divisionStats.Max(d => (int)((dynamic)d).LideresAsignados) : 0
                    },

                    // Top divisiones
                    TopDivisionesPorLideres = divisionStats.Take(5).ToList(),
                    TopDivisionesPorProductos = productStats.Take(5).ToList(),

                    // Divisiones sin asignaciones
                    DivisionesSinAsignaciones = allDivisionsResult.Data?
    .Where(d => !divisionStats.Any(ds => ((dynamic)ds).DivisionCode == d.Codigo))
    .Select(d => new { d.Codigo, d.Nombre })
    .Cast<object>()
    .ToList() ?? new List<object>()
                };

                // Guardar en caché
                _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES));

                return new ApiResponse<object>
                {
                    Success = true,
                    Message = "Estadísticas de divisiones obtenidas exitosamente",
                    Data = stats
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de divisiones");
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = $"Error al obtener estadísticas: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<List<UserResponse>>> GetDivisionLeadersAsync(string divisionCode, string? tienda = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(divisionCode))
                {
                    return new ApiResponse<List<UserResponse>>
                    {
                        Success = false,
                        Message = "El código de división es requerido"
                    };
                }

                var cacheKey = $"division_leaders_{divisionCode}_{tienda ?? "all"}";
                if (_cache.TryGetValue(cacheKey, out List<UserResponse>? cachedLeaders))
                {
                    return new ApiResponse<List<UserResponse>>
                    {
                        Success = true,
                        Message = "Líderes de división obtenidos exitosamente (caché)",
                        Data = cachedLeaders!
                    };
                }

                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var whereClause = "WHERE ud.DivisionCode = @DivisionCode AND ud.IsActive = 1 AND u.IsActive = 1";
                var parameters = new List<SqlParameter>
                {
                    new SqlParameter("@DivisionCode", SqlDbType.NVarChar, 50) { Value = divisionCode }
                };

                if (!string.IsNullOrEmpty(tienda))
                {
                    whereClause += " AND ud.Tienda = @Tienda";
                    parameters.Add(new SqlParameter("@Tienda", SqlDbType.NVarChar, 50) { Value = tienda });
                }

                var query = $@"
                    SELECT DISTINCT 
                        u.ID, u.USUARIO, u.NOMBRE, u.EMAIL, u.PERFIL, u.TIENDA, u.AREA, 
                        u.IsActive, u.UltimoAcceso, u.FechaCreacion, u.FechaActualizacion,
                        ud.Tienda as TiendaAsignacion, 
                        ud.CreatedDate as FechaAsignacion,
                        ud.CreatedBy as AsignadoPor
                    FROM UserDivisions ud
                    INNER JOIN Usuarios u ON ud.UserID = u.ID
                    {whereClause}
                    ORDER BY u.NOMBRE";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddRange(parameters.ToArray());

                var leaders = new List<UserResponse>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    leaders.Add(new UserResponse
                    {
                        Id = Convert.ToInt32(reader["ID"]),
                        Usuario = reader["USUARIO"]?.ToString() ?? "",
                        Nombre = reader["NOMBRE"]?.ToString() ?? "",
                        Email = reader["EMAIL"]?.ToString() ?? "",
                        Perfil = Enum.Parse<UserProfile>(reader["PERFIL"]?.ToString() ?? "LIDER"),
                        Tienda = reader["TIENDA"]?.ToString() ?? "",
                        Area = reader["AREA"]?.ToString() ?? "",
                        Activo = Convert.ToBoolean(reader["IsActive"]),
                        UltimoAcceso = reader.IsDBNull("UltimoAcceso") ? null : Convert.ToDateTime(reader["UltimoAcceso"]),
                        FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                        FechaActualizacion = Convert.ToDateTime(reader["FechaActualizacion"])
                    });
                }

                // Guardar en caché
                _cache.Set(cacheKey, leaders, TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES));

                return new ApiResponse<List<UserResponse>>
                {
                    Success = true,
                    Message = "Líderes de división obtenidos exitosamente",
                    Data = leaders
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener líderes de división: {DivisionCode}", divisionCode);
                return new ApiResponse<List<UserResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener líderes: {ex.Message}",
                    Data = new List<UserResponse>()
                };
            }
        }

        public async Task<ApiResponse<DivisionResponse>> GetDivisionByCodeAsync(string divisionCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(divisionCode))
                {
                    return new ApiResponse<DivisionResponse>
                    {
                        Success = false,
                        Message = "El código de división es requerido"
                    };
                }

                var cacheKey = $"{CACHE_KEY_DIVISION_PREFIX}{divisionCode}";
                if (_cache.TryGetValue(cacheKey, out DivisionResponse? cachedDivision))
                {
                    return new ApiResponse<DivisionResponse>
                    {
                        Success = true,
                        Message = "División obtenida exitosamente (caché)",
                        Data = cachedDivision!
                    };
                }

                var allDivisionsResult = await GetDivisionsAsync();
                if (!allDivisionsResult.Success || allDivisionsResult.Data == null)
                {
                    return new ApiResponse<DivisionResponse>
                    {
                        Success = false,
                        Message = "Error al obtener divisiones"
                    };
                }

                var division = allDivisionsResult.Data.FirstOrDefault(d => d.Codigo == divisionCode);
                if (division == null)
                {
                    return new ApiResponse<DivisionResponse>
                    {
                        Success = false,
                        Message = "División no encontrada"
                    };
                }

                // Guardar en caché
                _cache.Set(cacheKey, division, TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES));

                return new ApiResponse<DivisionResponse>
                {
                    Success = true,
                    Message = "División obtenida exitosamente",
                    Data = division
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener división: {DivisionCode}", divisionCode);
                return new ApiResponse<DivisionResponse>
                {
                    Success = false,
                    Message = $"Error al obtener división: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<List<DivisionResponse>>> GetDivisionsWithAssignmentsAsync(string? tienda = null)
        {
            try
            {
                var divisionsResult = await GetDivisionsAsync();
                if (!divisionsResult.Success || divisionsResult.Data == null)
                {
                    return divisionsResult;
                }

                var statsResult = await GetDivisionStatsAsync(tienda);
                if (!statsResult.Success)
                {
                    return divisionsResult; // Devolver divisiones sin stats
                }

                var stats = (dynamic)statsResult.Data!;
                var divisionesConLideres = (List<object>)stats.DivisionesConLideres;

                // Enriquecer las divisiones con información de asignaciones
                foreach (var division in divisionsResult.Data)
                {
                    var divisionInfo = divisionesConLideres.FirstOrDefault(d => ((dynamic)d).DivisionCode == division.Codigo);
                    if (divisionInfo != null)
                    {
                        var info = (dynamic)divisionInfo;
                        division.Descripcion += $" - {info.LideresAsignados} líder(es) asignado(s)";
                    }
                    else
                    {
                        division.Descripcion += " - Sin líderes asignados";
                    }
                }

                return divisionsResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener divisiones con asignaciones");
                return new ApiResponse<List<DivisionResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener divisiones con asignaciones: {ex.Message}",
                    Data = new List<DivisionResponse>()
                };
            }
        }

        public async Task<ApiResponse<bool>> ValidateDivisionAccessAsync(int userId, string divisionCode, string tienda)
        {
            try
            {
                var userDivisions = await GetUserDivisionsAsync(userId, tienda);
                if (!userDivisions.Success)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Error al validar acceso a división",
                        Data = false
                    };
                }

                var hasAccess = userDivisions.Data!.Contains(divisionCode);

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = hasAccess ? "Usuario tiene acceso a la división" : "Usuario no tiene acceso a la división",
                    Data = hasAccess
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar acceso a división. Usuario: {UserId}, División: {DivisionCode}", userId, divisionCode);
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Error al validar acceso: {ex.Message}",
                    Data = false
                };
            }
        }

        public async Task<ApiResponse<object>> GetDivisionHierarchyAsync()
        {
            try
            {
                const string cacheKey = "division_hierarchy";
                if (_cache.TryGetValue(cacheKey, out object? cachedHierarchy))
                {
                    return new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Jerarquía de divisiones obtenida exitosamente (caché)",
                        Data = cachedHierarchy!
                    };
                }

                using var connection = new SqlConnection(_innovacentroConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        [Division Code] as DivisionCode,
                        [Division] as DivisionName,
                        [Item Category Code] as CategoryCode,
                        [Categoria] as CategoryName,
                        [Product Group Code] as GroupCode,
                        [Grupo] as GroupName,
                        [Codigo Subgrupo] as SubGroupCode,
                        [SubGrupo] as SubGroupName,
                        COUNT(*) as ProductCount
                    FROM [INNOVACENTRO].[dbo].[View_ProductosLI]
                    WHERE [Division Code] IS NOT NULL AND [Division Code] != ''
                        AND [Blocked] = 0
                    GROUP BY [Division Code], [Division], [Item Category Code], [Categoria],
                             [Product Group Code], [Grupo], [Codigo Subgrupo], [SubGrupo]
                    ORDER BY [Division], [Categoria], [Grupo], [SubGrupo]";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var hierarchy = new Dictionary<string, object>();

                while (await reader.ReadAsync())
                {
                    var divisionCode = reader["DivisionCode"]?.ToString() ?? "";
                    var divisionName = reader["DivisionName"]?.ToString() ?? "";
                    var categoryCode = reader["CategoryCode"]?.ToString() ?? "";
                    var categoryName = reader["CategoryName"]?.ToString() ?? "";
                    var groupCode = reader["GroupCode"]?.ToString() ?? "";
                    var groupName = reader["GroupName"]?.ToString() ?? "";
                    var subGroupCode = reader["SubGroupCode"]?.ToString() ?? "";
                    var subGroupName = reader["SubGroupName"]?.ToString() ?? "";
                    var productCount = Convert.ToInt32(reader["ProductCount"]);

                    if (!hierarchy.ContainsKey(divisionCode))
                    {
                        hierarchy[divisionCode] = new
                        {
                            Code = divisionCode,
                            Name = divisionName,
                            Categories = new Dictionary<string, object>()
                        };
                    }

                    var division = (dynamic)hierarchy[divisionCode];
                    var categories = (Dictionary<string, object>)division.Categories;

                    if (!string.IsNullOrEmpty(categoryCode) && !categories.ContainsKey(categoryCode))
                    {
                        categories[categoryCode] = new
                        {
                            Code = categoryCode,
                            Name = categoryName,
                            Groups = new Dictionary<string, object>()
                        };
                    }

                    // Continuar construyendo la jerarquía...
                }

                var result = new
                {
                    TotalDivisions = hierarchy.Count,
                    Hierarchy = hierarchy.Values.ToList(),
                    GeneratedAt = DateTime.UtcNow
                };

                // Guardar en caché por más tiempo ya que la jerarquía cambia poco
                _cache.Set(cacheKey, result, TimeSpan.FromHours(2));

                return new ApiResponse<object>
                {
                    Success = true,
                    Message = "Jerarquía de divisiones obtenida exitosamente",
                    Data = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener jerarquía de divisiones");
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = $"Error al obtener jerarquía: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<bool>> BulkAssignDivisionsAsync(List<AssignDivisionsRequest> assignments, int assignedBy)
        {
            try
            {
                if (assignments == null || !assignments.Any())
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "La lista de asignaciones no puede estar vacía"
                    };
                }

                var results = new List<(bool Success, string Message, AssignDivisionsRequest Request)>();

                foreach (var assignment in assignments)
                {
                    var result = await AssignDivisionsToLeaderAsync(assignment, assignedBy);
                    results.Add((result.Success, result.Message, assignment));
                }

                var successCount = results.Count(r => r.Success);
                var failCount = results.Count(r => !r.Success);

                return new ApiResponse<bool>
                {
                    Success = failCount == 0,
                    Message = $"Procesadas {assignments.Count} asignaciones. Exitosas: {successCount}, Fallidas: {failCount}",
                    Data = failCount == 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en asignación masiva de divisiones");
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Error en asignación masiva: {ex.Message}",
                    Data = false
                };
            }
        }

        public async Task ClearDivisionCacheAsync()
        {
            try
            {
                // Limpiar todas las entradas relacionadas con divisiones
                var keysToRemove = new[]
                {
                    CACHE_KEY_ALL_DIVISIONS,
                    "division_hierarchy",
                };

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                }

                // Limpiar caché con patrones
                await ClearCacheByPatternAsync("divisions_store_");
                await ClearCacheByPatternAsync("division_stats_");
                await ClearCacheByPatternAsync("division_leaders_");
                await ClearCacheByPatternAsync(CACHE_KEY_DIVISION_PREFIX);
                await ClearCacheByPatternAsync(CACHE_KEY_USER_DIVISIONS_PREFIX);

                _logger.LogInformation("Caché de divisiones limpiado exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar caché de divisiones");
            }
        }

        // Métodos privados auxiliares
        private async Task<List<string>> ValidateDivisionsExistAsync(List<string> divisionCodes)
        {
            try
            {
                using var connection = new SqlConnection(_innovacentroConnection);
                await connection.OpenAsync();

                var validDivisions = new List<string>();
                
                foreach (var divisionCode in divisionCodes)
                {
                    const string query = @"
                        SELECT COUNT(1) 
                        FROM [INNOVACENTRO].[dbo].[View_ProductosLI] 
                        WHERE [Division Code] = @DivisionCode AND [Blocked] = 0";

                    using var command = new SqlCommand(query, connection);
                    command.Parameters.Add("@DivisionCode", SqlDbType.NVarChar, 50).Value = divisionCode;

                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        validDivisions.Add(divisionCode);
                    }
                }

                return validDivisions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar divisiones");
                return new List<string>();
            }
        }

        private async Task<List<string>> GetUserDivisionsInternalAsync(int userId, string tienda, SqlConnection connection, SqlTransaction? transaction = null)
        {
            const string query = @"
                SELECT ud.DivisionCode
                FROM UserDivisions ud
                INNER JOIN Usuarios u ON ud.UserID = u.ID
                WHERE ud.UserID = @UserId AND ud.Tienda = @Tienda AND ud.IsActive = 1 AND u.IsActive = 1
                ORDER BY ud.DivisionCode";

            using var command = transaction != null ? 
                new SqlCommand(query, connection, transaction) : 
                new SqlCommand(query, connection);

            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            command.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

            var divisions = new List<string>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                divisions.Add(reader["DivisionCode"]?.ToString() ?? "");
            }

            return divisions;
        }

        private async Task LogDivisionActivityAsync(int targetUserId, int actionUserId, string action, string description,
                                                   string entityType, int? entityId, string? metadata,
                                                   SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                INSERT INTO UserActivity (UserId, Action, Description, EntityType, EntityId, Metadata, CreatedAt)
                VALUES (@UserId, @Action, @Description, @EntityType, @EntityId, @Metadata, GETDATE())";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = targetUserId;
            command.Parameters.Add("@Action", SqlDbType.NVarChar, 50).Value = action;
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = description;
            command.Parameters.Add("@EntityType", SqlDbType.NVarChar, 50).Value = entityType;
            command.Parameters.Add("@EntityId", SqlDbType.Int).Value = entityId ?? (object)DBNull.Value;
            command.Parameters.Add("@Metadata", SqlDbType.NVarChar).Value = metadata ?? (object)DBNull.Value;

            await command.ExecuteNonQueryAsync();
        }

        private async Task ClearUserDivisionCacheAsync(int userId, string tienda)
        {
            try
            {
                var cacheKey = $"{CACHE_KEY_USER_DIVISIONS_PREFIX}{userId}_{tienda}";
                _cache.Remove(cacheKey);
                
                // También limpiar caché relacionado
                await ClearCacheByPatternAsync($"divisions_store_{tienda}");
                await ClearCacheByPatternAsync("division_stats_");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar caché de usuario");
            }
        }

        private async Task ClearCacheByPatternAsync(string pattern)
        {
            try
            {
                // En una implementación real, necesitarías acceso a las claves del caché
                // Por ahora, esto es un placeholder
                await Task.CompletedTask;
                
                // Para implementación completa, podrías usar IMemoryCache con reflection
                // o cambiar a Redis que soporta patrones nativamente
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar caché por patrón: {Pattern}", pattern);
            }
        }
    }
}
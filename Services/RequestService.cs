using InventarioAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace InventarioAPI.Services
{
    public interface IRequestService
    {
        Task<ApiResponse<RequestResponse>> CreateRequestAsync(CreateRequestRequest request, int requestorId);
        Task<ApiResponse<PagedResponse<RequestResponse>>> GetRequestsAsync(GetRequestsRequest request);
        Task<ApiResponse<RequestResponse>> GetRequestByIdAsync(int requestId);
        Task<ApiResponse<RequestResponse>> GetRequestByTicketAsync(string ticketNumber);
        Task<ApiResponse<bool>> UpdateCodeStatusAsync(UpdateCodeStatusRequest request, int userId);
        Task<ApiResponse<bool>> AssignCodeAsync(AssignCodeRequest request, int userId);
        Task<ApiResponse<bool>> AddCommentAsync(AddCommentRequest request, int userId);
        Task<ApiResponse<List<RequestHistoryResponse>>> GetRequestHistoryAsync(int requestId);
        Task<ApiResponse<RequestDashboardResponse>> GetDashboardAsync(int userId, string tienda = "");
        Task<ApiResponse<List<RequestCodeResponse>>> GetMyAssignedCodesAsync(int userId);
        Task<ApiResponse<List<RequestResponse>>> GetRecentActivityAsync(int count = 20);
        Task<ApiResponse<List<RequestResponse>>> BulkCreateRequestsAsync(BulkCreateRequest request, int userId);
        Task<ApiResponse<List<UserResponse>>> GetTeamByStoreAsync(string tienda);
        Task<ApiResponse<List<RequestResponse>>> GetRequestsByStoreAsync(string tienda);
        Task<ApiResponse<List<RequestResponse>>> GetAllAdminRequestsAsync();
        Task<ApiResponse<List<RequestResponse>>> GetRequestsByDivisionsAsync(DivisionFilterRequest request);
Task<ApiResponse<int>> BulkAssignCodesAsync(BulkAssignCodesRequest request, int userId);
        Task<ApiResponse<int>> BulkUpdateStatusAsync(BulkUpdateStatusRequest request, int userId);
Task<ApiResponse<bool>> CloseRequestAsync(int requestId, int userId);





    }

    public class RequestService : IRequestService
    {
        private readonly IConfiguration _configuration;
        private readonly string _inventarioConnection;
        private readonly string _innovacentroConnection;
        private readonly string _connectionString;

        public RequestService(IConfiguration configuration)
        {
            _configuration = configuration;
            _inventarioConnection = _configuration.GetConnectionString("InventarioConnection")
                ?? throw new InvalidOperationException("InventarioConnection not found");
            _innovacentroConnection = _configuration.GetConnectionString("InnovacentroConnection")
                ?? throw new InvalidOperationException("InnovacentroConnection not found");
        }


public async Task<ApiResponse<bool>> CloseRequestAsync(int requestId, int userId)
{
    try
    {
        using var connection = new SqlConnection(_configuration.GetConnectionString("InventarioConnection"));
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        // Verificar si la solicitud existe
        const string checkQuery = @"SELECT Status FROM ProductRequests WHERE ID = @RequestID AND IsActive = 1";
        using var checkCommand = new SqlCommand(checkQuery, connection, transaction);
        checkCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;

        var status = await checkCommand.ExecuteScalarAsync();
        if (status == null)
        {
            return new ApiResponse<bool> { Success = false, Message = "Solicitud no encontrada o inactiva" };
        }

        // Marcar como cerrada (ej. CANCELADO o LISTO según contexto)
        const string updateRequestQuery = @"
            UPDATE ProductRequests 
            SET Status = 'LISTO', UpdatedBy = @UserID, UpdatedDate = GETDATE(), IsActive = 0
            WHERE ID = @RequestID";
        using var updateRequestCommand = new SqlCommand(updateRequestQuery, connection, transaction);
        updateRequestCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;
        updateRequestCommand.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
        await updateRequestCommand.ExecuteNonQueryAsync();

        // Opcional: también cerrar los códigos si aplica
        const string updateCodesQuery = @"
            UPDATE RequestCodes 
            SET Status = 'LISTO', UpdatedDate = GETDATE()
            WHERE RequestID = @RequestID AND Status != 'LISTO'";
        using var updateCodesCommand = new SqlCommand(updateCodesQuery, connection, transaction);
        updateCodesCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;
        await updateCodesCommand.ExecuteNonQueryAsync();

        // Agregar historial
        const string insertHistoryQuery = @"
            INSERT INTO RequestHistory (RequestID, UserID, UserName, Action, Comment, CreatedDate)
            VALUES (@RequestID, @UserID, @UserName, @Action, @Comment, GETDATE())";
        using var historyCommand = new SqlCommand(insertHistoryQuery, connection, transaction);
        historyCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;
        historyCommand.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
        historyCommand.Parameters.Add("@UserName", SqlDbType.NVarChar, 100).Value = "Sistema";
        historyCommand.Parameters.Add("@Action", SqlDbType.NVarChar, 50).Value = "COMPLETED";
        historyCommand.Parameters.Add("@Comment", SqlDbType.NVarChar, 500).Value = "Solicitud cerrada manualmente";
        await historyCommand.ExecuteNonQueryAsync();

        transaction.Commit();

        return new ApiResponse<bool>
        {
            Success = true,
            Message = "Solicitud cerrada exitosamente",
            Data = true
        };
    }
    catch (Exception ex)
    {
        return new ApiResponse<bool>
        {
            Success = false,
            Message = $"Error al cerrar solicitud: {ex.Message}",
            Data = false
        };
    }
}

public async Task<ApiResponse<List<UserResponse>>> GetTeamByStoreAsync(string tienda)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                // Obtener usuarios de la tienda con roles relevantes para conteos
                const string query = @"
                    SELECT 
                        u.ID, u.USUARIO, u.NOMBRE, u.EMAIL, u.PERFIL, 
                        u.TIENDA, u.AREA, u.IsActive, u.UltimoAcceso,
                        u.FechaCreacion, u.FechaActualizacion,
                        COUNT(DISTINCT rc.ID) as CodigosAsignados,
                        COUNT(DISTINCT CASE WHEN rc.Status = 'PENDIENTE' THEN rc.ID END) as CodigosPendientes
                    FROM Usuarios u
                    LEFT JOIN RequestCodes rc ON u.ID = rc.AssignedToID
                    WHERE u.TIENDA = @Tienda 
                        AND u.IsActive = 1
                        AND u.PERFIL IN ('LIDER', 'INVENTARIO', 'GERENTE_TIENDA')
                    GROUP BY u.ID, u.USUARIO, u.NOMBRE, u.EMAIL, u.PERFIL, 
                             u.TIENDA, u.AREA, u.IsActive, u.UltimoAcceso,
                             u.FechaCreacion, u.FechaActualizacion
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

                    users.Add(user);
                }
                reader.Close();

                // Obtener divisiones asignadas para líderes
                foreach (var user in users.Where(u => u.Perfil == UserProfile.LIDER))
                {
                    const string divisionQuery = @"
                        SELECT DivisionCode 
                        FROM UserDivisions 
                        WHERE UserID = @UserId AND Tienda = @Tienda AND IsActive = 1";

                    using var divCommand = new SqlCommand(divisionQuery, connection);
                    divCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;
                    divCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

                    using var divReader = await divCommand.ExecuteReaderAsync();
                    var divisions = new List<string>();

                    while (await divReader.ReadAsync())
                    {
                        var divisionCode = divReader["DivisionCode"]?.ToString();
                        if (!string.IsNullOrEmpty(divisionCode))
                        {
                            divisions.Add(divisionCode);
                        }
                    }

                    user.DivisionesAsignadas = divisions;
                }

                return new ApiResponse<List<UserResponse>>
                {
                    Success = true,
                    Message = $"Se encontraron {users.Count} usuarios del equipo en {tienda}",
                    Data = users
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<UserResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener equipo de la tienda: {ex.Message}",
                    Data = new List<UserResponse>()
                };
            }
        }

        public async Task<ApiResponse<int>> BulkAssignCodesAsync(BulkAssignCodesRequest request, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                var assignedUser = await GetUserInfoAsync(request.AssignedToID, connection, transaction);
                var currentUser = await GetUserInfoAsync(userId, connection, transaction);

                if (assignedUser == null)
                    return new ApiResponse<int> { Success = false, Message = "Usuario asignado no encontrado" };

                int count = 0;
                foreach (var codeId in request.CodeIDs)
                {
                    const string updateQuery = @"
                UPDATE RequestCodes 
                SET AssignedToID = @AssignedToID, AssignedToName = @AssignedToName,
                    Notes = @Notes, UpdatedDate = GETDATE()
                WHERE ID = @CodeID";

                    using var cmd = new SqlCommand(updateQuery, connection, transaction);
                    cmd.Parameters.AddWithValue("@AssignedToID", request.AssignedToID);
                    cmd.Parameters.AddWithValue("@AssignedToName", assignedUser.Usuario);
                    cmd.Parameters.AddWithValue("@Notes", request.Notes ?? "");
                    cmd.Parameters.AddWithValue("@CodeID", codeId);
                    count += await cmd.ExecuteNonQueryAsync();

                    await AddHistoryEntryAsync(
                        requestId: 0, // opcional si lo quieres cargar
                        codeId: codeId,
                        userId: userId,
                        userName: currentUser?.Usuario ?? "Usuario",
                        action: HistoryAction.ASSIGNED,
                        oldValue: null,
                        newValue: assignedUser.Usuario,
                        comment: $"Asignación masiva. {request.Notes}",
                        connection, transaction
                    );
                }

                transaction.Commit();
                return new ApiResponse<int>
                {
                    Success = true,
                    Message = $"Asignados {count} códigos exitosamente",
                    Data = count
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<int>
                {
                    Success = false,
                    Message = $"Error al asignar códigos: {ex.Message}",
                    Data = 0
                };
            }
        }
public async Task<ApiResponse<int>> BulkUpdateStatusAsync(BulkUpdateStatusRequest request, int userId)
{
    try
    {
        using var connection = new SqlConnection(_inventarioConnection);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var userInfo = await GetUserInfoAsync(userId, connection, transaction);

        int count = 0;
        foreach (var codeId in request.CodeIDs)
        {
            const string updateQuery = @"
                UPDATE RequestCodes 
                SET Status = @Status, Notes = @Notes, UpdatedDate = GETDATE()
                WHERE ID = @CodeID";

            using var cmd = new SqlCommand(updateQuery, connection, transaction);
            cmd.Parameters.AddWithValue("@Status", request.Status.ToString());
            cmd.Parameters.AddWithValue("@Notes", request.Notes ?? "");
            cmd.Parameters.AddWithValue("@CodeID", codeId);
            count += await cmd.ExecuteNonQueryAsync();

            await AddHistoryEntryAsync(
                requestId: 0,
                codeId: codeId,
                userId: userId,
                userName: userInfo?.Usuario ?? "Usuario",
                action: HistoryAction.STATUS_CHANGED,
                oldValue: null,
                newValue: request.Status.ToString(),
                comment: $"Cambio de estado masivo. {request.Notes}",
                connection, transaction
            );
        }

        transaction.Commit();
        return new ApiResponse<int>
        {
            Success = true,
            Message = $"Actualizados {count} códigos",
            Data = count
        };
    }
    catch (Exception ex)
    {
        return new ApiResponse<int>
        {
            Success = false,
            Message = $"Error al actualizar estados: {ex.Message}",
            Data = 0
        };
    }
}

public async Task<ApiResponse<List<RequestResponse>>> GetAllAdminRequestsAsync()
{
    try
    {
        using var connection = new SqlConnection(_inventarioConnection);
        await connection.OpenAsync();

        const string query = "SELECT * FROM ProductRequests WHERE IsActive = 1 ORDER BY CreatedDate DESC";

        using var command = new SqlCommand(query, connection);
        var list = new List<RequestResponse>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapToRequestResponse(reader));
        }

        return new ApiResponse<List<RequestResponse>>
        {
            Success = true,
            Message = "Solicitudes administrativas obtenidas correctamente",
            Data = list
        };
    }
    catch (Exception ex)
    {
        return new ApiResponse<List<RequestResponse>>
        {
            Success = false,
            Message = $"Error al obtener solicitudes: {ex.Message}"
        };
    }
}
public async Task<ApiResponse<List<RequestResponse>>> GetRequestsByDivisionsAsync(DivisionFilterRequest request)
{
    try
    {
        using var connection = new SqlConnection(_inventarioConnection);
        await connection.OpenAsync();

        var sql = @"
            SELECT DISTINCT pr.*
            FROM ProductRequests pr
            INNER JOIN RequestCodes rc ON rc.RequestID = pr.ID
            INNER JOIN InventoryCounts ic ON ic.CodeID = rc.ID
            WHERE pr.IsActive = 1";

        var conditions = new List<string>();
        var parameters = new List<SqlParameter>();

        if (request.DivisionCodes?.Any() == true)
        {
            var inClause = string.Join(",", request.DivisionCodes.Select((d, i) => $"@Division{i}"));
            conditions.Add($"ic.COD_DIVISION IN ({inClause})");
            for (int i = 0; i < request.DivisionCodes.Count; i++)
            {
                parameters.Add(new SqlParameter($"@Division{i}", request.DivisionCodes[i]));
            }
        }

        if (!string.IsNullOrEmpty(request.Tienda))
        {
            conditions.Add("pr.Tienda = @Tienda");
            parameters.Add(new SqlParameter("@Tienda", request.Tienda));
        }

        if (request.FromDate.HasValue)
        {
            conditions.Add("pr.CreatedDate >= @FromDate");
            parameters.Add(new SqlParameter("@FromDate", request.FromDate));
        }

        if (request.ToDate.HasValue)
        {
            conditions.Add("pr.CreatedDate <= @ToDate");
            parameters.Add(new SqlParameter("@ToDate", request.ToDate));
        }

        if (conditions.Any())
        {
            sql += " AND " + string.Join(" AND ", conditions);
        }

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        var results = new List<RequestResponse>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(MapToRequestResponse(reader));
        }

        return new ApiResponse<List<RequestResponse>>
        {
            Success = true,
            Message = "Solicitudes por división obtenidas",
            Data = results
        };
    }
    catch (Exception ex)
    {
        return new ApiResponse<List<RequestResponse>>
        {
            Success = false,
            Message = $"Error: {ex.Message}"
        };
    }
}

    
public async Task<ApiResponse<List<RequestResponse>>> GetRequestsByStoreAsync(string tienda)
{
    using var connection = new SqlConnection(_inventarioConnection);
    await connection.OpenAsync();

    const string query = "SELECT * FROM ProductRequests WHERE Tienda = @Tienda AND IsActive = 1 ORDER BY CreatedDate DESC";

    using var command = new SqlCommand(query, connection);
    command.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;

    var list = new List<RequestResponse>();
    using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(MapToRequestResponse(reader));
    }

    return new ApiResponse<List<RequestResponse>>
    {
        Success = true,
        Message = "Solicitudes obtenidas correctamente",
        Data = list
    };
}

        public async Task<ApiResponse<RequestResponse>> CreateRequestAsync(CreateRequestRequest request, int requestorId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Obtener información del solicitante
                    var requestorInfo = await GetUserInfoAsync(requestorId, connection, transaction);
                    if (requestorInfo == null)
                    {
                        return new ApiResponse<RequestResponse>
                        {
                            Success = false,
                            Message = "Usuario solicitante no encontrado"
                        };
                    }

                    // Generar número de ticket
                    var ticketNumber = await GenerateTicketNumberAsync(connection, transaction);

                    // Crear la solicitud principal
                    const string insertRequestQuery = @"
                        INSERT INTO ProductRequests (TicketNumber, RequestorID, RequestorName, RequestorEmail, 
                                                   Tienda, Priority, Description, TotalCodes, DueDate)
                        VALUES (@TicketNumber, @RequestorID, @RequestorName, @RequestorEmail, 
                                @Tienda, @Priority, @Description, @TotalCodes, @DueDate);
                        SELECT SCOPE_IDENTITY();";

                    using var requestCommand = new SqlCommand(insertRequestQuery, connection, transaction);
                    requestCommand.Parameters.Add("@TicketNumber", SqlDbType.NVarChar, 50).Value = ticketNumber;
                    requestCommand.Parameters.Add("@RequestorID", SqlDbType.Int).Value = requestorId;
                    requestCommand.Parameters.Add("@RequestorName", SqlDbType.NVarChar, 200).Value = requestorInfo.Usuario;
                    requestCommand.Parameters.Add("@RequestorEmail", SqlDbType.NVarChar, 200).Value = requestorInfo.Nombre ?? (object)DBNull.Value;
                    requestCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = request.Tienda;
                    requestCommand.Parameters.Add("@Priority", SqlDbType.NVarChar, 20).Value = request.Priority.ToString();
                    requestCommand.Parameters.Add("@Description", SqlDbType.NVarChar, 1000).Value = request.Description;
                    requestCommand.Parameters.Add("@TotalCodes", SqlDbType.Int).Value = request.ProductCodes.Count;
                    requestCommand.Parameters.Add("@DueDate", SqlDbType.DateTime).Value = request.DueDate ?? (object)DBNull.Value;

                    var requestId = Convert.ToInt32(await requestCommand.ExecuteScalarAsync());

                    // Insertar códigos individuales
                    const string insertCodeQuery = @"
                        INSERT INTO RequestCodes (RequestID, ProductCode)
                        VALUES (@RequestID, @ProductCode)";

                    foreach (var code in request.ProductCodes.Distinct())
                    {
                        using var codeCommand = new SqlCommand(insertCodeQuery, connection, transaction);
                        codeCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;
                        codeCommand.Parameters.Add("@ProductCode", SqlDbType.NVarChar, 50).Value = code.Trim().ToUpper();
                        await codeCommand.ExecuteNonQueryAsync();
                    }

                    // Asignar códigos automáticamente
                    await AssignCodesAutomaticallyAsync(requestId, connection, transaction);

                    // Registrar en historial
                    await AddHistoryEntryAsync(requestId, null, requestorId, requestorInfo.Usuario,
                                            HistoryAction.CREATED, null, null,
                                            $"Solicitud creada con {request.ProductCodes.Count} códigos",
                                            connection, transaction);

                    transaction.Commit();

                    // Obtener la solicitud creada
                    var createdRequest = await GetRequestByIdAsync(requestId);
                    return createdRequest;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<RequestResponse>
                {
                    Success = false,
                    Message = $"Error al crear la solicitud: {ex.Message}"
                };
            }
        }


        public async Task<ApiResponse<List<RequestResponse>>> GetRecentActivityAsync(int count = 20)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
            SELECT TOP (@Count) *
            FROM ProductRequests
            WHERE IsActive = 1
            ORDER BY CreatedDate DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@Count", SqlDbType.Int).Value = count;

                var list = new List<RequestResponse>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(MapToRequestResponse(reader));
                }

                return new ApiResponse<List<RequestResponse>>
                {
                    Success = true,
                    Message = "Actividad reciente obtenida exitosamente",
                    Data = list
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<RequestResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener actividad reciente: {ex.Message}"
                };
            }
        }

public async Task<ApiResponse<List<RequestResponse>>> BulkCreateRequestsAsync(BulkCreateRequest request, int userId)
{
    var responses = new List<RequestResponse>();

    foreach (var r in request.Requests)
    {
        var result = await CreateRequestAsync(r, userId);
        if (!result.Success)
        {
            return new ApiResponse<List<RequestResponse>>
            {
                Success = false,
                Message = $"Error al crear solicitud en el lote: {result.Message}"
            };
        }

        if (result.Data != null)
        {
            responses.Add(result.Data);
        }
    }

    return new ApiResponse<List<RequestResponse>>
    {
        Success = true,
        Message = $"Se crearon {responses.Count} solicitudes exitosamente",
        Data = responses
    };
}


        public async Task<ApiResponse<PagedResponse<RequestResponse>>> GetRequestsAsync(GetRequestsRequest request)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var whereConditions = new List<string> { "r.IsActive = 1" };
                var parameters = new List<SqlParameter>();

                // Aplicar filtros
                if (!string.IsNullOrEmpty(request.Tienda))
                {
                    whereConditions.Add("r.Tienda = @Tienda");
                    parameters.Add(new SqlParameter("@Tienda", SqlDbType.NVarChar, 50) { Value = request.Tienda });
                }

                if (request.Status.HasValue)
                {
                    whereConditions.Add("r.Status = @Status");
                    parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 20) { Value = request.Status.Value.ToString() });
                }

                if (request.Priority.HasValue)
                {
                    whereConditions.Add("r.Priority = @Priority");
                    parameters.Add(new SqlParameter("@Priority", SqlDbType.NVarChar, 20) { Value = request.Priority.Value.ToString() });
                }

                if (request.RequestorID.HasValue)
                {
                    whereConditions.Add("r.RequestorID = @RequestorID");
                    parameters.Add(new SqlParameter("@RequestorID", SqlDbType.Int) { Value = request.RequestorID.Value });
                }

                if (request.FromDate.HasValue)
                {
                    whereConditions.Add("r.CreatedDate >= @FromDate");
                    parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = request.FromDate.Value });
                }

                if (request.ToDate.HasValue)
                {
                    whereConditions.Add("r.CreatedDate <= @ToDate");
                    parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = request.ToDate.Value });
                }

                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    whereConditions.Add("(r.TicketNumber LIKE @SearchTerm OR r.RequestorName LIKE @SearchTerm OR r.Description LIKE @SearchTerm)");
                    parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 200) { Value = $"%{request.SearchTerm}%" });
                }

                var whereClause = string.Join(" AND ", whereConditions);

                // Consulta con paginación
                var query = $@"
    SELECT COUNT(*) FROM ProductRequests r WHERE {whereClause};
    
    SELECT * FROM (
        SELECT r.*, ROW_NUMBER() OVER (ORDER BY r.CreatedDate DESC) as RowNum
        FROM ProductRequests r 
        WHERE {whereClause}
    ) t 
    WHERE RowNum BETWEEN @StartRow AND @EndRow
    ORDER BY CreatedDate DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddRange(parameters.ToArray());
                command.Parameters.Add("@StartRow", SqlDbType.Int).Value = (request.PageNumber - 1) * request.PageSize + 1;
                command.Parameters.Add("@EndRow", SqlDbType.Int).Value = request.PageNumber * request.PageSize;

                using var reader = await command.ExecuteReaderAsync();

                // Leer total de registros
                await reader.ReadAsync();
                var totalRecords = Convert.ToInt32(reader[0]);

                await reader.NextResultAsync();

                var requests = new List<RequestResponse>();
                while (await reader.ReadAsync())
                {
                    requests.Add(MapToRequestResponse(reader));
                }

                var totalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize);

                var pagedResponse = new PagedResponse<RequestResponse>
                {
                    Data = requests,
                    TotalRecords = totalRecords,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    HasNextPage = request.PageNumber < totalPages,
                    HasPreviousPage = request.PageNumber > 1
                };

                return new ApiResponse<PagedResponse<RequestResponse>>
                {
                    Success = true,
                    Message = "Solicitudes obtenidas exitosamente",
                    Data = pagedResponse
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PagedResponse<RequestResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener solicitudes: {ex.Message}",
                    Data = new PagedResponse<RequestResponse>()
                };
            }
        }

        public async Task<ApiResponse<RequestResponse>> GetRequestByIdAsync(int requestId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string requestQuery = @"
                    SELECT * FROM ProductRequests WHERE ID = @RequestID AND IsActive = 1";

                using var requestCommand = new SqlCommand(requestQuery, connection);
                requestCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;

                using var reader = await requestCommand.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new ApiResponse<RequestResponse>
                    {
                        Success = false,
                        Message = "Solicitud no encontrada"
                    };
                }

                var request = MapToRequestResponse(reader);
                reader.Close();

                // Obtener códigos asociados
                const string codesQuery = @"
                    SELECT * FROM RequestCodes WHERE RequestID = @RequestID ORDER BY CreatedDate";

                using var codesCommand = new SqlCommand(codesQuery, connection);
                codesCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;

                using var codesReader = await codesCommand.ExecuteReaderAsync();
                while (await codesReader.ReadAsync())
                {
                    request.Codes.Add(MapToRequestCodeResponse(codesReader));
                }

                // Calcular estadísticas
                request.Stats = CalculateStats(request);

                return new ApiResponse<RequestResponse>
                {
                    Success = true,
                    Message = "Solicitud obtenida exitosamente",
                    Data = request
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<RequestResponse>
                {
                    Success = false,
                    Message = $"Error al obtener la solicitud: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<RequestResponse>> GetRequestByTicketAsync(string ticketNumber)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT ID FROM ProductRequests WHERE TicketNumber = @TicketNumber AND IsActive = 1";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@TicketNumber", SqlDbType.NVarChar, 50).Value = ticketNumber;

                var result = await command.ExecuteScalarAsync();
                if (result == null)
                {
                    return new ApiResponse<RequestResponse>
                    {
                        Success = false,
                        Message = "Ticket no encontrado"
                    };
                }

                var requestId = Convert.ToInt32(result);
                return await GetRequestByIdAsync(requestId);
            }
            catch (Exception ex)
            {
                return new ApiResponse<RequestResponse>
                {
                    Success = false,
                    Message = $"Error al obtener el ticket: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<bool>> UpdateCodeStatusAsync(UpdateCodeStatusRequest request, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Obtener información actual del código
                    const string getCurrentStatusQuery = @"
                        SELECT rc.Status, rc.RequestID, rc.ProductCode, pr.TicketNumber
                        FROM RequestCodes rc
                        INNER JOIN ProductRequests pr ON rc.RequestID = pr.ID
                        WHERE rc.ID = @CodeID";

                    using var getCurrentCommand = new SqlCommand(getCurrentStatusQuery, connection, transaction);
                    getCurrentCommand.Parameters.Add("@CodeID", SqlDbType.Int).Value = request.CodeID;

                    using var reader = await getCurrentCommand.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "Código no encontrado"
                        };
                    }

                    var oldStatus = reader["Status"]?.ToString() ?? "";
                    var requestId = Convert.ToInt32(reader["RequestID"]);
                    var productCode = reader["ProductCode"]?.ToString() ?? "";
                    reader.Close();

                    // Actualizar estado del código
                    const string updateQuery = @"
                        UPDATE RequestCodes 
                        SET Status = @Status, Notes = @Notes, UpdatedDate = GETDATE(),
                            ProcessedDate = CASE WHEN @Status IN ('LISTO', 'AJUSTADO') THEN GETDATE() ELSE ProcessedDate END
                        WHERE ID = @CodeID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add("@CodeID", SqlDbType.Int).Value = request.CodeID;
                    updateCommand.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = request.Status.ToString();
                    updateCommand.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value = request.Notes;

                    await updateCommand.ExecuteNonQueryAsync();

                    // Obtener información del usuario
                    var userInfo = await GetUserInfoAsync(userId, connection, transaction);

                    // Registrar en historial
                    await AddHistoryEntryAsync(requestId, request.CodeID, userId, userInfo?.Usuario ?? "Usuario",
                                            HistoryAction.STATUS_CHANGED, oldStatus, request.Status.ToString(),
                                            $"Estado del código {productCode} cambiado. Notas: {request.Notes}",
                                            connection, transaction);

                    transaction.Commit();

                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Message = "Estado actualizado exitosamente",
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
                    Message = $"Error al actualizar estado: {ex.Message}",
                    Data = false
                };
            }
        }

        public async Task<ApiResponse<bool>> AssignCodeAsync(AssignCodeRequest request, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Obtener información del usuario asignado
                    var assignedUserInfo = await GetUserInfoAsync(request.AssignedToID, connection, transaction);
                    if (assignedUserInfo == null)
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "Usuario asignado no encontrado"
                        };
                    }

                    // Obtener información del código
                    const string getCodeQuery = @"
                        SELECT RequestID, ProductCode FROM RequestCodes WHERE ID = @CodeID";

                    using var getCodeCommand = new SqlCommand(getCodeQuery, connection, transaction);
                    getCodeCommand.Parameters.Add("@CodeID", SqlDbType.Int).Value = request.CodeID;

                    using var reader = await getCodeCommand.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "Código no encontrado"
                        };
                    }

                    var requestId = Convert.ToInt32(reader["RequestID"]);
                    var productCode = reader["ProductCode"]?.ToString() ?? "";
                    reader.Close();

                    // Actualizar asignación
                    const string updateQuery = @"
                        UPDATE RequestCodes 
                        SET AssignedToID = @AssignedToID, AssignedToName = @AssignedToName, 
                            Notes = @Notes, UpdatedDate = GETDATE()
                        WHERE ID = @CodeID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add("@CodeID", SqlDbType.Int).Value = request.CodeID;
                    updateCommand.Parameters.Add("@AssignedToID", SqlDbType.Int).Value = request.AssignedToID;
                    updateCommand.Parameters.Add("@AssignedToName", SqlDbType.NVarChar, 200).Value = assignedUserInfo.Usuario;
                    updateCommand.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value = request.Notes;

                    await updateCommand.ExecuteNonQueryAsync();

                    // Obtener información del usuario que asigna
                    var currentUserInfo = await GetUserInfoAsync(userId, connection, transaction);

                    // Registrar en historial
                    await AddHistoryEntryAsync(requestId, request.CodeID, userId, currentUserInfo?.Usuario ?? "Usuario",
                                            HistoryAction.ASSIGNED, null, assignedUserInfo.Usuario,
                                            $"Código {productCode} asignado manualmente. Notas: {request.Notes}",
                                            connection, transaction);

                    transaction.Commit();

                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Message = "Código asignado exitosamente",
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
                    Message = $"Error al asignar código: {ex.Message}",
                    Data = false
                };
            }
        }

        public async Task<ApiResponse<bool>> AddCommentAsync(AddCommentRequest request, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var userInfo = await GetUserInfoAsync(userId, connection);

                await AddHistoryEntryAsync(request.RequestID, request.CodeID, userId,
                                        userInfo?.Usuario ?? "Usuario", HistoryAction.COMMENT,
                                        null, null, request.Comment, connection);

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Comentario agregado exitosamente",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Error al agregar comentario: {ex.Message}",
                    Data = false
                };
            }
        }

        public async Task<ApiResponse<List<RequestHistoryResponse>>> GetRequestHistoryAsync(int requestId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT rh.*, rc.ProductCode
                    FROM RequestHistory rh
                    LEFT JOIN RequestCodes rc ON rh.CodeID = rc.ID
                    WHERE rh.RequestID = @RequestID
                    ORDER BY rh.CreatedDate DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;

                var history = new List<RequestHistoryResponse>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    history.Add(new RequestHistoryResponse
                    {
                        ID = Convert.ToInt32(reader["ID"]),
                        RequestID = Convert.ToInt32(reader["RequestID"]),
                        CodeID = reader.IsDBNull("CodeID") ? null : Convert.ToInt32(reader["CodeID"]),
                        ProductCode = reader["ProductCode"]?.ToString() ?? "",
                        UserID = Convert.ToInt32(reader["UserID"]),
                        UserName = reader["UserName"]?.ToString() ?? "",
                        Action = Enum.Parse<HistoryAction>(reader["Action"]?.ToString() ?? "COMMENT"),
                        OldValue = reader["OldValue"]?.ToString() ?? "",
                        NewValue = reader["NewValue"]?.ToString() ?? "",
                        Comment = reader["Comment"]?.ToString() ?? "",
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                    });
                }

                return new ApiResponse<List<RequestHistoryResponse>>
                {
                    Success = true,
                    Message = "Historial obtenido exitosamente",
                    Data = history
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<RequestHistoryResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener historial: {ex.Message}",
                    Data = new List<RequestHistoryResponse>()
                };
            }
        }

        public async Task<ApiResponse<RequestDashboardResponse>> GetDashboardAsync(int userId, string tienda = "")
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var dashboard = new RequestDashboardResponse();

                // Estadísticas generales
                var statsQuery = @"
                    SELECT 
                        COUNT(*) as TotalRequests,
                        COUNT(CASE WHEN Status = 'PENDIENTE' THEN 1 END) as PendingRequests,
                        COUNT(CASE WHEN Status = 'EN_REVISION' THEN 1 END) as InReviewRequests,
                        COUNT(CASE WHEN Status IN ('LISTO', 'AJUSTADO') THEN 1 END) as CompletedRequests,
                        COUNT(CASE WHEN DueDate < GETDATE() AND Status NOT IN ('LISTO', 'AJUSTADO') THEN 1 END) as OverdueRequests
                    FROM ProductRequests 
                    WHERE IsActive = 1" + (string.IsNullOrEmpty(tienda) ? "" : " AND Tienda = @Tienda");

                using var statsCommand = new SqlCommand(statsQuery, connection);
                if (!string.IsNullOrEmpty(tienda))
                {
                    statsCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;
                }

                using var statsReader = await statsCommand.ExecuteReaderAsync();
                if (await statsReader.ReadAsync())
                {
                    dashboard.TotalRequests = Convert.ToInt32(statsReader["TotalRequests"]);
                    dashboard.PendingRequests = Convert.ToInt32(statsReader["PendingRequests"]);
                    dashboard.InReviewRequests = Convert.ToInt32(statsReader["InReviewRequests"]);
                    dashboard.CompletedRequests = Convert.ToInt32(statsReader["CompletedRequests"]);
                    dashboard.OverdueRequests = Convert.ToInt32(statsReader["OverdueRequests"]);
                }
                statsReader.Close();

                // Códigos asignados al usuario
                const string myCodesQuery = @"
                    SELECT 
                        COUNT(*) as MyAssignedCodes,
                        COUNT(CASE WHEN Status = 'PENDIENTE' THEN 1 END) as MyPendingCodes
                    FROM RequestCodes 
                    WHERE AssignedToID = @UserID";

                using var myCodesCommand = new SqlCommand(myCodesQuery, connection);
                myCodesCommand.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;

                using var myCodesReader = await myCodesCommand.ExecuteReaderAsync();
                if (await myCodesReader.ReadAsync())
                {
                    dashboard.MyAssignedCodes = Convert.ToInt32(myCodesReader["MyAssignedCodes"]);
                    dashboard.MyPendingCodes = Convert.ToInt32(myCodesReader["MyPendingCodes"]);
                }

                return new ApiResponse<RequestDashboardResponse>
                {
                    Success = true,
                    Message = "Dashboard obtenido exitosamente",
                    Data = dashboard
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<RequestDashboardResponse>
                {
                    Success = false,
                    Message = $"Error al obtener dashboard: {ex.Message}",
                    Data = new RequestDashboardResponse()
                };
            }
        }

        public async Task<ApiResponse<List<RequestCodeResponse>>> GetMyAssignedCodesAsync(int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT rc.*, pr.TicketNumber, pr.Tienda, pr.RequestorName
                    FROM RequestCodes rc
                    INNER JOIN ProductRequests pr ON rc.RequestID = pr.ID
                    WHERE rc.AssignedToID = @UserID AND pr.IsActive = 1
                    ORDER BY rc.Status, rc.CreatedDate";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;

                var codes = new List<RequestCodeResponse>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    codes.Add(MapToRequestCodeResponse(reader));
                }

                return new ApiResponse<List<RequestCodeResponse>>
                {
                    Success = true,
                    Message = "Códigos asignados obtenidos exitosamente",
                    Data = codes
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<RequestCodeResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener códigos asignados: {ex.Message}",
                    Data = new List<RequestCodeResponse>()
                };
            }
        }




        // Métodos auxiliares privados
        private async Task<string> GenerateTicketNumberAsync(SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                SELECT ISNULL(MAX(CAST(RIGHT(TicketNumber, 4) AS INT)), 0) + 1
                FROM ProductRequests
                WHERE TicketNumber LIKE @Pattern";

            var datePattern = $"REQ-{DateTime.Now:yyyyMMdd}-%";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add("@Pattern", SqlDbType.NVarChar, 50).Value = datePattern;

            var sequence = Convert.ToInt32(await command.ExecuteScalarAsync());
            return $"REQ-{DateTime.Now:yyyyMMdd}-{sequence:0000}";
        }

        private async Task<UserInfo?> GetUserInfoAsync(int userId, SqlConnection connection, SqlTransaction? transaction = null)
        {
            const string query = "SELECT ID, USUARIO, NOMBRE, TIENDA FROM Usuarios WHERE ID = @UserID";

            using var command = transaction != null ?
                new SqlCommand(query, connection, transaction) :
                new SqlCommand(query, connection);

            command.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new UserInfo
                {
                    Id = Convert.ToInt32(reader["ID"]),
                    Usuario = reader["USUARIO"]?.ToString() ?? "",
                    Nombre = reader["NOMBRE"]?.ToString() ?? "",
                    Tienda = reader["TIENDA"]?.ToString() ?? ""
                };
            }
            return null;
        }

        private async Task AssignCodesAutomaticallyAsync(int requestId, SqlConnection connection, SqlTransaction transaction)
        {
            const string assignQuery = @"EXEC sp_AssignCodesToResponsible @RequestID";

            using var command = new SqlCommand(assignQuery, connection, transaction);
            command.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;
            await command.ExecuteNonQueryAsync();
        }

        private async Task AddHistoryEntryAsync(int requestId, int? codeId, int userId, string userName,
                                             HistoryAction action, string? oldValue, string? newValue,
                                             string? comment, SqlConnection connection, SqlTransaction? transaction = null)
        {
            const string query = @"
                INSERT INTO RequestHistory (RequestID, CodeID, UserID, UserName, Action, OldValue, NewValue, Comment)
                VALUES (@RequestID, @CodeID, @UserID, @UserName, @Action, @OldValue, @NewValue, @Comment)";

            using var command = transaction != null ?
                new SqlCommand(query, connection, transaction) :
                new SqlCommand(query, connection);

            command.Parameters.Add("@RequestID", SqlDbType.Int).Value = requestId;

            // CORRECCIÓN ESPECÍFICA PARA EL ERROR CS1503:
            if (codeId.HasValue)
            {
                command.Parameters.Add("@CodeID", SqlDbType.Int).Value = codeId.Value;
            }
            else
            {
                command.Parameters.Add("@CodeID", SqlDbType.Int).Value = DBNull.Value;
            }

            command.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
            command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = userName;
            command.Parameters.Add("@Action", SqlDbType.NVarChar, 50).Value = action.ToString();
            command.Parameters.Add("@OldValue", SqlDbType.NVarChar, 500).Value = oldValue ?? (object)DBNull.Value;
            command.Parameters.Add("@NewValue", SqlDbType.NVarChar, 500).Value = newValue ?? (object)DBNull.Value;
            command.Parameters.Add("@Comment", SqlDbType.NVarChar, 1000).Value = comment ?? (object)DBNull.Value;

            await command.ExecuteNonQueryAsync();
        }

        private RequestResponse MapToRequestResponse(SqlDataReader reader)
        {
            return new RequestResponse
            {
                ID = Convert.ToInt32(reader["ID"]),
                TicketNumber = reader["TicketNumber"]?.ToString() ?? "",
                RequestorID = Convert.ToInt32(reader["RequestorID"]),
                RequestorName = reader["RequestorName"]?.ToString() ?? "",
                RequestorEmail = reader["RequestorEmail"]?.ToString() ?? "",
                Tienda = reader["Tienda"]?.ToString() ?? "",
                Status = Enum.Parse<RequestStatus>(reader["Status"]?.ToString() ?? "PENDIENTE"),
                Priority = Enum.Parse<RequestPriority>(reader["Priority"]?.ToString() ?? "NORMAL"),
                Description = reader["Description"]?.ToString() ?? "",
                TotalCodes = reader.IsDBNull("TotalCodes") ? 0 : Convert.ToInt32(reader["TotalCodes"]),
                CompletedCodes = reader.IsDBNull("CompletedCodes") ? 0 : Convert.ToInt32(reader["CompletedCodes"]),
                PendingCodes = reader.IsDBNull("PendingCodes") ? 0 : Convert.ToInt32(reader["PendingCodes"]),
                CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                UpdatedDate = Convert.ToDateTime(reader["UpdatedDate"]),
                DueDate = reader.IsDBNull("DueDate") ? null : Convert.ToDateTime(reader["DueDate"]),
                CompletedDate = reader.IsDBNull("CompletedDate") ? null : Convert.ToDateTime(reader["CompletedDate"]),
                IsActive = Convert.ToBoolean(reader["IsActive"])
            };
        }

        private RequestCodeResponse MapToRequestCodeResponse(SqlDataReader reader)
        {
            return new RequestCodeResponse
            {
                ID = Convert.ToInt32(reader["ID"]),
                RequestID = Convert.ToInt32(reader["RequestID"]),
                ProductCode = reader["ProductCode"]?.ToString() ?? "",
                Status = Enum.Parse<RequestStatus>(reader["Status"]?.ToString() ?? "PENDIENTE"),
                AssignedToID = reader.IsDBNull("AssignedToID") ? null : Convert.ToInt32(reader["AssignedToID"]),
                AssignedToName = reader["AssignedToName"]?.ToString() ?? "",
                AssignmentType = reader["AssignmentType"]?.ToString() ?? "",
                AssignmentInfo = reader["AssignmentInfo"]?.ToString() ?? "",
                Notes = reader["Notes"]?.ToString() ?? "",
                ProcessedDate = reader.IsDBNull("ProcessedDate") ? null : Convert.ToDateTime(reader["ProcessedDate"]),
                CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                UpdatedDate = Convert.ToDateTime(reader["UpdatedDate"])
            };
        }

        private RequestStatsInfo CalculateStats(RequestResponse request)
        {
            var stats = new RequestStatsInfo();

            if (request.Codes.Any())
            {
                stats.PendingCodes = request.Codes.Count(c => c.Status == RequestStatus.PENDIENTE);
                stats.InReviewCodes = request.Codes.Count(c => c.Status == RequestStatus.EN_REVISION);
                stats.ReadyCodes = request.Codes.Count(c => c.Status == RequestStatus.LISTO);
                stats.AdjustedCodes = request.Codes.Count(c => c.Status == RequestStatus.AJUSTADO);

                var completedCodes = stats.ReadyCodes + stats.AdjustedCodes;
                stats.CompletionPercentage = request.TotalCodes > 0 ?
                    Math.Round((decimal)completedCodes / request.TotalCodes * 100, 2) : 0;
            }

            stats.DaysOpen = (DateTime.Now - request.CreatedDate).Days;
            stats.IsOverdue = request.DueDate.HasValue && DateTime.Now > request.DueDate.Value &&
                             request.Status != RequestStatus.AJUSTADO;

            return stats;
        }
    }
}
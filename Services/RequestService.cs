using InventarioAPI.Models;
using Microsoft.Data.SqlClient;
using System.Data;

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
    }

    public class RequestService : IRequestService
    {
        private readonly IConfiguration _configuration;
        private readonly string _inventarioConnection;
        private readonly string _innovacentroConnection;

        public RequestService(IConfiguration configuration)
        {
            _configuration = configuration;
            _inventarioConnection = _configuration.GetConnectionString("InventarioConnection") 
                ?? throw new InvalidOperationException("InventarioConnection not found");
            _innovacentroConnection = _configuration.GetConnectionString("InnovacentroConnection") 
                ?? throw new InvalidOperationException("InnovacentroConnection not found");
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
                    
                    SELECT r.*, 
                           ROW_NUMBER() OVER (ORDER BY r.CreatedDate DESC) as RowNum
                    FROM ProductRequests r 
                    WHERE {whereClause}
                    ORDER BY r.CreatedDate DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddRange(parameters.ToArray());
                command.Parameters.Add("@Offset", SqlDbType.Int).Value = (request.PageNumber - 1) * request.PageSize;
                command.Parameters.Add("@PageSize", SqlDbType.Int).Value = request.PageSize;

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
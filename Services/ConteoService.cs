using InventarioAPI.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace InventarioAPI.Services
{
    public interface IInventoryCountService
    {
        Task<ApiResponse<List<int>>> CreateInventoryCountsAsync(CreateInventoryCountsRequest request, int userId);
        Task<ApiResponse<InventoryCountResponse>> RegisterPhysicalCountAsync(RegisterPhysicalCountRequest request, int userId);
        Task<ApiResponse<bool>> UpdateCountStatusAsync(UpdateCountStatusRequest request, int userId);
        Task<ApiResponse<PagedResponse<InventoryCountResponse>>> GetInventoryCountsAsync(GetInventoryCountsRequest request);
        Task<ApiResponse<InventoryCountResponse>> GetInventoryCountByIdAsync(int countId);
        Task<ApiResponse<List<InventoryCountHistoryResponse>>> GetCountHistoryAsync(int countId);
        Task<ApiResponse<InventoryCountDashboardResponse>> GetCountDashboardAsync(string tienda = "");
        Task<ApiResponse<List<InventoryCountSummaryResponse>>> GetMyAssignedCountsAsync(int userId);
        Task<ApiResponse<bool>> AddCountCommentAsync(AddCountCommentRequest request, int userId);
        Task<ApiResponse<object>> BatchUpdateCountsAsync(BatchUpdateCountsRequest request, int userId);
        Task<ApiResponse<List<InventoryCountResponse>>> GetPendingCountsByRequestAsync(int requestId);
    }

    public class InventoryCountService : IInventoryCountService
    {
        private readonly IConfiguration _configuration;
        private readonly string _inventarioConnection;
        private readonly string _innovacentroConnection;

        public InventoryCountService(IConfiguration configuration)
        {
            _configuration = configuration;
            _inventarioConnection = _configuration.GetConnectionString("InventarioConnection") 
                ?? throw new InvalidOperationException("InventarioConnection not found");
            _innovacentroConnection = _configuration.GetConnectionString("InnovacentroConnection") 
                ?? throw new InvalidOperationException("InnovacentroConnection not found");
        }

        public async Task<ApiResponse<List<int>>> CreateInventoryCountsAsync(CreateInventoryCountsRequest request, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Verificar que la solicitud existe y está activa
                    const string checkRequestQuery = @"
                        SELECT COUNT(1) FROM ProductRequests 
                        WHERE ID = @RequestID AND IsActive = 1";

                    using var checkCommand = new SqlCommand(checkRequestQuery, connection, transaction);
                    checkCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = request.RequestID;

                    var requestExists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                    if (!requestExists)
                    {
                        return new ApiResponse<List<int>>
                        {
                            Success = false,
                            Message = "La solicitud no existe o no está activa"
                        };
                    }

                    // Ejecutar stored procedure para crear conteos
                    const string createCountsQuery = @"EXEC sp_CreateInventoryCountsFromCodes @RequestID, @CreatedBy";

                    using var createCommand = new SqlCommand(createCountsQuery, connection, transaction);
                    createCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = request.RequestID;
                    createCommand.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = userId;

                    await createCommand.ExecuteNonQueryAsync();

                    // Obtener los IDs de los conteos creados
                    const string getCreatedCountsQuery = @"
                        SELECT ID FROM InventoryCounts 
                        WHERE RequestID = @RequestID AND CreatedBy = @CreatedBy 
                        AND CreatedDate >= DATEADD(MINUTE, -5, GETDATE())";

                    using var getCountsCommand = new SqlCommand(getCreatedCountsQuery, connection, transaction);
                    getCountsCommand.Parameters.Add("@RequestID", SqlDbType.Int).Value = request.RequestID;
                    getCountsCommand.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = userId;

                    var countIds = new List<int>();
                    using var reader = await getCountsCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        countIds.Add(Convert.ToInt32(reader["ID"]));
                    }

                    transaction.Commit();

                    return new ApiResponse<List<int>>
                    {
                        Success = true,
                        Message = $"Se crearon {countIds.Count} conteos exitosamente",
                        Data = countIds
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
                return new ApiResponse<List<int>>
                {
                    Success = false,
                    Message = $"Error al crear conteos: {ex.Message}",
                    Data = new List<int>()
                };
            }
        }

        public async Task<ApiResponse<InventoryCountResponse>> RegisterPhysicalCountAsync(RegisterPhysicalCountRequest request, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Obtener información actual del conteo
                    const string getCurrentInfoQuery = @"
                        SELECT CANTIDAD_FISICA, COMENTARIO, ESTADO 
                        FROM InventoryCounts 
                        WHERE ID = @CountID AND IsActive = 1";

                    using var getCurrentCommand = new SqlCommand(getCurrentInfoQuery, connection, transaction);
                    getCurrentCommand.Parameters.Add("@CountID", SqlDbType.Int).Value = request.CountID;

                    using var reader = await getCurrentCommand.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return new ApiResponse<InventoryCountResponse>
                        {
                            Success = false,
                            Message = "Conteo no encontrado"
                        };
                    }

                    var oldCantidad = reader["CANTIDAD_FISICA"] as decimal?;
                    var oldComentario = reader["COMENTARIO"]?.ToString() ?? "";
                    reader.Close();

                    // Actualizar el conteo físico
                    const string updateQuery = @"
                        UPDATE InventoryCounts 
                        SET CANTIDAD_FISICA = @CantidadFisica, 
                            COMENTARIO = @Comentario,
                            ESTATUS_CODIGO = 'CONTADO',
                            UpdatedBy = @UserId,
                            UpdatedDate = GETDATE()
                        WHERE ID = @CountID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add("@CountID", SqlDbType.Int).Value = request.CountID;
                    updateCommand.Parameters.Add("@CantidadFisica", SqlDbType.Decimal).Value = request.CantidadFisica;
                    updateCommand.Parameters.Add("@Comentario", SqlDbType.NVarChar, 500).Value = request.Comentario ?? (object)DBNull.Value;
                    updateCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                    await updateCommand.ExecuteNonQueryAsync();

                    // Obtener información del usuario
                    var userInfo = await GetUserInfoAsync(userId, connection, transaction);

                    // Registrar en historial
                    await AddCountHistoryEntryAsync(request.CountID, userId, userInfo?.Usuario ?? "Usuario", 
                                                 CountAction.COUNTED, 
                                                 oldCantidad?.ToString() ?? "null", 
                                                 request.CantidadFisica.ToString(),
                                                 $"Cantidad física registrada. Comentario: {request.Comentario}", 
                                                 connection, transaction);

                    transaction.Commit();

                    // Obtener el conteo actualizado
                    var updatedCount = await GetInventoryCountByIdAsync(request.CountID);
                    return updatedCount;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<InventoryCountResponse>
                {
                    Success = false,
                    Message = $"Error al registrar conteo físico: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<bool>> UpdateCountStatusAsync(UpdateCountStatusRequest request, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Obtener estado actual
                    const string getCurrentStatusQuery = @"
                        SELECT ESTADO FROM InventoryCounts 
                        WHERE ID = @CountID AND IsActive = 1";

                    using var getCurrentCommand = new SqlCommand(getCurrentStatusQuery, connection, transaction);
                    getCurrentCommand.Parameters.Add("@CountID", SqlDbType.Int).Value = request.CountID;

                    var currentStatusResult = await getCurrentCommand.ExecuteScalarAsync();
                    var currentStatus = currentStatusResult?.ToString();
                    if (currentStatus == null)
                    {
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = "Conteo no encontrado"
                        };
                    }

                    // Actualizar estado
                    const string updateQuery = @"
                        UPDATE InventoryCounts 
                        SET ESTADO = @Estado, 
                            COMENTARIO = CASE 
                                WHEN @Comentario IS NOT NULL AND @Comentario != '' 
                                THEN @Comentario 
                                ELSE COMENTARIO 
                            END,
                            UpdatedBy = @UserId,
                            UpdatedDate = GETDATE()
                        WHERE ID = @CountID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add("@CountID", SqlDbType.Int).Value = request.CountID;
                    updateCommand.Parameters.Add("@Estado", SqlDbType.NVarChar, 20).Value = request.Estado.ToString();
                    updateCommand.Parameters.Add("@Comentario", SqlDbType.NVarChar, 500).Value = request.Comentario ?? (object)DBNull.Value;
                    updateCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                    await updateCommand.ExecuteNonQueryAsync();

                    // Actualizar estado del código asociado
                    const string updateCodeQuery = @"EXEC sp_UpdateCodeStatusFromCount @CountID, @UserId";
                    using var updateCodeCommand = new SqlCommand(updateCodeQuery, connection, transaction);
                    updateCodeCommand.Parameters.Add("@CountID", SqlDbType.Int).Value = request.CountID;
                    updateCodeCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    await updateCodeCommand.ExecuteNonQueryAsync();

                    // Obtener información del usuario
                    var userInfo = await GetUserInfoAsync(userId, connection, transaction);

                    // Registrar en historial
                    await AddCountHistoryEntryAsync(request.CountID, userId, userInfo?.Usuario ?? "Usuario", 
                                                 CountAction.STATUS_CHANGED, currentStatus, request.Estado.ToString(),
                                                 $"Estado cambiado. Comentario: {request.Comentario}", 
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

        public async Task<ApiResponse<PagedResponse<InventoryCountResponse>>> GetInventoryCountsAsync(GetInventoryCountsRequest request)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var whereConditions = new List<string> { "ic.IsActive = 1" };
                var parameters = new List<SqlParameter>();

                // Aplicar filtros
                if (request.RequestID.HasValue)
                {
                    whereConditions.Add("ic.RequestID = @RequestID");
                    parameters.Add(new SqlParameter("@RequestID", SqlDbType.Int) { Value = request.RequestID.Value });
                }

                if (!string.IsNullOrEmpty(request.Tienda))
                {
                    whereConditions.Add("ic.TIENDA = @Tienda");
                    parameters.Add(new SqlParameter("@Tienda", SqlDbType.NVarChar, 50) { Value = request.Tienda });
                }

                if (request.Estado.HasValue)
                {
                    whereConditions.Add("ic.ESTADO = @Estado");
                    parameters.Add(new SqlParameter("@Estado", SqlDbType.NVarChar, 20) { Value = request.Estado.Value.ToString() });
                }

                if (!string.IsNullOrEmpty(request.EstatusCodigoFilter))
                {
                    whereConditions.Add("ic.ESTATUS_CODIGO = @EstatusCodigoFilter");
                    parameters.Add(new SqlParameter("@EstatusCodigoFilter", SqlDbType.NVarChar, 20) { Value = request.EstatusCodigoFilter });
                }

                if (!string.IsNullOrEmpty(request.DivisionCode))
                {
                    whereConditions.Add("ic.COD_DIVISION = @DivisionCode");
                    parameters.Add(new SqlParameter("@DivisionCode", SqlDbType.NVarChar, 20) { Value = request.DivisionCode });
                }

                if (!string.IsNullOrEmpty(request.Categoria))
                {
                    whereConditions.Add("ic.CATEGORIA = @Categoria");
                    parameters.Add(new SqlParameter("@Categoria", SqlDbType.NVarChar, 100) { Value = request.Categoria });
                }

                if (request.FromDate.HasValue)
                {
                    whereConditions.Add("ic.FECHA >= @FromDate");
                    parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = request.FromDate.Value });
                }

                if (request.ToDate.HasValue)
                {
                    whereConditions.Add("ic.FECHA <= @ToDate");
                    parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = request.ToDate.Value });
                }

                if (request.AssignedToID.HasValue)
                {
                    whereConditions.Add("rc.AssignedToID = @AssignedToID");
                    parameters.Add(new SqlParameter("@AssignedToID", SqlDbType.Int) { Value = request.AssignedToID.Value });
                }

                if (request.HasDifferences.HasValue)
                {
                    if (request.HasDifferences.Value)
                        whereConditions.Add("ABS(ISNULL(ic.CANTIDAD_FISICA, 0) - ic.STOCK_CALCULADO) > 0.01");
                    else
                        whereConditions.Add("ABS(ISNULL(ic.CANTIDAD_FISICA, 0) - ic.STOCK_CALCULADO) <= 0.01");
                }

                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    whereConditions.Add("(ic.COD_BARRAS LIKE @SearchTerm OR ic.DESCRIPCION LIKE @SearchTerm OR ic.TICKET LIKE @SearchTerm)");
                    parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 200) { Value = $"%{request.SearchTerm}%" });
                }

                var whereClause = string.Join(" AND ", whereConditions);

                // Consulta con paginación usando la vista completa
              var query = $@"
    SELECT COUNT(*) FROM View_InventoryCountsComplete ic 
    LEFT JOIN RequestCodes rc ON ic.CodeID = rc.ID
    WHERE {whereClause};
    
    SELECT * FROM (
        SELECT *, ROW_NUMBER() OVER (ORDER BY ic.FECHA DESC) as RowNum
        FROM View_InventoryCountsComplete ic 
        LEFT JOIN RequestCodes rc ON ic.CodeID = rc.ID
        WHERE {whereClause}
    ) t 
    WHERE RowNum BETWEEN @StartRow AND @EndRow
    ORDER BY FECHA DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddRange(parameters.ToArray());
                command.Parameters.Add("@StartRow", SqlDbType.Int).Value = (request.PageNumber - 1) * request.PageSize + 1;
                command.Parameters.Add("@EndRow", SqlDbType.Int).Value = request.PageNumber * request.PageSize;

                using var reader = await command.ExecuteReaderAsync();
                
                // Leer total de registros
                await reader.ReadAsync();
                var totalRecords = Convert.ToInt32(reader[0]);
                
                await reader.NextResultAsync();
                
                var counts = new List<InventoryCountResponse>();
                while (await reader.ReadAsync())
                {
                    counts.Add(MapToInventoryCountResponse(reader));
                }

                var totalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize);

                var pagedResponse = new PagedResponse<InventoryCountResponse>
                {
                    Data = counts,
                    TotalRecords = totalRecords,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    HasNextPage = request.PageNumber < totalPages,
                    HasPreviousPage = request.PageNumber > 1
                };

                return new ApiResponse<PagedResponse<InventoryCountResponse>>
                {
                    Success = true,
                    Message = "Conteos obtenidos exitosamente",
                    Data = pagedResponse
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PagedResponse<InventoryCountResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener conteos: {ex.Message}",
                    Data = new PagedResponse<InventoryCountResponse>()
                };
            }
        }

        public async Task<ApiResponse<InventoryCountResponse>> GetInventoryCountByIdAsync(int countId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT * FROM View_InventoryCountsComplete 
                    WHERE ID = @CountID AND IsActive = 1";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@CountID", SqlDbType.Int).Value = countId;

                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new ApiResponse<InventoryCountResponse>
                    {
                        Success = false,
                        Message = "Conteo no encontrado"
                    };
                }

                var count = MapToInventoryCountResponse(reader);

                return new ApiResponse<InventoryCountResponse>
                {
                    Success = true,
                    Message = "Conteo obtenido exitosamente",
                    Data = count
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InventoryCountResponse>
                {
                    Success = false,
                    Message = $"Error al obtener conteo: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<List<InventoryCountHistoryResponse>>> GetCountHistoryAsync(int countId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT * FROM InventoryCountHistory 
                    WHERE CountID = @CountID 
                    ORDER BY CreatedDate DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@CountID", SqlDbType.Int).Value = countId;

                var history = new List<InventoryCountHistoryResponse>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    history.Add(new InventoryCountHistoryResponse
                    {
                        ID = Convert.ToInt32(reader["ID"]),
                        CountID = Convert.ToInt32(reader["CountID"]),
                        UserID = Convert.ToInt32(reader["UserID"]),
                        UserName = reader["UserName"]?.ToString() ?? "",
                        Action = Enum.Parse<CountAction>(reader["Action"]?.ToString() ?? "CREATED"),
                        OldValue = reader["OldValue"]?.ToString() ?? "",
                        NewValue = reader["NewValue"]?.ToString() ?? "",
                        Comment = reader["Comment"]?.ToString() ?? "",
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                    });
                }

                return new ApiResponse<List<InventoryCountHistoryResponse>>
                {
                    Success = true,
                    Message = "Historial obtenido exitosamente",
                    Data = history
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InventoryCountHistoryResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener historial: {ex.Message}",
                    Data = new List<InventoryCountHistoryResponse>()
                };
            }
        }

        public async Task<ApiResponse<InventoryCountDashboardResponse>> GetCountDashboardAsync(string tienda = "")
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var dashboard = new InventoryCountDashboardResponse();

                // Estadísticas generales
                var whereClause = string.IsNullOrEmpty(tienda) ? "WHERE IsActive = 1" : "WHERE IsActive = 1 AND TIENDA = @Tienda";
                
                var statsQuery = $@"
                    SELECT 
                        COUNT(*) as TotalCounts,
                        COUNT(CASE WHEN ESTADO = 'EN_REVISION' THEN 1 END) as CountsEnRevision,
                        COUNT(CASE WHEN ESTADO = 'DEVUELTO' THEN 1 END) as CountsDevueltos,
                        COUNT(CASE WHEN ESTADO = 'FORENSE' THEN 1 END) as CountsForenses,
                        COUNT(CASE WHEN ESTADO = 'AJUSTADO' THEN 1 END) as CountsAjustados,
                        COUNT(CASE WHEN CANTIDAD_FISICA IS NULL THEN 1 END) as CountsPendientes,
                        COUNT(CASE WHEN ABS(ISNULL(CANTIDAD_FISICA, 0) - STOCK_CALCULADO) > 0.01 THEN 1 END) as CountsWithDifferences,
                        COUNT(CASE WHEN ABS(ISNULL(CANTIDAD_FISICA, 0) - STOCK_CALCULADO) <= 0.01 THEN 1 END) as CountsWithoutDifferences,
                        SUM(COSTO_TOTAL) as TotalCostoDiferencias,
                        SUM(CASE WHEN COSTO_TOTAL > 0 THEN COSTO_TOTAL ELSE 0 END) as CostoAjustesPositivos,
                        SUM(CASE WHEN COSTO_TOTAL < 0 THEN COSTO_TOTAL ELSE 0 END) as CostoAjustesNegativos
                    FROM InventoryCounts {whereClause}";

                using var statsCommand = new SqlCommand(statsQuery, connection);
                if (!string.IsNullOrEmpty(tienda))
                {
                    statsCommand.Parameters.Add("@Tienda", SqlDbType.NVarChar, 50).Value = tienda;
                }

                using var statsReader = await statsCommand.ExecuteReaderAsync();
                if (await statsReader.ReadAsync())
                {
                    dashboard.TotalCounts = Convert.ToInt32(statsReader["TotalCounts"]);
                    dashboard.CountsEnRevision = Convert.ToInt32(statsReader["CountsEnRevision"]);
                    dashboard.CountsDevueltos = Convert.ToInt32(statsReader["CountsDevueltos"]);
                    dashboard.CountsForenses = Convert.ToInt32(statsReader["CountsForenses"]);
                    dashboard.CountsAjustados = Convert.ToInt32(statsReader["CountsAjustados"]);
                    dashboard.CountsPendientes = Convert.ToInt32(statsReader["CountsPendientes"]);
                    dashboard.CountsWithDifferences = Convert.ToInt32(statsReader["CountsWithDifferences"]);
                    dashboard.CountsWithoutDifferences = Convert.ToInt32(statsReader["CountsWithoutDifferences"]);
                    dashboard.TotalCostoDiferencias = Convert.ToDecimal(statsReader["TotalCostoDiferencias"] ?? 0);
                    dashboard.CostoAjustesPositivos = Convert.ToDecimal(statsReader["CostoAjustesPositivos"] ?? 0);
                    dashboard.CostoAjustesNegativos = Convert.ToDecimal(statsReader["CostoAjustesNegativos"] ?? 0);
                }
                statsReader.Close();

                // Estadísticas por tienda (solo si no se filtró por tienda específica)
                if (string.IsNullOrEmpty(tienda))
                {
                    const string tiendaStatsQuery = @"
                        SELECT 
                            TIENDA,
                            COUNT(*) as TotalCounts,
                            COUNT(CASE WHEN ABS(ISNULL(CANTIDAD_FISICA, 0) - STOCK_CALCULADO) > 0.01 THEN 1 END) as CountsWithDifferences,
                            SUM(COSTO_TOTAL) as TotalCostoDiferencias,
                            CAST(COUNT(CASE WHEN CANTIDAD_FISICA IS NOT NULL THEN 1 END) * 100.0 / COUNT(*) AS DECIMAL(5,2)) as CompletionPercentage
                        FROM InventoryCounts 
                        WHERE IsActive = 1
                        GROUP BY TIENDA
                        ORDER BY TotalCounts DESC";

                    using var tiendaCommand = new SqlCommand(tiendaStatsQuery, connection);
                    using var tiendaReader = await tiendaCommand.ExecuteReaderAsync();

                    while (await tiendaReader.ReadAsync())
                    {
                        dashboard.CountsByTienda.Add(new CountsByTiendaStats
                        {
                            Tienda = tiendaReader["TIENDA"]?.ToString() ?? "",
                            TotalCounts = Convert.ToInt32(tiendaReader["TotalCounts"]),
                            CountsWithDifferences = Convert.ToInt32(tiendaReader["CountsWithDifferences"]),
                            TotalCostoDiferencias = Convert.ToDecimal(tiendaReader["TotalCostoDiferencias"] ?? 0),
                            CompletionPercentage = Convert.ToDecimal(tiendaReader["CompletionPercentage"] ?? 0)
                        });
                    }
                    tiendaReader.Close();
                }

                // Estadísticas por división
                var divisionWhereClause = string.IsNullOrEmpty(tienda) ? "WHERE IsActive = 1" : "WHERE IsActive = 1 AND TIENDA = @Tienda2";
                var divisionStatsQuery = $@"
                    SELECT 
                        COD_DIVISION,
                        CATEGORIA,
                        COUNT(*) as TotalCounts,
                        COUNT(CASE WHEN ABS(ISNULL(CANTIDAD_FISICA, 0) - STOCK_CALCULADO) > 0.01 THEN 1 END) as CountsWithDifferences,
                        SUM(COSTO_TOTAL) as TotalCostoDiferencias
                    FROM InventoryCounts 
                    {divisionWhereClause}
                    AND COD_DIVISION IS NOT NULL AND COD_DIVISION != ''
                    GROUP BY COD_DIVISION, CATEGORIA
                    ORDER BY TotalCounts DESC";

                using var divisionCommand = new SqlCommand(divisionStatsQuery, connection);
                if (!string.IsNullOrEmpty(tienda))
                {
                    divisionCommand.Parameters.Add("@Tienda2", SqlDbType.NVarChar, 50).Value = tienda;
                }

                using var divisionReader = await divisionCommand.ExecuteReaderAsync();
                while (await divisionReader.ReadAsync())
                {
                    dashboard.CountsByDivision.Add(new CountsByDivisionStats
                    {
                        DivisionCode = divisionReader["COD_DIVISION"]?.ToString() ?? "",
                        Division = divisionReader["CATEGORIA"]?.ToString() ?? "",
                        TotalCounts = Convert.ToInt32(divisionReader["TotalCounts"]),
                        CountsWithDifferences = Convert.ToInt32(divisionReader["CountsWithDifferences"]),
                        TotalCostoDiferencias = Convert.ToDecimal(divisionReader["TotalCostoDiferencias"] ?? 0)
                    });
                }
                divisionReader.Close();

                // Estadísticas por estado
                var statusWhereClause = string.IsNullOrEmpty(tienda) ? "WHERE IsActive = 1" : "WHERE IsActive = 1 AND TIENDA = @Tienda3";
                var statusStatsQuery = $@"
    SELECT 
        ESTADO,
        COUNT(*) as Count,
        CAST(COUNT(*) * 100.0 / (
            SELECT COUNT(*) 
            FROM InventoryCounts 
            {statusWhereClause.Replace("@Tienda3", "@Tienda4")}
        ) AS DECIMAL(5,2)) as Percentage,
        SUM(COSTO_TOTAL) as TotalCosto
    FROM InventoryCounts 
    {statusWhereClause}
    GROUP BY ESTADO
    ORDER BY Count DESC";

                using var statusCommand = new SqlCommand(statusStatsQuery, connection);
                if (!string.IsNullOrEmpty(tienda))
                {
                    statusCommand.Parameters.Add("@Tienda3", SqlDbType.NVarChar, 50).Value = tienda;
                    statusCommand.Parameters.Add("@Tienda4", SqlDbType.NVarChar, 50).Value = tienda;
                }

                using var statusReader = await statusCommand.ExecuteReaderAsync();
                while (await statusReader.ReadAsync())
                {
                    dashboard.CountsByStatus.Add(new CountsByStatusStats
                    {
                        Estado = Enum.Parse<CountStatus>(statusReader["ESTADO"]?.ToString() ?? "EN_REVISION"),
                        Count = Convert.ToInt32(statusReader["Count"]),
                        Percentage = Convert.ToDecimal(statusReader["Percentage"] ?? 0),
                        TotalCosto = Convert.ToDecimal(statusReader["TotalCosto"] ?? 0)
                    });
                }
                statusReader.Close();

                // Estadísticas por tipo de movimiento
                var movementWhereClause = string.IsNullOrEmpty(tienda) ? "WHERE IsActive = 1 AND CANTIDAD_FISICA IS NOT NULL" : "WHERE IsActive = 1 AND CANTIDAD_FISICA IS NOT NULL AND TIENDA = @Tienda5";
                var movementStatsQuery = $@"
                    SELECT 
                        CASE 
                            WHEN (ISNULL(CANTIDAD_FISICA, 0) - STOCK_CALCULADO) > 0 THEN 'AJUSTE_POSITIVO'
                            WHEN (ISNULL(CANTIDAD_FISICA, 0) - STOCK_CALCULADO) < 0 THEN 'AJUSTE_NEGATIVO'
                            ELSE 'STOCK_CUADRADO'
                        END as TipoMovimiento,
                        COUNT(*) as Count,
                        SUM(COSTO_TOTAL) as TotalCosto,
                        CAST(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM InventoryCounts {movementWhereClause.Replace("@Tienda5", "@Tienda6")}) AS DECIMAL(5,2)) as Percentage
                    FROM InventoryCounts 
                    {movementWhereClause}
                    GROUP BY CASE 
                        WHEN (ISNULL(CANTIDAD_FISICA, 0) - STOCK_CALCULADO) > 0 THEN 'AJUSTE_POSITIVO'
                        WHEN (ISNULL(CANTIDAD_FISICA, 0) - STOCK_CALCULADO) < 0 THEN 'AJUSTE_NEGATIVO'
                        ELSE 'STOCK_CUADRADO'
                    END
                    ORDER BY Count DESC";

                using var movementCommand = new SqlCommand(movementStatsQuery, connection);
                if (!string.IsNullOrEmpty(tienda))
                {
                    movementCommand.Parameters.Add("@Tienda5", SqlDbType.NVarChar, 50).Value = tienda;
                    movementCommand.Parameters.Add("@Tienda6", SqlDbType.NVarChar, 50).Value = tienda;
                }

                using var movementReader = await movementCommand.ExecuteReaderAsync();
                while (await movementReader.ReadAsync())
                {
                    dashboard.CountsByMovementType.Add(new CountsByMovementTypeStats
                    {
                        TipoMovimiento = Enum.Parse<MovementType>(movementReader["TipoMovimiento"]?.ToString() ?? "STOCK_CUADRADO"),
                        Count = Convert.ToInt32(movementReader["Count"]),
                        TotalCosto = Convert.ToDecimal(movementReader["TotalCosto"] ?? 0),
                        Percentage = Convert.ToDecimal(movementReader["Percentage"] ?? 0)
                    });
                }
                movementReader.Close();

                // Conteos recientes (últimos 10)
                var recentWhereClause = string.IsNullOrEmpty(tienda) ? "WHERE IsActive = 1" : "WHERE IsActive = 1 AND TIENDA = @Tienda7";
                var recentQuery = $@"
    SELECT TOP 10 * FROM View_InventoryCountsComplete 
    {recentWhereClause}
    ORDER BY FECHA DESC";

                using var recentCommand = new SqlCommand(recentQuery, connection);
                if (!string.IsNullOrEmpty(tienda))
                {
                    recentCommand.Parameters.Add("@Tienda7", SqlDbType.NVarChar, 50).Value = tienda;
                }

                using var recentReader = await recentCommand.ExecuteReaderAsync();
                while (await recentReader.ReadAsync())
                {
                    dashboard.RecentCounts.Add(MapToInventoryCountResponse(recentReader));
                }

                return new ApiResponse<InventoryCountDashboardResponse>
                {
                    Success = true,
                    Message = "Dashboard obtenido exitosamente",
                    Data = dashboard
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InventoryCountDashboardResponse>
                {
                    Success = false,
                    Message = $"Error al obtener dashboard: {ex.Message}",
                    Data = new InventoryCountDashboardResponse()
                };
            }
        }

        public async Task<ApiResponse<List<InventoryCountSummaryResponse>>> GetMyAssignedCountsAsync(int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        ic.ID, ic.COD_BARRAS, ic.DESCRIPCION, ic.TIENDA, 
                        ic.STOCK_CALCULADO, ic.CANTIDAD_FISICA, 
                        ISNULL(ic.CANTIDAD_FISICA, 0) - ic.STOCK_CALCULADO as DIFERENCIA,
                        ic.COSTO_TOTAL, ic.ESTADO,
                        rc.AssignedToName
                    FROM InventoryCounts ic
                    INNER JOIN RequestCodes rc ON ic.CodeID = rc.ID
                    WHERE rc.AssignedToID = @UserID AND ic.IsActive = 1
                    ORDER BY ic.ESTADO, ic.FECHA DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;

                var counts = new List<InventoryCountSummaryResponse>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    counts.Add(new InventoryCountSummaryResponse
                    {
                        ID = Convert.ToInt32(reader["ID"]),
                        CodBarras = reader["COD_BARRAS"]?.ToString() ?? "",
                        Descripcion = reader["DESCRIPCION"]?.ToString() ?? "",
                        Tienda = reader["TIENDA"]?.ToString() ?? "",
                        StockCalculado = Convert.ToDecimal(reader["STOCK_CALCULADO"]),
                        CantidadFisica = reader.IsDBNull("CANTIDAD_FISICA") ? null : Convert.ToDecimal(reader["CANTIDAD_FISICA"]),
                        Diferencia = Convert.ToDecimal(reader["DIFERENCIA"]),
                        CostoTotal = Convert.ToDecimal(reader["COSTO_TOTAL"]),
                        Estado = Enum.Parse<CountStatus>(reader["ESTADO"]?.ToString() ?? "EN_REVISION"),
                        AssignedToName = reader["AssignedToName"]?.ToString() ?? ""
                    });
                }

                return new ApiResponse<List<InventoryCountSummaryResponse>>
                {
                    Success = true,
                    Message = "Conteos asignados obtenidos exitosamente",
                    Data = counts
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InventoryCountSummaryResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener conteos asignados: {ex.Message}",
                    Data = new List<InventoryCountSummaryResponse>()
                };
            }
        }

        public async Task<ApiResponse<bool>> AddCountCommentAsync(AddCountCommentRequest request, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var userInfo = await GetUserInfoAsync(userId, connection);

                await AddCountHistoryEntryAsync(request.CountID, userId, 
                                              userInfo?.Usuario ?? "Usuario", CountAction.COMMENT_ADDED, 
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

        public async Task<ApiResponse<object>> BatchUpdateCountsAsync(BatchUpdateCountsRequest request, int userId)
        {
            try
            {
                using var connection = new SqlConnection(_inventarioConnection);
                await connection.OpenAsync();

                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                foreach (var countRequest in request.Counts)
                {
                    try
                    {
                        var result = await RegisterPhysicalCountAsync(countRequest, userId);
                        
                        results.Add(new 
                        { 
                            CountID = countRequest.CountID, 
                            Success = result.Success, 
                            Message = result.Message 
                        });

                        if (result.Success)
                            successCount++;
                        else
                            failCount++;
                    }
                    catch (Exception ex)
                    {
                        results.Add(new 
                        { 
                            CountID = countRequest.CountID, 
                            Success = false, 
                            Message = ex.Message 
                        });
                        failCount++;
                    }
                }

                return new ApiResponse<object>
                {
                    Success = failCount == 0,
                    Message = $"Procesados {request.Counts.Count} conteos. Exitosos: {successCount}, Fallidos: {failCount}",
                    Data = new { Results = results, SuccessCount = successCount, FailCount = failCount }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = $"Error en actualización masiva: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<List<InventoryCountResponse>>> GetPendingCountsByRequestAsync(int requestId)
        {
            try
            {
                var getCountsRequest = new GetInventoryCountsRequest
                {
                    RequestID = requestId,
                    EstatusCodigoFilter = "PENDIENTE",
                    PageSize = 1000
                };

                var result = await GetInventoryCountsAsync(getCountsRequest);
                
                return new ApiResponse<List<InventoryCountResponse>>
                {
                    Success = result.Success,
                    Message = result.Message,
                    Data = result.Data?.Data ?? new List<InventoryCountResponse>()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InventoryCountResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener conteos pendientes: {ex.Message}",
                    Data = new List<InventoryCountResponse>()
                };
            }
        }

        // Métodos auxiliares privados
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

        private async Task AddCountHistoryEntryAsync(int countId, int userId, string userName, 
                                                   CountAction action, string? oldValue, string? newValue, 
                                                   string? comment, SqlConnection connection, SqlTransaction? transaction = null)
        {
            const string query = @"
                INSERT INTO InventoryCountHistory (CountID, UserID, UserName, Action, OldValue, NewValue, Comment)
                VALUES (@CountID, @UserID, @UserName, @Action, @OldValue, @NewValue, @Comment)";

            using var command = transaction != null ? 
                new SqlCommand(query, connection, transaction) : 
                new SqlCommand(query, connection);

            command.Parameters.Add("@CountID", SqlDbType.Int).Value = countId;
            command.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
            command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = userName;
            command.Parameters.Add("@Action", SqlDbType.NVarChar, 50).Value = action.ToString();
            command.Parameters.Add("@OldValue", SqlDbType.NVarChar, 500).Value = oldValue ?? (object)DBNull.Value;
            command.Parameters.Add("@NewValue", SqlDbType.NVarChar, 500).Value = newValue ?? (object)DBNull.Value;
            command.Parameters.Add("@Comment", SqlDbType.NVarChar, 1000).Value = comment ?? (object)DBNull.Value;

            await command.ExecuteNonQueryAsync();
        }

        private InventoryCountResponse MapToInventoryCountResponse(SqlDataReader reader)
        {
            return new InventoryCountResponse
            {
                ID = Convert.ToInt32(reader["ID"]),
                RequestID = Convert.ToInt32(reader["RequestID"]),
                CodeID = Convert.ToInt32(reader["CodeID"]),
                Tienda = reader["TIENDA"]?.ToString() ?? "",
                CodBarras = reader["COD_BARRAS"]?.ToString() ?? "",
                NoProducto = reader["No_PRODUCTO"]?.ToString() ?? "",
                Descripcion = reader["DESCRIPCION"]?.ToString() ?? "",
                Descripcion2 = reader["DESCRIPCION2"]?.ToString() ?? "",
                CodDivision = reader["COD_DIVISION"]?.ToString() ?? "",
                Categoria = reader["CATEGORIA"]?.ToString() ?? "",
                StockCalculado = Convert.ToDecimal(reader["STOCK_CALCULADO"]),
                CantidadFisica = reader.IsDBNull("CANTIDAD_FISICA") ? null : Convert.ToDecimal(reader["CANTIDAD_FISICA"]),
                Diferencia = Convert.ToDecimal(reader["DIFERENCIA"]),
                CostoUnitario = Convert.ToDecimal(reader["COSTO_UNITARIO"]),
                CostoTotal = Convert.ToDecimal(reader["COSTO_TOTAL"]),
                Comentario = reader["COMENTARIO"]?.ToString() ?? "",
                TipoMovimiento = Enum.Parse<MovementType>(reader["TIPO_MOVIMIENTO"]?.ToString() ?? "STOCK_CUADRADO"),
                EstatusCodigoFilter = reader["ESTATUS_CODIGO"]?.ToString() ?? "",
                Ticket = reader["TICKET"]?.ToString() ?? "",
                Estado = Enum.Parse<CountStatus>(reader["ESTADO"]?.ToString() ?? "EN_REVISION"),
                Fecha = Convert.ToDateTime(reader["FECHA"]),
                CreatedBy = Convert.ToInt32(reader["CreatedBy"]),
                CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                UpdatedBy = reader.IsDBNull("UpdatedBy") ? null : Convert.ToInt32(reader["UpdatedBy"]),
                UpdatedDate = Convert.ToDateTime(reader["UpdatedDate"]),
                IsActive = Convert.ToBoolean(reader["IsActive"]),

                // Información adicional si está disponible en la vista
                TicketNumber = GetSafeString(reader, "TicketNumber"),
                RequestorName = GetSafeString(reader, "RequestorName"),
                RequestPriority = GetSafeEnum<RequestPriority>(reader, "RequestPriority", RequestPriority.NORMAL),
                ProductCode = GetSafeString(reader, "ProductCode"),
                AssignedToName = GetSafeString(reader, "AssignedToName"),

                // Información del producto desde INNOVACENTRO si está disponible
                ProductDescription = GetSafeString(reader, "ProductDescription"),
                ProductDescription2 = GetSafeString(reader, "ProductDescription2"),
                ProductDivisionCode = GetSafeString(reader, "ProductDivisionCode"),
                ItemCategoryCode = GetSafeString(reader, "ItemCategoryCode"),
                ProductGroupCode = GetSafeString(reader, "ProductGroupCode"),
                UnitMeasureCode = GetSafeString(reader, "UnitMeasureCode"),
                ProductStatus = GetSafeString(reader, "ProductStatus"),
                ItemClasification = GetSafeString(reader, "ItemClasification"),
                ItemStockClasification = GetSafeString(reader, "ItemStockClasification"),
                ItemUnitPrice = GetSafeDecimal(reader, "ItemUnitPrice"),
                ItemUnitCost = GetSafeDecimal(reader, "ItemUnitCost"),
                CentroAbastecimiento = GetSafeString(reader, "Centro Abastecimiento"),
                CentroAbastecimiento2 = GetSafeString(reader, "Centro Abastecimiento 2")
            };
        }

        // Métodos auxiliares para lectura segura del SqlDataReader
        private string GetSafeString(SqlDataReader reader, string columnName)
        {
            try
            {
                return reader[columnName]?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private decimal GetSafeDecimal(SqlDataReader reader, string columnName)
        {
            try
            {
                return reader[columnName] != DBNull.Value ? Convert.ToDecimal(reader[columnName]) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private T GetSafeEnum<T>(SqlDataReader reader, string columnName, T defaultValue) where T : struct, Enum
        {
            try
            {
                var value = reader[columnName]?.ToString();
                return string.IsNullOrEmpty(value) ? defaultValue : Enum.Parse<T>(value);
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
using System.Data;
using InventarioAPI.Models;
using Microsoft.Data.SqlClient;

namespace InventarioAPI.Services
{
    public interface IAssignmentService
    {
        Task<ApiResponse<AssignmentResponse>> CreateAssignmentAsync(CreateAssignmentRequest request, int assignedBy);
        Task<ApiResponse<List<AssignmentResponse>>> GetAssignmentsAsync(GetAssignmentsRequest request);
        Task<ApiResponse<List<ProductHierarchyItem>>> GetProductHierarchyAsync();
        Task<ApiResponse<bool>> DeleteAssignmentAsync(int assignmentId, int deletedBy);
        Task<ApiResponse<List<AssignmentResponse>>> GetUserAssignmentsByTiendaAsync(string tienda);
    }

    public class AssignmentService : IAssignmentService
    {
        private readonly IConfiguration _configuration;
        private readonly string _inventarioConnection;
        private readonly string _innovacentroConnection;

        public AssignmentService(IConfiguration configuration)
        {
            _configuration = configuration;
            _inventarioConnection = _configuration.GetConnectionString("InventarioConnection") 
                ?? throw new InvalidOperationException("InventarioConnection not found");
            _innovacentroConnection = _configuration.GetConnectionString("InnovacentroConnection") 
                ?? throw new InvalidOperationException("InnovacentroConnection not found");
        }

        public async Task<ApiResponse<AssignmentResponse>> CreateAssignmentAsync(CreateAssignmentRequest request, int assignedBy)
        {
            try
            {
                // Validar que el usuario existe
                var userExists = await ValidateUserExistsAsync(request.UserID);
                if (!userExists)
                {
                    return new ApiResponse<AssignmentResponse>
                    {
                        Success = false,
                        Message = "El usuario especificado no existe"
                    };
                }

                // Desactivar asignación anterior del mismo tipo y tienda para el usuario
                await DeactivatePreviousAssignmentAsync(request.UserID, request.Tienda, request.AssignmentType);

                // Crear nueva asignación
                const string insertQuery = @"
                    INSERT INTO UserProductAssignments 
                    (UserID, Tienda, AssignmentType, DivisionCode, Division, CategoryCode, Categoria, 
                     GroupCode, Grupo, SubGroupCode, SubGrupo, AssignedBy, IsActive)
                    VALUES 
                    (@UserID, @Tienda, @AssignmentType, @DivisionCode, @Division, @CategoryCode, @Categoria,
                     @GroupCode, @Grupo, @SubGroupCode, @SubGrupo, @AssignedBy, 1);
                    SELECT SCOPE_IDENTITY();";

                using var connection = new SqlConnection(_inventarioConnection);
                using var command = new SqlCommand(insertQuery, connection);

                command.Parameters.AddWithValue("@UserID", request.UserID);
                command.Parameters.AddWithValue("@Tienda", request.Tienda);
                command.Parameters.AddWithValue("@AssignmentType", request.AssignmentType.ToString());
                command.Parameters.AddWithValue("@DivisionCode", (object)request.DivisionCode ?? DBNull.Value);
                command.Parameters.AddWithValue("@Division", (object)request.Division ?? DBNull.Value);
                command.Parameters.AddWithValue("@CategoryCode", (object)request.CategoryCode ?? DBNull.Value);
                command.Parameters.AddWithValue("@Categoria", (object)request.Categoria ?? DBNull.Value);
                command.Parameters.AddWithValue("@GroupCode", (object)request.GroupCode ?? DBNull.Value);
                command.Parameters.AddWithValue("@Grupo", (object)request.Grupo ?? DBNull.Value);
                command.Parameters.AddWithValue("@SubGroupCode", (object)request.SubGroupCode ?? DBNull.Value);
                command.Parameters.AddWithValue("@SubGrupo", (object)request.SubGrupo ?? DBNull.Value);
                command.Parameters.AddWithValue("@AssignedBy", assignedBy);

                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                var newId = Convert.ToInt32(result);

                // Obtener la asignación creada
                var createdAssignment = await GetAssignmentByIdAsync(newId);

                return new ApiResponse<AssignmentResponse>
                {
                    Success = true,
                    Message = "Asignación creada exitosamente",
                    Data = createdAssignment
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<AssignmentResponse>
                {
                    Success = false,
                    Message = $"Error al crear la asignación: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<List<AssignmentResponse>>> GetAssignmentsAsync(GetAssignmentsRequest request)
        {
            try
            {
                var whereConditions = new List<string>();
                var parameters = new List<SqlParameter>();

                if (request.UserID.HasValue)
                {
                    whereConditions.Add("a.UserID = @UserID");
                    parameters.Add(new SqlParameter("@UserID", request.UserID.Value));
                }

                if (!string.IsNullOrEmpty(request.Tienda))
                {
                    whereConditions.Add("a.Tienda = @Tienda");
                    parameters.Add(new SqlParameter("@Tienda", request.Tienda));
                }

                if (request.AssignmentType.HasValue)
                {
                    whereConditions.Add("a.AssignmentType = @AssignmentType");
                    parameters.Add(new SqlParameter("@AssignmentType", request.AssignmentType.Value.ToString()));
                }

                if (request.IsActive.HasValue)
                {
                    whereConditions.Add("a.IsActive = @IsActive");
                    parameters.Add(new SqlParameter("@IsActive", request.IsActive.Value));
                }

                var whereClause = whereConditions.Count > 0 
                    ? "WHERE " + string.Join(" AND ", whereConditions)
                    : "";

                const string query = @"
                    SELECT a.ID, a.UserID, u.USUARIO, u.NOMBRE as NombreUsuario, a.Tienda, a.AssignmentType,
                           a.DivisionCode, a.Division, a.CategoryCode, a.Categoria, 
                           a.GroupCode, a.Grupo, a.SubGroupCode, a.SubGrupo,
                           a.AssignedBy, ab.USUARIO as AssignedByName, a.AssignedDate, a.IsActive
                    FROM UserProductAssignments a
                    INNER JOIN Usuarios u ON a.UserID = u.ID
                    INNER JOIN Usuarios ab ON a.AssignedBy = ab.ID
                    {0}
                    ORDER BY a.AssignedDate DESC";

                using var connection = new SqlConnection(_inventarioConnection);
                using var command = new SqlCommand(string.Format(query, whereClause), connection);

                command.Parameters.AddRange(parameters.ToArray());
                await connection.OpenAsync();

                var assignments = new List<AssignmentResponse>();
                using var reader = await command.ExecuteReaderAsync();

              while (await reader.ReadAsync())
{
    assignments.Add(new AssignmentResponse
    {
        ID = reader["ID"] is DBNull ? 0 : Convert.ToInt32(reader["ID"]),
        UserID = reader["UserID"] is DBNull ? 0 : Convert.ToInt32(reader["UserID"]),
        Usuario = reader["USUARIO"] is DBNull ? "" : reader["USUARIO"].ToString(),
        NombreUsuario = reader["NombreUsuario"] is DBNull ? "" : reader["NombreUsuario"].ToString(),
        Tienda = reader["Tienda"] is DBNull ? "" : reader["Tienda"].ToString(),
        AssignmentType = Enum.Parse<AssignmentType>(reader["AssignmentType"].ToString()),
        ProductInfo = new ProductAssignmentInfo
        {
            DivisionCode = reader["DivisionCode"] is DBNull ? "" : reader["DivisionCode"].ToString(),
            Division = reader["Division"] is DBNull ? "" : reader["Division"].ToString(),
            CategoryCode = reader["CategoryCode"] is DBNull ? "" : reader["CategoryCode"].ToString(),
            Categoria = reader["Categoria"] is DBNull ? "" : reader["Categoria"].ToString(),
            GroupCode = reader["GroupCode"] is DBNull ? "" : reader["GroupCode"].ToString(),
            Grupo = reader["Grupo"] is DBNull ? "" : reader["Grupo"].ToString(),
            SubGroupCode = reader["SubGroupCode"] is DBNull ? "" : reader["SubGroupCode"].ToString(),
            SubGrupo = reader["SubGrupo"] is DBNull ? "" : reader["SubGrupo"].ToString()
        },
        AssignedBy = reader["AssignedBy"] is DBNull ? 0 : Convert.ToInt32(reader["AssignedBy"]),
        AssignedByName = reader["AssignedByName"] is DBNull ? "" : reader["AssignedByName"].ToString(),
        AssignedDate = reader["AssignedDate"] is DBNull ? DateTime.Now : Convert.ToDateTime(reader["AssignedDate"]),
        IsActive = reader["IsActive"] is DBNull ? false : Convert.ToBoolean(reader["IsActive"])
    });
}


                return new ApiResponse<List<AssignmentResponse>>
                {
                    Success = true,
                    Message = "Asignaciones obtenidas exitosamente",
                    Data = assignments
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<AssignmentResponse>>
                {
                    Success = false,
                    Message = $"Error al obtener asignaciones: {ex.Message}",
                    Data = new List<AssignmentResponse>()
                };
            }
        }

        public async Task<ApiResponse<List<ProductHierarchyItem>>> GetProductHierarchyAsync()
        {
            try
            {
                const string query = @"
                    SELECT [Division Code] as DivisionCode, [Division], 
                           [Item Category Code] as CategoryCode, [Categoria],
                           [Product Group Code] as GroupCode, [Grupo],
                           [Codigo Subgrupo] as SubGroupCode, [SubGrupo]
                    FROM [INNOVACENTRO].[dbo].[View_ProductosLI]
                    GROUP BY [Division Code], [Division], [Item Category Code], [Categoria],
                             [Product Group Code], [Grupo], [Codigo Subgrupo], [SubGrupo]
                    ORDER BY [Division], [Categoria], [Grupo], [SubGrupo]";

                using var connection = new SqlConnection(_innovacentroConnection);
                using var command = new SqlCommand(query, connection);

                await connection.OpenAsync();
                var hierarchy = new List<ProductHierarchyItem>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    hierarchy.Add(new ProductHierarchyItem
                    {
                     DivisionCode = reader["DivisionCode"] is DBNull ? "" : reader["DivisionCode"].ToString(),
Division = reader["Division"] is DBNull ? "" : reader["Division"].ToString(),
CategoryCode = reader["CategoryCode"] is DBNull ? "" : reader["CategoryCode"].ToString(),
Categoria = reader["Categoria"] is DBNull ? "" : reader["Categoria"].ToString(),
GroupCode = reader["GroupCode"] is DBNull ? "" : reader["GroupCode"].ToString(),
Grupo = reader["Grupo"] is DBNull ? "" : reader["Grupo"].ToString(),
SubGroupCode = reader["SubGroupCode"] is DBNull ? "" : reader["SubGroupCode"].ToString(),
SubGrupo = reader["SubGrupo"] is DBNull ? "" : reader["SubGrupo"].ToString()

                    });
                }

                return new ApiResponse<List<ProductHierarchyItem>>
                {
                    Success = true,
                    Message = "Jerarquía de productos obtenida exitosamente",
                    Data = hierarchy
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ProductHierarchyItem>>
                {
                    Success = false,
                    Message = $"Error al obtener jerarquía de productos: {ex.Message}",
                    Data = new List<ProductHierarchyItem>()
                };
            }
        }

        public async Task<ApiResponse<bool>> DeleteAssignmentAsync(int assignmentId, int deletedBy)
        {
            try
            {
                const string query = @"
                    UPDATE UserProductAssignments 
                    SET IsActive = 0 
                    WHERE ID = @AssignmentID";

                using var connection = new SqlConnection(_inventarioConnection);
                using var command = new SqlCommand(query, connection);

                command.Parameters.AddWithValue("@AssignmentID", assignmentId);

                await connection.OpenAsync();
                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Message = "Asignación eliminada exitosamente",
                        Data = true
                    };
                }

                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "No se encontró la asignación especificada",
                    Data = false
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Error al eliminar asignación: {ex.Message}",
                    Data = false
                };
            }
        }

        public async Task<ApiResponse<List<AssignmentResponse>>> GetUserAssignmentsByTiendaAsync(string tienda)
        {
            var request = new GetAssignmentsRequest { Tienda = tienda, IsActive = true };
            return await GetAssignmentsAsync(request);
        }

        // Métodos privados auxiliares
        private async Task<bool> ValidateUserExistsAsync(int userId)
        {
            const string query = "SELECT COUNT(1) FROM Usuarios WHERE ID = @UserID AND IsActive = 1";

            using var connection = new SqlConnection(_inventarioConnection);
            using var command = new SqlCommand(query, connection);

            command.Parameters.AddWithValue("@UserID", userId);
            await connection.OpenAsync();

            var result = await command.ExecuteScalarAsync();
            var count = Convert.ToInt32(result);
            return count > 0;
        }

        private async Task DeactivatePreviousAssignmentAsync(int userId, string tienda, AssignmentType assignmentType)
        {
            const string query = @"
                UPDATE UserProductAssignments 
                SET IsActive = 0 
                WHERE UserID = @UserID AND Tienda = @Tienda AND AssignmentType = @AssignmentType AND IsActive = 1";

            using var connection = new SqlConnection(_inventarioConnection);
            using var command = new SqlCommand(query, connection);

            command.Parameters.AddWithValue("@UserID", userId);
            command.Parameters.AddWithValue("@Tienda", tienda);
            command.Parameters.AddWithValue("@AssignmentType", assignmentType.ToString());

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        private async Task<AssignmentResponse> GetAssignmentByIdAsync(int assignmentId)
        {
            var request = new GetAssignmentsRequest();
            var allAssignments = await GetAssignmentsAsync(request);
            
            return allAssignments.Data?.FirstOrDefault(a => a.ID == assignmentId)
                ?? throw new InvalidOperationException("Assignment not found");
        }
    }
}
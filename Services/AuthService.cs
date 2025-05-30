using InventarioAPI.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Data;

namespace InventarioAPI.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
    }

    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AuthService(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("InventarioConnection") 
                ?? throw new InvalidOperationException("Connection string not found");
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                // Validar credenciales en la base de datos
                var user = await ValidateUserCredentialsAsync(request.Usuario, request.Password);
                
                if (user == null)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Usuario o contraseña incorrectos"
                    };
                }

                // Generar token JWT
                var token = GenerateJwtToken(user);
                var expiresAt = DateTime.UtcNow.AddHours(
                    int.Parse(_configuration["JwtSettings:ExpirationHours"] ?? "8")
                );

                return new LoginResponse
                {
                    Success = true,
                    Message = "Login exitoso",
                    Token = token,
                    User = user,
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Error durante la autenticación: {ex.Message}"
                };
            }
        }

        private async Task<UserInfo> ValidateUserCredentialsAsync(string usuario, string password)
        {
            // Consulta ajustada según el esquema real de tu tabla
            const string query = @"
                SELECT ID, USUARIO, NOMBRE, PERFIL, TIENDA, AREA
                FROM Usuarios 
                WHERE USUARIO = @Usuario AND PASSWORD = @Password AND IsActive = 1";

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection);
            
            command.Parameters.AddWithValue("@Usuario", usuario);
            command.Parameters.AddWithValue("@Password", password);
            
            await connection.OpenAsync();
            
            using var reader = await command.ExecuteReaderAsync();
            
         if (await reader.ReadAsync())
{
    return new UserInfo
    {
        Id = reader["ID"] is DBNull ? 0 : Convert.ToInt32(reader["ID"]),
        Usuario = reader["USUARIO"] is DBNull ? "" : reader["USUARIO"].ToString(),
        Nombre = reader["NOMBRE"] is DBNull ? "" : reader["NOMBRE"].ToString(),
        Perfil = reader["PERFIL"] is DBNull ? "" : reader["PERFIL"].ToString(),
        Tienda = reader["TIENDA"] is DBNull ? "" : reader["TIENDA"].ToString(),
        Area = reader["AREA"] is DBNull ? "" : reader["AREA"].ToString()
    };
}

            
            return null;
        }

        private string GenerateJwtToken(UserInfo user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? 
                throw new InvalidOperationException("JWT Secret Key not found");
            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Usuario),
                new Claim("nombre", user.Nombre),
                new Claim("perfil", user.Perfil),
                new Claim("tienda", user.Tienda),
                new Claim("area", user.Area),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, 
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), 
                    ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(
                    int.Parse(jwtSettings["ExpirationHours"] ?? "8")
                ),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}